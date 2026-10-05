using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Qapptia.Core;
using Qapptia.Core.Abstractions;
using Qapptia.Platform.Windows.UI;
using Serilog;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Qapptia.Platform.Windows;

/// <summary>
/// Implementación de bajo nivel del ciclo de vida del icono de bandeja de sistema (Shell_NotifyIconW) en Win32.
/// Delega la renderización y presentación del menú contextual a <see cref="NativeTrayMenuRenderer"/>.
/// </summary>
public sealed class WindowsTrayIconService : ITrayIconService
{
    private const int TrayIconId = 1;
    private const uint WM_USER = 0x0400;
    private const uint WM_TRAYICON = WM_USER + 100;
    private const uint WM_REFRESH_TRAYICON = WM_USER + 101;
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_DESTROY = 0x0002;
    private const uint WM_QUERYENDSESSION = 0x0011;
    private const uint WM_ENDSESSION = 0x0016;
    private const uint WM_POWERBROADCAST = 0x0218;
    private const uint WM_DISPLAYCHANGE = 0x007E;
    private const long PBT_APMRESUMEAUTOMATIC = 0x0012;
    private const long PBT_APMRESUMESUSPEND = 0x0007;

    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_CONTEXTMENU = 0x007B;

    private readonly ILogger _logger;
    private readonly Thread _staThread;
    private readonly ManualResetEventSlim _ready = new();

    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _hIcon = IntPtr.Zero;
    private uint _wmTaskbarCreated;
    private TrayMenuDefinition? _menuDefinition;
    private string? _iconPath;
    private bool _disposed;
    private WndProcDelegate? _wndProcDelegate;

    public WindowsTrayIconService(ILogger logger)
    {
        _logger = logger;

        _staThread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "WindowsNativeTrayIconLoop"
        };
        _staThread.SetApartmentState(ApartmentState.STA);
    }

    public void Initialize(TrayMenuDefinition menu, string iconPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _menuDefinition = menu;
        _iconPath = iconPath;

        _staThread.Start();
        _ready.Wait();
    }

    private void RunMessageLoop()
    {
        try
        {
            _wndProcDelegate = WndProc;
            IntPtr hInst = GetModuleHandle(null);
            _wmTaskbarCreated = RegisterWindowMessage("TaskbarCreated");

            var wndClass = new WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = _wndProcDelegate,
                hInstance = hInst,
                lpszClassName = "QapptiaNativeTrayWindow_" + Guid.NewGuid().ToString("N")
            };

            ushort regResult = RegisterClassEx(ref wndClass);
            if (regResult == 0)
            {
                _logger.Warning("No se pudo registrar la clase Win32 para la bandeja de sistema.");
            }

            _hwnd = CreateWindowEx(
                0,
                wndClass.lpszClassName,
                "QapptiaTrayMessageWindow",
                0,
                0, 0, 0, 0,
                IntPtr.Zero,
                IntPtr.Zero,
                hInst,
                IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
            {
                _logger.Error("Error al crear ventana de mensajería Win32 para la bandeja.");
                return;
            }

            int cx = GetSystemMetrics(49); // SM_CXSMICON
            int cy = GetSystemMetrics(50); // SM_CYSMICON
            if (!string.IsNullOrEmpty(_iconPath) && File.Exists(_iconPath))
            {
                _hIcon = LoadImage(IntPtr.Zero, _iconPath, 1, cx, cy, 0x10); // IMAGE_ICON, LR_LOADFROMFILE
            }

            if (_hIcon == IntPtr.Zero)
            {
                _hIcon = ExtractIcon(hInst, Environment.ProcessPath ?? "", 0);
            }

            AddOrUpdateTrayIcon();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error crítico al inicializar bandeja de sistema Win32.");
        }
        finally
        {
            _ready.Set();
        }

        while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TRAYICON)
        {
            uint mouseMsg = (uint)(lParam.ToInt64() & 0xFFFF);
            switch (mouseMsg)
            {
                case WM_RBUTTONUP:
                case WM_CONTEXTMENU:
                    if (_menuDefinition != null && _hwnd != IntPtr.Zero)
                    {
                        NativeTrayMenuRenderer.Show(_hwnd, _menuDefinition);
                    }
                    return IntPtr.Zero;

                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                    if (_menuDefinition != null)
                    {
                        NativeTrayMenuRenderer.ExecuteDefaultAction(_menuDefinition);
                    }
                    return IntPtr.Zero;
            }
        }
        else if (msg == WM_REFRESH_TRAYICON || (_wmTaskbarCreated != 0 && msg == _wmTaskbarCreated))
        {
            _logger.Information("Recreando/refrescando icono de bandeja tras mensaje del shell (0x{Msg:X4}).", msg);
            AddOrUpdateTrayIcon();
            return IntPtr.Zero;
        }
        else if (msg == WM_POWERBROADCAST)
        {
            long powerEvent = wParam.ToInt64();
            if (powerEvent == PBT_APMRESUMEAUTOMATIC || powerEvent == PBT_APMRESUMESUSPEND)
            {
                _logger.Information("Reanudación del sistema detectada (WM_POWERBROADCAST: 0x{PowerEvent:X4}). Restaurando icono.", powerEvent);
                AddOrUpdateTrayIcon();
            }
            return (IntPtr)1;
        }
        else if (msg == WM_DISPLAYCHANGE)
        {
            _logger.Debug("Cambio de configuración de pantalla detectado (WM_DISPLAYCHANGE).");
            AddOrUpdateTrayIcon();
            return IntPtr.Zero;
        }
        else if (msg == WM_QUERYENDSESSION)
        {
            return (IntPtr)1;
        }
        else if (msg == WM_ENDSESSION)
        {
            if (wParam != IntPtr.Zero)
            {
                _logger.Information("Fin de sesión del sistema en curso (WM_ENDSESSION). Eliminando icono de bandeja.");
                RemoveTrayIcon();
            }
            return IntPtr.Zero;
        }
        else if (msg == WM_CLOSE)
        {
            DestroyWindow(hWnd);
            return IntPtr.Zero;
        }
        else if (msg == WM_DESTROY)
        {
            PostQuitMessage(0);
            return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void ShowNotification(string title, string message, TrayNotificationType type = TrayNotificationType.Info, int timeoutMs = Constants.NotificationDurationMs)
    {
        if (_disposed || _hwnd == IntPtr.Zero) return;

        NOTIFY_ICON_INFOTIP_FLAGS infoFlags = type switch
        {
            TrayNotificationType.Warning => NOTIFY_ICON_INFOTIP_FLAGS.NIIF_WARNING,
            TrayNotificationType.Error => NOTIFY_ICON_INFOTIP_FLAGS.NIIF_ERROR,
            _ => NOTIFY_ICON_INFOTIP_FLAGS.NIIF_INFO
        };

        unsafe
        {
            var notifyData = new NOTIFYICONDATAW
            {
                cbSize = (uint)sizeof(NOTIFYICONDATAW),
                hWnd = (HWND)_hwnd,
                uID = TrayIconId,
                uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_INFO,
                dwInfoFlags = infoFlags
            };
            notifyData.Anonymous.uTimeout = (uint)timeoutMs;

            CopyStringToBuffer(message ?? "", notifyData.szInfo.AsSpan());
            CopyStringToBuffer(title ?? "", notifyData.szInfoTitle.AsSpan());

            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in notifyData);
        }
    }

    public void RefreshIcon()
    {
        if (_disposed || _hwnd == IntPtr.Zero) return;
        PostMessage(_hwnd, WM_REFRESH_TRAYICON, IntPtr.Zero, IntPtr.Zero);
    }

    private void AddOrUpdateTrayIcon()
    {
        if (_disposed || _hwnd == IntPtr.Zero || _hIcon == IntPtr.Zero) return;

        unsafe
        {
            var notifyData = new NOTIFYICONDATAW
            {
                cbSize = (uint)sizeof(NOTIFYICONDATAW),
                hWnd = (HWND)_hwnd,
                uID = TrayIconId,
                uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = (HICON)_hIcon
            };

            CopyStringToBuffer(Constants.CaptureAppName, notifyData.szTip.AsSpan());

            BOOL added = PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, in notifyData);
            if (added)
            {
                _logger.Information("WindowsTrayIconService: Icono de bandeja registrado exitosamente (NIM_ADD).");
            }
            else
            {
                BOOL modified = PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in notifyData);
                if (modified)
                {
                    _logger.Debug("WindowsTrayIconService: Icono de bandeja actualizado (NIM_MODIFY).");
                }
                else
                {
                    int lastError = Marshal.GetLastWin32Error();
                    _logger.Warning("WindowsTrayIconService: Shell_NotifyIcon no pudo registrar ni modificar el icono (Win32 Error: {Error}).", lastError);
                }
            }
        }
    }

    private void RemoveTrayIcon()
    {
        if (_hwnd == IntPtr.Zero) return;
        unsafe
        {
            var notifyData = new NOTIFYICONDATAW
            {
                cbSize = (uint)sizeof(NOTIFYICONDATAW),
                hWnd = (HWND)_hwnd,
                uID = TrayIconId
            };
            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in notifyData);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hwnd != IntPtr.Zero)
        {
            RemoveTrayIcon();
            PostMessage(_hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }

        _staThread.Join(1000);
        _ready.Dispose();
    }

    private static unsafe void CopyStringToBuffer(string text, Span<char> buffer)
    {
        buffer.Clear();
        if (string.IsNullOrEmpty(text)) return;
        ReadOnlySpan<char> span = text.AsSpan();
        int len = Math.Min(span.Length, buffer.Length - 1);
        span[..len].CopyTo(buffer);
    }

    #region Win32 Shell & Window P/Invoke

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public int style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
        int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern sbyte GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string pszExeFileName, int nIconIndex);

    #endregion
}
