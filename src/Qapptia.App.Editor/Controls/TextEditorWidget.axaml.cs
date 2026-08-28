using System.Linq;
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && this.IsVisible)
        {
            UpdateDisplay();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void UpdateDisplay()
    {
        if (DataContext is EditorViewModel vm && vm.ActiveTextInputShape != null)
        {
            var sizeBox = this.FindControl<TextBox>("SizeTextBox");
            if (sizeBox != null && !sizeBox.IsFocused)
            {
                sizeBox.Text = ((int)vm.ActiveTextInputShape.TextSize).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }

    private void ExecuteOnActiveShape(Action<Qapptia.Editor.Models.ITextInputShape> action)
    {
        if (DataContext is EditorViewModel vm && vm.ActiveTextInputShape != null)
        {
            action(vm.ActiveTextInputShape);
            UpdateDisplay();
            vm.TriggerRedraw();
        }
    }

    private void SizeTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb)
        {
            if (int.TryParse(tb.Text, out int size))
            {
                int min = (int)Qapptia.Editor.Core.Constants.TextToolMinFontSize;
                if (size < min) size = min;
                ExecuteOnActiveShape(shape =>
                {
                    shape.TextSize = size;
                    shape.RequestFocus();
                });
                tb.Text = size.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            e.Handled = true;
        }
    }

    private void SizeTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && !string.IsNullOrEmpty(tb.Text))
        {
            var cleanText = new string(tb.Text.Where(char.IsDigit).ToArray());
            if (cleanText != tb.Text)
            {
                tb.Text = cleanText;
                tb.CaretIndex = cleanText.Length;
            }
            if (int.TryParse(cleanText, out int val))
            {
                int max = (int)Qapptia.Editor.Core.Constants.TextToolMaxFontSize;
                if (val > max)
                {
                    tb.Text = max.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    tb.CaretIndex = tb.Text.Length;
                }
            }
        }
    }

    private void SizeTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            if (int.TryParse(tb.Text, out int size))
            {
                int min = (int)Qapptia.Editor.Core.Constants.TextToolMinFontSize;
                if (size < min)
                {
                    tb.Text = min.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    ExecuteOnActiveShape(shape => shape.TextSize = min);
                }
                else
                {
                    ExecuteOnActiveShape(shape => shape.TextSize = size);
                }
            }
            else
            {
                if (DataContext is EditorViewModel vm && vm.ActiveTextInputShape != null)
                {
                    tb.Text = ((int)vm.ActiveTextInputShape.TextSize).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }
    }

    private void IncreaseSize_Click(object? sender, RoutedEventArgs e)
    {
        ExecuteOnActiveShape(shape =>
        {
            shape.TextSize += 2;
            shape.RequestFocus();
        });
    }

    private void DecreaseSize_Click(object? sender, RoutedEventArgs e)
    {
        ExecuteOnActiveShape(shape =>
        {
            shape.TextSize -= 2;
            shape.RequestFocus();
        });
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
            if (vm.ActiveTextInputShape is Qapptia.App.Editor.ViewModels.Shapes.VectorShape vectorShape)
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
