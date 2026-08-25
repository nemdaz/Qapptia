using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Qapptia.Core.Capture;

namespace Qapptia.UI.Components.Overlay;

public partial class SelectionOverlayWindow : Window, IDisposable
{
    private bool _isDragging;
    private Point _dragStart;
    private Point _dragEnd;
    private Point _currentPos;

    private readonly IBrush _dimBrush = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0));
    private readonly IPen _selectionPen = new Pen(Brushes.White, 2);

    private readonly TaskCompletionSource<AreaInfo?> _tcs;

    private readonly Avalonia.Media.Imaging.WriteableBitmap? _backgroundImage;

    public SelectionOverlayWindow(TaskCompletionSource<AreaInfo?> tcs, Qapptia.Core.Abstractions.ScreenCaptureResult? frozenScreen = null)
    {
        _tcs = tcs;
        if (frozenScreen != null)
        {
            _backgroundImage = new Avalonia.Media.Imaging.WriteableBitmap(
                new Avalonia.PixelSize(frozenScreen.Width, frozenScreen.Height),
                new Avalonia.Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Unpremul);

            using var fb = _backgroundImage.Lock();
            System.Runtime.InteropServices.Marshal.Copy(frozenScreen.BgraPixels, 0, fb.Address, frozenScreen.BgraPixels.Length);
        }

        InitializeComponent();

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        KeyDown += OnKeyDown;
        Closed += (_, _) => 
        {
            Dispose();
            _tcs.TrySetResult(null);
        };
    }

    public void Dispose()
    {
        _backgroundImage?.Dispose();
        GC.SuppressFinalize(this);
    }

    public SelectionOverlayWindow()
    {
        _tcs = null!; // For XAML previewer only
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        var primary = Screens.Primary;
        if (primary is not null)
        {
            // Usar Bounds en lugar de WorkingArea para cubrir también la barra de tareas
            var bounds = primary.Bounds;
            Position = new PixelPoint(0, 0);
            Width = bounds.Width;
            Height = bounds.Height;
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
        _currentPos = e.GetPosition(this);
        if (_isDragging)
        {
            _dragEnd = _currentPos;
        }
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

        if (_backgroundImage != null)
        {
            context.DrawImage(_backgroundImage, bounds);
        }

        if (!_isDragging)
        {
            context.FillRectangle(_dimBrush, bounds);
        }
        else
        {
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

        // Dibuja la cruz punteada que sigue al cursor.
        var dottedPen = new Pen(Brushes.White, 1, new DashStyle(new double[] { 4, 4 }, 0));
        context.DrawLine(dottedPen, new Point(0, _currentPos.Y), new Point(Width, _currentPos.Y));
        context.DrawLine(dottedPen, new Point(_currentPos.X, 0), new Point(_currentPos.X, Height));

        // Dibuja las coordenadas X, Y del cursor.
        var text = $"X: {(int)_currentPos.X}, Y: {(int)_currentPos.Y}";
        var formattedText = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Arial"),
            14,
            Brushes.White);

        // Ajusta el cuadrante dinámicamente para no tapar el área iluminada.
        double offsetX = 15;
        double offsetY = 15;

        if (_isDragging)
        {
            if (_currentPos.X < _dragStart.X)
                offsetX = -formattedText.Width - 15;
                
            if (_currentPos.Y < _dragStart.Y)
                offsetY = -formattedText.Height - 15;
        }

        context.DrawText(formattedText, new Point(_currentPos.X + offsetX, _currentPos.Y + offsetY));
    }
}