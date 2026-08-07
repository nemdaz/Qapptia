using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Qapptia.Editor.Models;
using Qapptia.App.Editor.ViewModels;

namespace Qapptia.App.Editor.Controls;

public class AnnotationCanvas : Control
{
    public static readonly StyledProperty<EditorViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<AnnotationCanvas, EditorViewModel?>(nameof(ViewModel));

    public EditorViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == ViewModelProperty)
        {
            if (change.OldValue is EditorViewModel oldVm)
            {
                oldVm.ImageLoaded -= OnViewModelImageLoaded;
            }
            
            if (change.NewValue is EditorViewModel newVm)
            {
                newVm.ImageLoaded += OnViewModelImageLoaded;
            }
        }
    }

    private void OnViewModelImageLoaded(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }

    private Point _lastMousePos;
    private VectorShape? _currentDrawingShape;
    private VectorShape? _selectedShape;

    public AnnotationCanvas()
    {
        ClipToBounds = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (ViewModel?.Store == null) return;

        // Dibujar fondo si existe
        if (ViewModel.Store.BackgroundImage != null)
        {
            var rect = new Rect(0, 0, ViewModel.Store.BackgroundImage.Size.Width, ViewModel.Store.BackgroundImage.Size.Height);
            context.DrawImage(ViewModel.Store.BackgroundImage, rect);
        }

        // Delegar el dibujado de vectores a SkiaSharp
        context.Custom(new SkiaCanvasDrawOperation(new Rect(Bounds.Size), ViewModel.Store.Shapes, _currentDrawingShape));
    }

    private class SkiaCanvasDrawOperation : Avalonia.Rendering.SceneGraph.ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly System.Collections.Generic.IEnumerable<VectorShape> _shapes;
        private readonly VectorShape? _currentShape;

        public SkiaCanvasDrawOperation(Rect bounds, System.Collections.Generic.IEnumerable<VectorShape> shapes, VectorShape? currentShape)
        {
            _bounds = bounds;
            _shapes = shapes;
            _currentShape = currentShape;
        }

        public Rect Bounds => _bounds;
        public bool HitTest(Point p) => false;
        public bool Equals(Avalonia.Rendering.SceneGraph.ICustomDrawOperation? other) => false;
        public void Dispose() {}

        public void Render(Avalonia.Media.ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<Avalonia.Skia.ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null) return;
            
            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            canvas.Save();
            
            foreach (var shape in _shapes)
            {
                shape.RenderSkia(canvas);
            }
            
            _currentShape?.RenderSkia(canvas);
            
            canvas.Restore();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (ViewModel == null) return;

        var point = e.GetPosition(this);
        _lastMousePos = point;

        ViewModel.Store.ClearSelection();
        _selectedShape = ViewModel.Store.Shapes.LastOrDefault(s => s.HitTest(point));
        
        if (_selectedShape != null)
        {
            _selectedShape.IsSelected = true;
        }
        else
        {
            _currentDrawingShape = CreateShape(ViewModel.ActiveTool, point, ViewModel.ActiveColor);
        }

        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (ViewModel == null) return;

        var point = e.GetPosition(this);

        if (_currentDrawingShape != null)
        {
            if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift) &&
                (_currentDrawingShape is RectangleShape || _currentDrawingShape is EllipseShape))
            {
                double dx = point.X - _currentDrawingShape.Start.X;
                double dy = point.Y - _currentDrawingShape.Start.Y;
                double size = Math.Max(Math.Abs(dx), Math.Abs(dy));
                
                double signX = dx >= 0 ? 1 : -1;
                double signY = dy >= 0 ? 1 : -1;
                
                _currentDrawingShape.End = new Point(
                    _currentDrawingShape.Start.X + size * signX,
                    _currentDrawingShape.Start.Y + size * signY
                );
            }
            else
            {
                _currentDrawingShape.End = point;
            }
            InvalidateVisual();
        }
        else if (_selectedShape != null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // Mover la forma seleccionada
            double dx = point.X - _lastMousePos.X;
            double dy = point.Y - _lastMousePos.Y;
            
            _selectedShape.Start = new Point(_selectedShape.Start.X + dx, _selectedShape.Start.Y + dy);
            _selectedShape.End = new Point(_selectedShape.End.X + dx, _selectedShape.End.Y + dy);
            
            _lastMousePos = point;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        
        if (_currentDrawingShape != null)
        {
            double dx = _currentDrawingShape.End.X - _currentDrawingShape.Start.X;
            double dy = _currentDrawingShape.End.Y - _currentDrawingShape.Start.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance >= Qapptia.Editor.Core.Constants.DrawMinDistance)
            {
                ViewModel?.Store.AddShape(_currentDrawingShape);
            }
            
            _currentDrawingShape = null;
            InvalidateVisual();
        }
    }

    private static VectorShape? CreateShape(ToolType tool, Point startPos, Color color)
    {
        return tool switch
        {
            ToolType.Rectangle => new RectangleShape { Start = startPos, End = startPos, Color = color },
            ToolType.Ellipse => new EllipseShape { Start = startPos, End = startPos, Color = color },
            ToolType.Line => new LineShape { Start = startPos, End = startPos, Color = color },
            ToolType.Arrow => new ArrowShape { Start = startPos, End = startPos, Color = color },
            _ => null
        };
    }
}
