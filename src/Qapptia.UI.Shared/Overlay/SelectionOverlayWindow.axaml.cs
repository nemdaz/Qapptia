using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Qapptia.Core.Capture;

namespace Qapptia.UI.Shared.Overlay;

public partial class SelectionOverlayWindow : Window
{
    private bool _isDragging;
    private Point _dragStart;
    private Point _dragEnd;

    private readonly IBrush _dimBrush = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0));
    private readonly IPen _selectionPen = new Pen(Brushes.White, 2);

    private readonly TaskCompletionSource<AreaInfo?> _tcs;

    public SelectionOverlayWindow(TaskCompletionSource<AreaInfo?> tcs)
    {
        _tcs = tcs;
        InitializeComponent();

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        KeyDown += OnKeyDown;
        Closed += (_, _) => _tcs.TrySetResult(null);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        var primary = Screens.Primary;
        if (primary is not null)
        {
            var wa = primary.WorkingArea;
            Position = new PixelPoint(0, 0);
            Width = wa.Width;
            Height = wa.Height;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDragging = true;
        _dragStart = e.GetPosition(this);
        _dragEnd = _dragStart;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;
        _dragEnd = e.GetPosition(this);
        InvalidateVisual();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;

        var x = Math.Min(_dragStart.X, _dragEnd.X);
        var y = Math.Min(_dragStart.Y, _dragEnd.Y);
        var w = Math.Abs(_dragEnd.X - _dragStart.X);
        var h = Math.Abs(_dragEnd.Y - _dragStart.Y);

        if (w > 5 && h > 5)
        {
            var screenPos = this.PointToScreen(new Point(x, y));
            _tcs.TrySetResult(new AreaInfo(screenPos.X, screenPos.Y, (int)w, (int)h));
        }
        else
        {
            _tcs.TrySetResult(null);
        }
        Close();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _tcs.TrySetResult(null);
            Close();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(0, 0, Width, Height);

        if (!_isDragging)
        {
            context.FillRectangle(_dimBrush, bounds);
            return;
        }

        var x = Math.Min(_dragStart.X, _dragEnd.X);
        var y = Math.Min(_dragStart.Y, _dragEnd.Y);
        var w = Math.Abs(_dragEnd.X - _dragStart.X);
        var h = Math.Abs(_dragEnd.Y - _dragStart.Y);

        context.FillRectangle(_dimBrush, new Rect(0, 0, Width, y));
        context.FillRectangle(_dimBrush, new Rect(0, y + h, Width, Height - y - h));
        context.FillRectangle(_dimBrush, new Rect(0, y, x, h));
        context.FillRectangle(_dimBrush, new Rect(x + w, y, Width - x - w, h));

        context.DrawRectangle(_selectionPen, new Rect(x, y, w, h));
    }
}