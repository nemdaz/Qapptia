using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using System.Linq;
using Qapptia.App.Editor.ViewModels;

namespace Qapptia.App.Editor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        var configService = new Qapptia.Core.Configuration.JsonConfigService(Qapptia.Core.AppConstants.DefaultConfigPath);
        var savePath = string.IsNullOrWhiteSpace(configService.Current.SavePath) ? Qapptia.Core.AppConstants.DefaultSavePath : configService.Current.SavePath;
        var stateStore = new Qapptia.Editor.Models.EditorStateStore(
            savePath, 
            Qapptia.Core.AppConstants.EditorStateFileName);
        
        var vm = new EditorViewModel(stateStore, savePath);
        DataContext = vm;
        
        vm.CopyRequested += Vm_CopyRequested;
        vm.RotateRequested += Vm_RotateRequested;

        var copyBinding = new Avalonia.Input.KeyBinding
        {
            Gesture = Avalonia.Input.KeyGesture.Parse(Qapptia.Core.AppConstants.ShortcutCopyClipboard),
            Command = vm.CopyCommand
        };
        this.KeyBindings.Add(copyBinding);

        vm.LoadSidebarImagesCommand.Execute(null);

        bool isInitialSizeSet = false;
        this.SizeChanged += (s, e) =>
        {
            var grid = this.FindControl<Grid>("MainGrid");
            if (grid != null && grid.ColumnDefinitions.Count >= 3)
            {
                var sidebarCol = grid.ColumnDefinitions[2];
                var width = e.NewSize.Width;
                sidebarCol.MinWidth = width * 0.10; // Mínimo 10%
                sidebarCol.MaxWidth = width * 0.50; // Máximo 50%
                
                if (!isInitialSizeSet && width > 0)
                {
                    sidebarCol.Width = new GridLength(width * 0.30);
                    isInitialSizeSet = true;
                }
                else
                {
                    // Si el ancho actual es menor que el mínimo, lo forzamos al mínimo
                    if (sidebarCol.Width.Value < sidebarCol.MinWidth)
                    {
                        sidebarCol.Width = new GridLength(sidebarCol.MinWidth);
                    }
                    // Si es mayor que el máximo, lo forzamos al máximo
                    else if (sidebarCol.Width.Value > sidebarCol.MaxWidth)
                    {
                        sidebarCol.Width = new GridLength(sidebarCol.MaxWidth);
                    }
                }
            }
        };
        
        // Registrar evento de rueda del ratón con Tunnel para interceptarlo antes que el ScrollViewer
        this.AddHandler(Avalonia.Input.InputElement.PointerWheelChangedEvent, EditorScrollViewer_PointerWheelChanged, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        
        var zoomCombo = this.FindControl<ComboBox>("ZoomComboBox");
        if (zoomCombo != null)
        {
            zoomCombo.AddHandler(Avalonia.Input.InputElement.TextInputEvent, (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Text))
                {
                    foreach (char c in e.Text)
                    {
                        if (!char.IsDigit(c) && c != '%')
                        {
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

            // Al presionar Enter, forzamos la actualización
            zoomCombo.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, (s, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Enter && s is ComboBox cb)
                {
                    var tb = cb.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
                    if (tb != null && DataContext is EditorViewModel vm)
                    {
                        vm.SelectedZoomString = tb.Text ?? string.Empty;
                        
                        // Cerrar el combo si está desplegado
                        cb.IsDropDownOpen = false;
                        
                        // Remover el foco para confirmar la selección visualmente
                        this.Focus();
                    }
                }
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }
    }

    private void FitImage_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is EditorViewModel vm && vm.HasImage)
        {
            var scrollViewer = this.FindControl<ScrollViewer>("EditorScrollViewer");
            if (scrollViewer != null && vm.ImageWidth > 0 && vm.ImageHeight > 0)
            {
                // Dejamos un pequeño margen para que no choque con los bordes (por ejemplo, restar 64px del layout)
                double availableWidth = scrollViewer.Bounds.Width - 64;
                double availableHeight = scrollViewer.Bounds.Height - 64;

                if (availableWidth <= 0) availableWidth = 100;
                if (availableHeight <= 0) availableHeight = 100;

                double scaleX = availableWidth / vm.ImageWidth;
                double scaleY = availableHeight / vm.ImageHeight;

                float fitZoom = (float)Math.Min(scaleX, scaleY);
                if (fitZoom <= 0) fitZoom = 0.1f;
                if (fitZoom > 5.0f) fitZoom = 5.0f; // Max 500%
                
                vm.ZoomLevel = fitZoom;
            }
        }
    }

    private void EditorScrollViewer_PointerWheelChanged(object? sender, Avalonia.Input.PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control) && DataContext is EditorViewModel vm)
        {
            float currentZoom = vm.ZoomLevel;
            float step = currentZoom < 1.0f ? 0.1f : 0.25f;
            int direction = e.Delta.Y > 0 ? 1 : -1;

            float newZoom = currentZoom + (step * direction);
            
            // Clamp between min and max (0.1 to 5.0)
            newZoom = Math.Max(0.1f, Math.Min(newZoom, 5.0f));

            if (Math.Abs(newZoom - currentZoom) > 0.01f)
            {
                vm.ZoomLevel = newZoom;
            }

            // Mark as handled to prevent the ScrollViewer from scrolling up/down
            e.Handled = true;
        }
    }

    private async void Vm_CopyRequested(object? sender, EventArgs e)
    {
        if (DataContext is EditorViewModel vm)
        {
            var canvas = this.FindControl<Qapptia.App.Editor.Controls.AnnotationCanvas>("MainCanvas");
            if (canvas != null && vm.ImageWidth > 0 && vm.ImageHeight > 0)
            {
                var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)vm.ImageWidth, (int)vm.ImageHeight));
                rtb.Render(canvas);
                
                using var ms = new System.IO.MemoryStream();
                rtb.Save(ms, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                
#if WINDOWS
                try
                {
                    var clipboardService = new Qapptia.Platform.Windows.WindowsClipboardService(
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<Qapptia.Platform.Windows.WindowsClipboardService>.Instance);
                    
                    await clipboardService.SetImageAsync(ms.ToArray());
                    vm.ShowToast("Imagen copiada al portapapeles", Qapptia.Editor.Models.NotificationType.Success);
                }
                catch (Exception)
                {
                    vm.ShowToast("Error al copiar al portapapeles", Qapptia.Editor.Models.NotificationType.Error);
                }
#endif
            }
        }
    }

    private void Vm_RotateRequested(object? sender, EventArgs e)
    {
        if (DataContext is EditorViewModel vm && vm.Store.BackgroundImage != null)
        {
            var oldBmp = vm.Store.BackgroundImage;
            int w = oldBmp.PixelSize.Width;
            int h = oldBmp.PixelSize.Height;
            
            var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(h, w), new Vector(96, 96));
            
            using (var ctx = rtb.CreateDrawingContext())
            {
                var transform = Matrix.CreateTranslation(0, 0) * Matrix.CreateRotation(Math.PI / 2) * Matrix.CreateTranslation(h, 0);
                
                using (ctx.PushTransform(transform))
                {
                    ctx.DrawImage(oldBmp, new Rect(0, 0, w, h));
                }
            }
            
            vm.Store.SetBackground(rtb);
            vm.ImageWidth = h;
            vm.ImageHeight = w;
            
            foreach (var shape in vm.Store.Shapes)
            {
                var start = shape.Start;
                var end = shape.End;
                shape.Start = new Point(h - start.Y, start.X);
                shape.End = new Point(h - end.Y, end.X);
            }
            
            vm.ShowToast("Imagen rotada 90°", Qapptia.Editor.Models.NotificationType.Info);
        }
    }
}