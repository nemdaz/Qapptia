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
        this.AttachedToVisualTree += (s, e) => UpdateDisplay();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        UpdateDisplay();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void UpdateDisplay()
    {
        if (DataContext is EditorViewModel vm && vm.ActiveTextInputShape != null)
        {
            var sizeBlock = this.FindControl<TextBlock>("SizeTextBlock");
            if (sizeBlock != null)
            {
                sizeBlock.Text = ((int)vm.ActiveTextInputShape.TextSize).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }

    private void IncreaseSize_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is EditorViewModel vm && vm.ActiveTextInputShape != null)
        {
            vm.ActiveTextInputShape.TextSize += 2;
            UpdateDisplay();
            vm.TriggerRedraw();
        }
    }

    private void DecreaseSize_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is EditorViewModel vm && vm.ActiveTextInputShape != null)
        {
            if (vm.ActiveTextInputShape.TextSize > 8)
            {
                vm.ActiveTextInputShape.TextSize -= 2;
                UpdateDisplay();
                vm.TriggerRedraw();
            }
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
            if (vm.ActiveTextInputShape is Qapptia.Editor.Models.VectorShape vectorShape)
            {
                vectorShape.Move(dx, dy);
                vm.CurrentTextBounds = vm.ActiveTextInputShape.TextBounds;
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
