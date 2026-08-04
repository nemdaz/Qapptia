using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Qapptia.Core.Abstractions;

namespace Qapptia.Platform.Windows;

public sealed class WindowsTrayIconService : ITrayIconService
{
    private readonly ILogger<WindowsTrayIconService> _logger;
    private readonly Thread _staThread;
    private readonly ManualResetEventSlim _ready = new();
    
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private bool _disposed;
    
    // Almacenamos la definición de inicio temporalmente hasta que arranque el hilo.
    private TrayMenuDefinition? _initialMenu;
    private string? _initialIconPath;

    public WindowsTrayIconService(ILogger<WindowsTrayIconService> logger)
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
            
            _contextMenu = new ContextMenuStrip();
            
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
                        var menuItem = new ToolStripMenuItem(actionItem.Text);
                        menuItem.Click += (s, e) => 
                        {
                            // WinForms corre en su propio hilo, ejecutamos el action en el ThreadPool
                            // para no bloquear el UI del menu
                            System.Threading.Tasks.Task.Run(() => actionItem.OnClick?.Invoke());
                        };
                        _contextMenu.Items.Add(menuItem);
                    }
                }
            }

            _notifyIcon = new NotifyIcon
            {
                ContextMenuStrip = _contextMenu,
                Text = "Qapptia Screenshot",
                Visible = true
            };

            if (!string.IsNullOrEmpty(_initialIconPath) && System.IO.File.Exists(_initialIconPath))
            {
                _notifyIcon.Icon = new Icon(_initialIconPath);
            }
            
            _logger.LogInformation("WindowsTrayIconService inicializado (NotifyIcon nativo).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inicializar WindowsTrayIconService.");
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
}
