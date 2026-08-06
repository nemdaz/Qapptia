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
        var vm = new EditorViewModel();
        DataContext = vm;
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
}