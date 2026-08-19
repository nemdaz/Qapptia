using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Qapptia.App.Editor.ViewModels;

namespace Qapptia.App.Editor.Controls;

public partial class TextEditorWidget : UserControl
{
    public TextEditorWidget()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void IncreaseSize_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is EditorViewModel vm)
        {
            vm.CurrentTextSize += 2;
        }
    }

    private void DecreaseSize_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is EditorViewModel vm)
        {
            if (vm.CurrentTextSize > 8)
                vm.CurrentTextSize -= 2;
        }
    }

    private bool _isDragging;
    private Point _dragStartPoint;

    private void DragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            if (sender is Control c)
            {
                ToolTip.SetIsOpen(c, false);
            }
            _dragStartPoint = e.GetPosition(this.Parent as Control);
            e.Pointer.Capture(sender as IInputElement);
            e.Handled = true;
        }
    }

    private void DragHandle_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging && DataContext is EditorViewModel vm)
        {
            if (sender is Control c)
            {
                ToolTip.SetIsOpen(c, false);
            }
            var currentPoint = e.GetPosition(this.Parent as Control);
            double dx = currentPoint.X - _dragStartPoint.X;
            double dy = currentPoint.Y - _dragStartPoint.Y;

            // Sincronizar posición del vector y overlay flotante preservando el ancho
            if (vm.EditingTextShape != null)
            {
                vm.EditingTextShape.Move(dx, dy);

                double left = Math.Min(vm.EditingTextShape.Start.X, vm.EditingTextShape.End.X);
                double top = Math.Min(vm.EditingTextShape.Start.Y, vm.EditingTextShape.End.Y);

                vm.CurrentTextBounds = new Rect(
                    left,
                    top - 32,
                    vm.EditingTextShape.BoxWidth,
                    vm.CurrentTextBounds.Height);

                vm.TriggerRedraw();
            }

            _dragStartPoint = currentPoint;
            e.Handled = true;
        }
    }

    private void DragHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }
}
