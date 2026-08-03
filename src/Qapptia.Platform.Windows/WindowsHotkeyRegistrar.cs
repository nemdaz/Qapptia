using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Qapptia.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Qapptia.Platform.Windows;

/// <summary>
/// Registrar de hotkeys globales en Windows usando <c>RegisterHotKey</c> + una message-only window
/// oculta (HWND_MESSAGE) que bombea mensajes WM_HOTKEY en un thread STA dedicado.
/// </summary>
public sealed class WindowsHotkeyRegistrar : IHotkeyRegistrar, IDisposable
{
    private const int WM_QUIT = 0x0012;
    private const int WM_HOTKEY = 0x0312;
    private const uint WINDOW_EX_STYLE_None = 0;

    private readonly ILogger<WindowsHotkeyRegistrar> _logger;
    private readonly Thread _messageThread;
    private readonly ManualResetEventSlim _ready = new();
    private HWND _hwnd;
    private int _nextId;
    private readonly Dictionary<int, Registration> _registrations = new();
    private volatile bool _running = true;

    public WindowsHotkeyRegistrar(ILogger<WindowsHotkeyRegistrar> logger)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("WindowsHotkeyRegistrar requiere Windows.");
        _logger = logger;

        _messageThread = new Thread(MessageLoop)
        {
            IsBackground = true,
            Name = "HotkeyMessageLoop",
        };
        _messageThread.SetApartmentState(ApartmentState.STA);
        _messageThread.Start();
        _ready.Wait();
    }

    public IHotkeyHandle Register(HotkeyModifiers modifiers, uint virtualKey, Action callback)
    {
        if (!_running) throw new ObjectDisposedException(nameof(WindowsHotkeyRegistrar));

        var id = Interlocked.Increment(ref _nextId);
        var win32Modifiers = MapModifiers(modifiers);
        var reg = new Registration(id, modifiers, virtualKey, callback, this);

        lock (_registrations) { _registrations[id] = reg; }

        if (!PInvoke.RegisterHotKey(_hwnd, id, win32Modifiers, virtualKey))
        {
            lock (_registrations) { _registrations.Remove(id); }
            var err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"RegisterHotKey falló (err={err}) para mod={modifiers} vk=0x{virtualKey:X}");
        }

        _logger.LogInformation("Hotkey registrado id={Id} mod={Mod} vk={Vk}", id, modifiers, virtualKey);
        return reg;
    }

    private static HOT_KEY_MODIFIERS MapModifiers(HotkeyModifiers m)
    {
        HOT_KEY_MODIFIERS result = 0;
        if ((m & HotkeyModifiers.Alt) != 0) result |= HOT_KEY_MODIFIERS.MOD_ALT;
        if ((m & HotkeyModifiers.Control) != 0) result |= HOT_KEY_MODIFIERS.MOD_CONTROL;
        if ((m & HotkeyModifiers.Shift) != 0) result |= HOT_KEY_MODIFIERS.MOD_SHIFT;
        if ((m & HotkeyModifiers.Win) != 0) result |= HOT_KEY_MODIFIERS.MOD_WIN;
        return result;
    }

    internal void Unregister(int id)
    {
        try { PInvoke.UnregisterHotKey(_hwnd, id); } catch { }
        lock (_registrations) { _registrations.Remove(id); }
        _logger.LogDebug("Hotkey desregistrado id={Id}", id);
    }

    private unsafe void MessageLoop()
    {
        // GetModuleHandle((string?)null) devuelve HMODULE del proceso actual.
        var hinst = PInvoke.GetModuleHandle((string?)null);
        var hinstance = new HINSTANCE(hinst.DangerousGetHandle());
        hinst.Dispose();

        var className = "QapptiaHotkeyWnd".AsSpan();
        var windowName = "QapptiaHotkey".AsSpan();

        fixed (char* pClassName = className)
        fixed (char* pWindowName = windowName)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = WndProcStatic,
                lpszClassName = pClassName,
                hInstance = hinstance,
            };
            ushort atom = PInvoke.RegisterClassEx(in wc);
            if (atom == 0)
                throw new InvalidOperationException($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");

            _hwnd = PInvoke.CreateWindowEx(
                (WINDOW_EX_STYLE)WINDOW_EX_STYLE_None,
                new PCWSTR(pClassName),
                new PCWSTR(pWindowName),
                WINDOW_STYLE.WS_OVERLAPPED,
                0, 0, 0, 0,
                new HWND(-3), // HWND_MESSAGE
                HMENU.Null,
                hinstance,
                null);
        }

        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");

        _ready.Set();

        while (_running)
        {
            var ret = PInvoke.GetMessage(out var msg, _hwnd, 0, 0);
            if (ret == 0 || ret == -1) break;

            if (msg.message == WM_HOTKEY)
            {
                var id = (int)msg.wParam.Value;
                Registration? reg;
                lock (_registrations) { _registrations.TryGetValue(id, out reg); }
                if (reg is not null)
                {
                    try { reg.OnClickInternal(); }
                    catch (Exception ex) { _logger.LogError(ex, "Hotkey callback id={Id}", id); }
                }
            }
            else
            {
                PInvoke.TranslateMessage(in msg);
                PInvoke.DispatchMessage(in msg);
            }
        }
    }

    private static LRESULT WndProcStatic(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
        => PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);

    public void Dispose()
    {
        _running = false;
        try { PInvoke.PostMessage(_hwnd, WM_QUIT, 0, 0); } catch { }
        _messageThread.Join(1000);
    }

    private sealed class Registration : IHotkeyHandle
    {
        private readonly WindowsHotkeyRegistrar _owner;
        private bool _disposed;

        public Registration(int id, HotkeyModifiers modifiers, uint virtualKey, Action onClick, WindowsHotkeyRegistrar owner)
        {
            Id = id;
            Modifiers = modifiers;
            VirtualKey = virtualKey;
            OnClickInternal = onClick;
            _owner = owner;
        }

        public int Id { get; }
        public HotkeyModifiers Modifiers { get; }
        public uint VirtualKey { get; }
        public bool IsRegistered => !_disposed;
        internal Action OnClickInternal { get; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _owner.Unregister(Id);
        }
    }
}
