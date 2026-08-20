using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Serilog;
using Qapptia.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Qapptia.Platform.Windows;

/// <summary>
/// Registrar de hotkeys globales en Windows usando <c>RegisterHotKey</c> + una message-only window
/// oculta (HWND_MESSAGE) que bombea mensajes WM_HOTKEY en un thread STA dedicado. Todas las llamadas
/// a RegisterHotKey/UnregisterHotKey se marsheaen al thread propietario del HWND via cola y PostMessage.
/// </summary>
public sealed class WindowsHotkeyRegistrar : IHotkeyRegistrar, IDisposable
{
    private const int WM_QUIT = 0x0012;
    private const int WM_HOTKEY = 0x0312;
    private const uint WM_REGISTER = 0x0400 + 1;
    private const uint WM_UNREGISTER = 0x0400 + 2;

    private readonly ILogger _logger;
    private readonly Thread _messageThread;
    private readonly ManualResetEventSlim _ready = new();
    private readonly BlockingCollection<RegisterRequest> _pending = new();
    private readonly ConcurrentQueue<int> _unregisterQueue = new();
    private HWND _hwnd;
    private int _nextId;
    private readonly Dictionary<int, Registration> _registrations = new();
    private volatile bool _running = true;

    public WindowsHotkeyRegistrar(ILogger logger)
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
        ObjectDisposedException.ThrowIf(!_running, this);

        var id = Interlocked.Increment(ref _nextId);
        var req = new RegisterRequest(id, modifiers, virtualKey, callback, this);

        _pending.Add(req);
        try { PInvoke.PostMessage(_hwnd, WM_REGISTER, 0, 0); }
        catch { _pending.TryTake(out _); throw; }

        // Bloquea hasta que el message thread procese.
        return req.Task.Task.GetAwaiter().GetResult();
    }

    internal void Unregister(int id)
    {
        if (!_running) return;
        _unregisterQueue.Enqueue(id);
        try { PInvoke.PostMessage(_hwnd, WM_UNREGISTER, 0, 0); } catch { }
    }

    private void ProcessRegistrations()
    {
        while (_pending.TryTake(out var req))
        {
            try
            {
                var win32Modifiers = MapModifiers(req.Modifiers);
                lock (_registrations) { _registrations[req.Id] = req.Registration; }

                if (!PInvoke.RegisterHotKey(_hwnd, req.Id, win32Modifiers, req.VirtualKey))
                {
                    lock (_registrations) { _registrations.Remove(req.Id); }
                    var err = Marshal.GetLastWin32Error();
                    req.Task.SetException(new InvalidOperationException(
                        $"RegisterHotKey falló (err={err}) para mod={req.Modifiers} vk=0x{req.VirtualKey:X}"));
                    continue;
                }

                _logger.Information("Hotkey registrado id={Id} mod={Mod} vk={Vk}",
                    req.Id, req.Modifiers, req.VirtualKey);
                req.Task.SetResult(req.Registration);
            }
            catch (Exception ex)
            {
                req.Task.SetException(ex);
            }
        }
    }

    private void ProcessUnregistrations()
    {
        while (_unregisterQueue.TryDequeue(out var id))
        {
            try { PInvoke.UnregisterHotKey(_hwnd, id); } catch { }
            lock (_registrations) { _registrations.Remove(id); }
            _logger.Debug("Hotkey desregistrado id={Id}", id);
        }
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

    private unsafe void MessageLoop()
    {
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
                (WINDOW_EX_STYLE)0,
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

            switch (msg.message)
            {
                    case WM_HOTKEY:
                    {
                        var id = (int)msg.wParam.Value;
                        Registration? reg;
                        lock (_registrations) { _registrations.TryGetValue(id, out reg); }
                        if (reg is not null)
                        {
                            var now = DateTime.UtcNow;
                            _logger.Debug("WM_HOTKEY recibido para id={Id}. LastFired={LastFired:HH:mm:ss.fff}, Now={Now:HH:mm:ss.fff}", id, reg.LastFired, now);
                            if ((now - reg.LastFired).TotalMilliseconds > 400)
                            {
                                reg.LastFired = now;
                                try { reg.OnClickInternal(); }
                                catch (Exception ex) { _logger.Error(ex, "Hotkey callback id={Id}", id); }
                            }
                        }
                        break;
                    }
                case WM_REGISTER:
                    ProcessRegistrations();
                    break;
                case WM_UNREGISTER:
                    ProcessUnregistrations();
                    break;
                default:
                    PInvoke.TranslateMessage(in msg);
                    PInvoke.DispatchMessage(in msg);
                    break;
            }
        }
    }

    private static LRESULT WndProcStatic(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
        => PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);

    public void Dispose()
    {
        _running = false;
        _pending.CompleteAdding();
        try { PInvoke.PostMessage(_hwnd, WM_QUIT, 0, 0); } catch { }
        _messageThread.Join(1000);
        try { _pending.Dispose(); } catch { }
    }

    private sealed class RegisterRequest
    {
        public int Id { get; }
        public HotkeyModifiers Modifiers { get; }
        public uint VirtualKey { get; }
        public Registration Registration { get; }
        public TaskCompletionSource<IHotkeyHandle> Task { get; } = new();

        public RegisterRequest(int id, HotkeyModifiers modifiers, uint virtualKey, Action onClick, WindowsHotkeyRegistrar owner)
        {
            Id = id;
            Modifiers = modifiers;
            VirtualKey = virtualKey;
            Registration = new Registration(id, modifiers, virtualKey, onClick, owner);
        }
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
        public DateTime LastFired { get; set; } = DateTime.MinValue;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _owner.Unregister(Id);
        }
    }
}
