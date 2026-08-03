using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Qapptia.Core.Abstractions;

namespace Qapptia.Platform.Windows;

public sealed class WindowsShutterSoundService : IShutterSoundService
{
    private const string ResourceName = "Qapptia.Core.Assets.Sounds.shutter_a.wav";
    private readonly ILogger<WindowsShutterSoundService> _logger;
    
    private readonly byte[]? _audioData;
    private readonly WaveFormat? _waveFormat;

    public WindowsShutterSoundService(ILogger<WindowsShutterSoundService> logger)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("WindowsShutterSoundService requiere Windows.");
        
        _logger = logger;

        try
        {
            var coreAsm = Assembly.GetAssembly(typeof(IShutterSoundService))
                ?? throw new InvalidOperationException("No se encontró el ensamblado Qapptia.Core");
                
            using var stream = coreAsm.GetManifestResourceStream(ResourceName);
            if (stream is not null)
            {
                using var reader = new WaveFileReader(stream);
                _waveFormat = reader.WaveFormat;
                
                // Leemos los frames completos a la memoria (igual que el legacy dict en Python)
                _audioData = new byte[reader.Length];
                int read = reader.Read(_audioData, 0, _audioData.Length);
                if (read < _audioData.Length)
                {
                    Array.Resize(ref _audioData, read);
                }
            }
            else
            {
                _logger.LogWarning("Recurso embebido {Name} no encontrado", ResourceName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inicializar el sonido del obturador.");
        }
    }

    public Task PlayAsync(CancellationToken ct = default)
    {
        if (_audioData is null || _waveFormat is null)
            return Task.CompletedTask;

        try
        {
            // Estrategia "legacy" de Python (RawOutputStream + frames en memoria)
            // Fire-and-forget: WaveOutEvent reproduce en background y se limpia a sí mismo al terminar.
            var ms = new MemoryStream(_audioData);
            var provider = new RawSourceWaveStream(ms, _waveFormat);
            
            var output = new WaveOutEvent { DesiredLatency = 100 };
            output.Init(provider);
            
            output.PlaybackStopped += (s, e) =>
            {
                try
                {
                    output.Dispose();
                    provider.Dispose();
                    ms.Dispose();
                }
                catch { }
            };
            
            output.Play();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo reproducir shutter sound");
        }
        
        return Task.CompletedTask;
    }
}