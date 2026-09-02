using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Qapptia.Core;
using Qapptia.Core.Abstractions;
using Qapptia.Core.Extensions;
using Qapptia.Platform.Windows.UI;
using Serilog;

namespace Qapptia.Platform.Windows;

public sealed class WindowsTrayIconService : ITrayIconService
{
    private readonly ILogger _logger;
    private readonly Thread _staThread;
    private readonly ManualResetEventSlim _ready = new();

    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private bool _disposed;

    // Almacenamos la definición de inicio temporalmente hasta que arranque el hilo.
    private TrayMenuDefinition? _initialMenu;
    private string? _initialIconPath;

    public WindowsTrayIconService(ILogger logger)
    {
        _logger = logger;

        _staThread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "WindowsTrayIconLoop"
        };
        _staThread.SetApartmentState(ApartmentState.STA);
    }

    public void Initialize(TrayMenuDefinition menu, string iconPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _initialMenu = menu;
        _initialIconPath = iconPath;

        _staThread.Start();
        _ready.Wait(); // Esperamos a que el icono esté creado
    }

    private void RunMessageLoop()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            _contextMenu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(249, 249, 249),
                ForeColor = Color.Black,
                ShowImageMargin = false,
                ShowCheckMargin = false, // Desactivado porque solo hay acciones instantáneas
                Renderer = new ModernTrayMenuRenderer(),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            _contextMenu.Opening += (s, e) =>
            {
                if (Environment.OSVersion.Version.Build >= 22000) // Windows 11
                {
                    int DWMWCP_ROUND = 2;
                    _ = DwmSetWindowAttribute(_contextMenu.Handle, 33, ref DWMWCP_ROUND, sizeof(int));
                }

                foreach (ToolStripItem item in _contextMenu.Items)
                {
                    if (item is ToolStripMenuItem menuItem && menuItem.Tag is TrayMenuActionItem action && action.ShortcutTextProvider != null)
                    {
                        var rawShortcut = action.ShortcutTextProvider() ?? "";
                        menuItem.ShortcutKeyDisplayString = rawShortcut.ToShortcutTitleCase();
                    }
                }
            };

            if (_initialMenu != null)
            {
                foreach (var item in _initialMenu.Items)
                {
                    if (item is TrayMenuSeparatorItem)
                    {
                        _contextMenu.Items.Add(new ToolStripSeparator());
                    }
                    else if (item is TrayMenuActionItem actionItem)
                    {
                        var menuItem = new ToolStripMenuItem(actionItem.Text)
                        {
                            Tag = actionItem,
                            Checked = actionItem.IsChecked
                        };
                        menuItem.Click += (s, e) =>
                        {
                            System.Threading.Tasks.Task.Run(() => actionItem.OnClick?.Invoke());
                        };
                        _contextMenu.Items.Add(menuItem);
                    }
                }
            }

            _notifyIcon = new NotifyIcon
            {
                ContextMenuStrip = _contextMenu,
                Text = Constants.CaptureAppName,
                Visible = true
            };

            if (!string.IsNullOrEmpty(_initialIconPath) && System.IO.File.Exists(_initialIconPath))
            {
                // Especificar SmallIconSize evita el escalado borroso nativo de Windows en la bandeja del sistema.
                _notifyIcon.Icon = new Icon(_initialIconPath, SystemInformation.SmallIconSize);
            }

            _logger.Information("WindowsTrayIconService inicializado (NotifyIcon nativo).");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error al inicializar WindowsTrayIconService.");
        }
        finally
        {
            _ready.Set();
        }

        // Bombea los mensajes de Windows para el NotifyIcon
        Application.Run();

        // Limpieza final cuando sale del loop
        _notifyIcon?.Dispose();
        _contextMenu?.Dispose();
    }

    public void ShowNotification(string title, string message, TrayNotificationType type = TrayNotificationType.Info, int timeoutMs = Constants.NotificationDurationMs)
    {
        if (_disposed) return;

        var icon = type switch
        {
            TrayNotificationType.Warning => ToolTipIcon.Warning,
            TrayNotificationType.Error => ToolTipIcon.Error,
            _ => ToolTipIcon.Info
        };

        void Execute()
        {
            if (_notifyIcon != null && _notifyIcon.Visible)
            {
                _notifyIcon.ShowBalloonTip(timeoutMs, title, message, icon);
            }
        }

        if (_contextMenu != null && _contextMenu.InvokeRequired)
        {
            _contextMenu.BeginInvoke((MethodInvoker)Execute);
        }
        else
        {
            Execute();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_contextMenu != null && _contextMenu.InvokeRequired)
        {
            _contextMenu.Invoke((MethodInvoker)delegate
            {
                Application.ExitThread();
            });
        }
        else
        {
            Application.ExitThread();
        }

        _staThread.Join(1000); // Esperar brevemente a que cierre el hilo
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}

