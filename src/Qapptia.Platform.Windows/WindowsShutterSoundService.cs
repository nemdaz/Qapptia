using System.IO;
using System.Reflection;
using Serilog;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Qapptia.Core.Abstractions;

namespace Qapptia.Platform.Windows;

public sealed class WindowsShutterSoundService : IShutterSoundService, IDisposable
{
    private const string ResourceName = "Qapptia.Core.Assets.Sounds.shutter_a.wav";
    private readonly ILogger _logger;
    
    private readonly WaveOutEvent? _waveOut;
    private readonly MixingSampleProvider? _mixer;
    private readonly float[]? _cachedAudioSamples;
    private readonly WaveFormat? _waveFormat;

    public WindowsShutterSoundService(ILogger logger)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("WindowsShutterSoundService requiere Windows.");
        
        _logger = logger;

        try
        {
            var coreAsm = Assembly.GetAssembly(typeof(IShutterSoundService))
                ?? throw new InvalidOperationException("No se encontró el ensamblado Qapptia.Core");
                
            var stream = coreAsm.GetManifestResourceStream(ResourceName);
            if (stream is not null)
            {
                using var reader = new WaveFileReader(stream);
                
                // Convertimos a ISampleProvider (IEEE Float 32-bit) que es el estándar del Mixer
                ISampleProvider sampleProvider = reader.ToSampleProvider();
                _waveFormat = sampleProvider.WaveFormat;
                
                // Leemos absolutamente todos los samples a un array flotante
                var wholeFile = new List<float>((int)(reader.Length / 4));
                var readBuffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
                int samplesRead;
                while ((samplesRead = sampleProvider.Read(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    wholeFile.AddRange(readBuffer.Take(samplesRead));
                }
                _cachedAudioSamples = wholeFile.ToArray();

                // Inicializamos el Hot Mixer que nunca se detiene
                _mixer = new MixingSampleProvider(_waveFormat)
                {
                    ReadFully = true // ¡CRÍTICO! Mantiene el stream vivo enviando silencio cuando no hay sonidos
                };
                
                // Inicializamos WaveOutEvent (el driver de audio nativo) y lo mantenemos caliente
                _waveOut = new WaveOutEvent { DesiredLatency = 100 };
                _waveOut.Init(_mixer);
                _waveOut.Play(); // Comienza a reproducir silencio infinito en background
                
                _logger.Information("Hot Audio Engine inicializado con {Samples} samples a {Rate}Hz", 
                    _cachedAudioSamples.Length, _waveFormat.SampleRate);
            }
            else
            {
                _logger.Warning("Recurso embebido {Name} no encontrado", ResourceName);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error al inicializar el Hot Audio Engine");
        }
    }

    public Task PlayAsync(CancellationToken ct = default)
    {
        if (_mixer is null || _cachedAudioSamples is null || _waveFormat is null)
            return Task.CompletedTask;

        try
        {
            // Simplemente inyectamos un clon del sonido al canal del Mixer que ya está corriendo!
            var provider = new CachedSoundSampleProvider(_cachedAudioSamples, _waveFormat);
            _mixer.AddMixerInput(provider);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "No se pudo inyectar shutter sound en el mixer");
        }
        
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
    }
    
    // Proveedor que sirve el array de flotantes desde memoria (Thread-safe para el mixer)
    private sealed class CachedSoundSampleProvider : ISampleProvider
    {
        private readonly float[] _audioData;
        private int _position;

        public CachedSoundSampleProvider(float[] audioData, WaveFormat waveFormat)
        {
            _audioData = audioData;
            WaveFormat = waveFormat;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            var availableSamples = _audioData.Length - _position;
            var samplesToCopy = Math.Min(availableSamples, count);
            if (samplesToCopy == 0) return 0;
            
            Array.Copy(_audioData, _position, buffer, offset, samplesToCopy);
            _position += samplesToCopy;
            return samplesToCopy;
        }
    }
}
