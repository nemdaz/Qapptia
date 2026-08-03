using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Qapptia.Core.Abstractions;
using System.Media; // Nativo de Windows

namespace Qapptia.Platform.Windows;

public sealed class WindowsShutterSoundService : IShutterSoundService, IDisposable
{
    private const string ResourceName = "Qapptia.Core.Assets.Sounds.shutter_a.wav";
    private readonly ILogger<WindowsShutterSoundService> _logger;
    
    private readonly SoundPlayer? _player;

    public WindowsShutterSoundService(ILogger<WindowsShutterSoundService> logger)
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
                // Copiamos a un MemoryStream en memoria porque SoundPlayer requiere
                // acceso exclusivo al stream de origen.
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                stream.Dispose();

                _player = new SoundPlayer(ms);
                // Carga el sonido en memoria de inmediato
                _player.Load();
            }
            else
            {
                _logger.LogWarning("Recurso embebido {Name} no encontrado", ResourceName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inicializar el sonido del obturador con SoundPlayer.");
        }
    }

    public Task PlayAsync(CancellationToken ct = default)
    {
        if (_player is null)
            return Task.CompletedTask;

        try
        {
            // Play() es nativo de Win32 (PlaySound API) y se ejecuta 
            // de forma totalmente asíncrona en su propio hilo de sistema.
            _player.Play();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo reproducir shutter sound");
        }
        
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _player?.Stream?.Dispose();
        _player?.Dispose();
    }
}