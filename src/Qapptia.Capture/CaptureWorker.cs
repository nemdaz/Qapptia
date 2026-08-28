using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Qapptia.Core.Abstractions;
using Qapptia.Core.Capture;
using Qapptia.Core.Configuration;
using Qapptia.Core.Ipc;
using Serilog;

namespace Qapptia.Capture;

public sealed class CaptureWorker : BackgroundService, ICaptureActionHandler
{
    private readonly IHotkeyRegistrar _hotkeys;
    private readonly IFullscreenCaptureService _fullscreenCapture;
    private readonly IAreaCaptureService _areaCapture;
    private readonly IConfigService _config;
    private readonly IPowerEvents _powerEvents;
    private readonly IShutterSoundService _shutterSound;
    private readonly ILogger _logger;
    private readonly Channel<CaptureJob> _channel = Channel.CreateBounded<CaptureJob>(4);

    private IHotkeyHandle? _hotkeyScreen;
    private IHotkeyHandle? _hotkeyArea;
    private IHotkeyHandle? _hotkeyFlow;

    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private DateTime _lastFlowHotkey = DateTime.MinValue;

    public CaptureWorker(
        IHotkeyRegistrar hotkeys,
        IFullscreenCaptureService fullscreenCapture,
        IAreaCaptureService areaCapture,
        IConfigService config,
        IPowerEvents powerEvents,
        IShutterSoundService shutterSound,
        ILogger logger)
    {
        _hotkeys = hotkeys;
        _fullscreenCapture = fullscreenCapture;
        _areaCapture = areaCapture;
        _config = config;
        _powerEvents = powerEvents;
        _shutterSound = shutterSound;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("CaptureWorker iniciado");
        SetupPowerEvents();
        RegisterHotkeys();

        await ProcessChannelAsync(stoppingToken);
    }

    public async Task HandleWakeUpAsync(CancellationToken ct)
    {
        _logger.Information("WakeUp recibido");
    }

    public async Task HandleQuitAsync(CancellationToken ct)
    {
        _logger.Information("Quit recibido — deteniendo worker");
        await StopAsync(ct);
    }

    public async Task HandleRefreshTrayAsync(CancellationToken ct)
    {
        _logger.Information("Refrescando tray, config y hotkeys");
        _config.Reload();
        UnregisterHotkeys();
        RegisterHotkeys();
    }

    public async Task HandleFullscreenCaptureAsync(CancellationToken ct)
    {
        await _channel.Writer.WriteAsync(new CaptureJob
        {
            Mode = CaptureMode.Fullscreen,
            DelayMs = _config.Current.ManualTimer * 1000,
            IncludeCursor = _config.Current.ShowMouse,
        }, ct);
    }

    public async Task HandleAreaCaptureAsync(CancellationToken ct)
    {
        await _channel.Writer.WriteAsync(new CaptureJob
        {
            Mode = CaptureMode.Area,
            DelayMs = _config.Current.ManualTimer * 1000,
            IncludeCursor = _config.Current.ShowMouse,
        }, ct);
    }

    private async Task ProcessChannelAsync(CancellationToken ct)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(ct))
        {
            await _captureGate.WaitAsync(ct);
            try
            {
                CaptureResult result;

                if (job.Mode == CaptureMode.Area)
                {
                    // Si hay delay, esperamos antes de congelar la pantalla
                    if (job.DelayMs > 0) await Task.Delay(job.DelayMs, ct);

                    // 1. Tomar la foto de toda la pantalla y congelarla en memoria
                    var frozenScreen = await _fullscreenCapture.CaptureFrozenScreenAsync(job.IncludeCursor, ct);

                    // 2. Pasar la foto congelada al selector de área para que la dibuje de fondo
                    var area = await _areaCapture.SelectAreaAsync(frozenScreen, ct);
                    if (area is null)
                    {
                        _logger.Information("Selección de área cancelada");
                        continue;
                    }

                    // 3. Finalizar la captura procesando el recorte en base a la foto original congelada
                    result = await _fullscreenCapture.FinalizeFrozenAreaCaptureAsync(frozenScreen, area, job, ct);
                }
                else
                {
                    result = await _fullscreenCapture.CaptureAsync(job, ct);
                }

                _logger.Information("Captura {Mode} -> {Path} ({W}x{H})",
                    job.Mode, result.FilePath, result.Width, result.Height);

                _logger.Debug("Invocando PlayAsync desde CaptureWorker (Mode={Mode})", job.Mode);
                _ = _shutterSound.PlayAsync(CancellationToken.None);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error en captura {Mode}", job.Mode);
            }
            finally
            {
                _captureGate.Release();
            }
        }
    }



    private void RegisterHotkeys()
    {
        try
        {
            var screenShortcut = ParseShortcut(_config.Current.ShortcutScreen);
            if (screenShortcut is { } s)
                _hotkeyScreen = _hotkeys.Register(s.Modifiers, s.Key, () =>
                {
                    _ = HandleFullscreenCaptureAsync(CancellationToken.None);
                });

            var areaShortcut = ParseShortcut(_config.Current.ShortcutArea);
            if (areaShortcut is { } a)
                _hotkeyArea = _hotkeys.Register(a.Modifiers, a.Key, () =>
                {
                    _ = HandleAreaCaptureAsync(CancellationToken.None);
                });

            var flowShortcut = ParseShortcut(_config.Current.ShortcutFlow);
            if (flowShortcut is { } f)
                _hotkeyFlow = _hotkeys.Register(f.Modifiers, f.Key, () =>
                {
                    HandleFlowHotkey();
                });
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Fallo registro hotkeys");
        }
    }

    private void HandleFlowHotkey()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastFlowHotkey).TotalSeconds < 0.5)
        {
            _ = HandleAreaCaptureAsync(CancellationToken.None);
            _lastFlowHotkey = DateTime.MinValue;
        }
        else
        {
            _ = HandleFullscreenCaptureAsync(CancellationToken.None);
            _lastFlowHotkey = now;
        }
    }

    private void UnregisterHotkeys()
    {
        _hotkeyScreen?.Dispose();
        _hotkeyArea?.Dispose();
        _hotkeyFlow?.Dispose();
        _hotkeyScreen = null;
        _hotkeyArea = null;
        _hotkeyFlow = null;
    }

    private void SetupPowerEvents()
    {
        _powerEvents.PowerModeChanged += (_, mode) =>
        {
            _logger.Information("Power event: {Mode}", mode);
            if (mode == PowerMode.Resume && _powerEvents.RequiresHotkeyReRegistrationAfterResume)
            {
                UnregisterHotkeys();
                RegisterHotkeys();
                _logger.Information("Hotkeys re-registrados tras resume");
            }
        };
    }

    private static (HotkeyModifiers Modifiers, uint Key)? ParseShortcut(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) return null;
        var parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return null;

        var modifiers = HotkeyModifiers.None;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i].ToLowerInvariant() switch
            {
                "ctrl" => HotkeyModifiers.Control,
                "alt" => HotkeyModifiers.Alt,
                "shift" => HotkeyModifiers.Shift,
                "win" => HotkeyModifiers.Win,
                _ => HotkeyModifiers.None,
            };
        }

        var keyName = parts[^1].ToLowerInvariant();
        uint vk = keyName switch
        {
            "q" => 0x51,
            "a" => 0x41,
            "f" => 0x46,
            "s" => 0x53,
            "d" => 0x44,
            "c" => 0x43,
            "v" => 0x56,
            "x" => 0x58,
            "z" => 0x5A,
            "1" => 0x31,
            "2" => 0x32,
            "3" => 0x33,
            "4" => 0x34,
            "5" => 0x35,
            "6" => 0x36,
            "7" => 0x37,
            "8" => 0x38,
            "9" => 0x39,
            "0" => 0x30,
            "printscreen" => 0x2C,
            "prtsc" => 0x2C,
            "escape" => 0x1B,
            "esc" => 0x1B,
            "space" => 0x20,
            "enter" => 0x0D,
            "tab" => 0x09,
            "backspace" => 0x08,
            "delete" => 0x2E,
            "del" => 0x2E,
            "insert" => 0x2D,
            "ins" => 0x2D,
            "home" => 0x24,
            "end" => 0x23,
            "pageup" => 0x21,
            "pagedown" => 0x22,
            "up" => 0x26,
            "down" => 0x28,
            "left" => 0x25,
            "right" => 0x27,
            _ when keyName.Length == 1 && char.IsAsciiLetterUpper(keyName[0]) => (uint)(keyName[0]),
            _ when keyName.Length == 1 && char.IsAsciiLetterLower(keyName[0]) => (uint)char.ToUpperInvariant(keyName[0]),
            _ when keyName.Length == 1 && char.IsDigit(keyName[0]) => (uint)(0x30 + (keyName[0] - '0')),
            _ => 0,
        };

        if (vk == 0) return null;
        return (modifiers, vk);
    }
}
