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
                oldVm.RequestRedraw -= OnViewModelImageLoaded;
            }
            
            if (change.NewValue is EditorViewModel newVm)
            {
                newVm.ImageLoaded += OnViewModelImageLoaded;
                newVm.RequestRedraw += OnViewModelImageLoaded;
            }
        }
    }

    private void OnViewModelImageLoaded(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }

    private enum CanvasInteraction
    {
        None,
        CommittingText,
        DrawingShape,
        ManipulatingShape,
        CreatingText
    }

    private Point _lastMousePos;
    private Point _pointerPressedPoint;
    private bool _hasDragged;
    private CanvasInteraction _interaction = CanvasInteraction.None;
    private HandleType _activeHandle = HandleType.None;
    private VectorShape? _currentDrawingShape;
    private VectorShape? _selectedShape;

    public AnnotationCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        AddHandler(RequestBringIntoViewEvent, (s, e) => e.Handled = true, Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble);
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

    private sealed class SkiaCanvasDrawOperation : Avalonia.Rendering.SceneGraph.ICustomDrawOperation
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

        // Confirmar texto al hacer clic fuera
        if (ViewModel.IsEditingText)
        {
            _interaction = CanvasInteraction.CommittingText;
            ViewModel.CommitTextEditing();
            InvalidateVisual();
            return;
        }

        var point = e.GetPosition(this);
        _lastMousePos = point;
        _pointerPressedPoint = point;
        _hasDragged = false;
        Focus();

        HandleType hitHandle = HandleType.None;
        VectorShape? hitShape = null;

        for (int i = ViewModel.Store.Shapes.Count - 1; i >= 0; i--)
        {
            var shape = ViewModel.Store.Shapes[i];
            var handle = shape.HitTest(point);
            if (handle != HandleType.None)
            {
                hitShape = shape;
                hitHandle = handle;
                break;
            }
        }

        ViewModel.Store.ClearSelection();
        _selectedShape = hitShape;
        _activeHandle = hitHandle;
        
        if (_selectedShape != null)
        {
            _selectedShape.IsSelected = true;
            // Doble clic sobre texto con cualquier herramienta abre edición inmediata sin cambiar ActiveTool
            if (_selectedShape is TextShape textShape && e.ClickCount >= 2)
            {
                _interaction = CanvasInteraction.None;
                ViewModel.StartTextEditing(textShape);
            }
            else
            {
                _interaction = CanvasInteraction.ManipulatingShape;
            }
        }
        else
        {
            if (ViewModel.ActiveTool == ToolType.Text)
            {
                _interaction = CanvasInteraction.CreatingText;
            }
            else
            {
                _interaction = CanvasInteraction.DrawingShape;
                _currentDrawingShape = CreateShape(ViewModel.ActiveTool, point, ViewModel.ActiveColor);
            }
        }

        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (ViewModel == null) return;

        var point = e.GetPosition(this);

        // Detectar si el usuario comenzó a arrastrar
        if (!_hasDragged && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            double distSq = (point.X - _pointerPressedPoint.X) * (point.X - _pointerPressedPoint.X) +
                            (point.Y - _pointerPressedPoint.Y) * (point.Y - _pointerPressedPoint.Y);
            if (distSq > 9) // Umbral de 3 píxeles
            {
                _hasDragged = true;
            }
        }

        if (_interaction == CanvasInteraction.DrawingShape && _currentDrawingShape != null)
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
        else if (_interaction == CanvasInteraction.ManipulatingShape && _selectedShape != null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            double dx = point.X - _lastMousePos.X;
            double dy = point.Y - _lastMousePos.Y;
            
            if (_activeHandle == HandleType.Body)
            {
                _selectedShape.Start = new Point(_selectedShape.Start.X + dx, _selectedShape.Start.Y + dy);
                _selectedShape.End = new Point(_selectedShape.End.X + dx, _selectedShape.End.Y + dy);
            }
            else if (_activeHandle == HandleType.Start)
            {
                _selectedShape.Start = new Point(_selectedShape.Start.X + dx, _selectedShape.Start.Y + dy);
            }
            else if (_activeHandle == HandleType.End)
            {
                _selectedShape.End = new Point(_selectedShape.End.X + dx, _selectedShape.End.Y + dy);
            }
            else if (_activeHandle != HandleType.None)
            {
                double minX = Math.Min(_selectedShape.Start.X, _selectedShape.End.X);
                double maxX = Math.Max(_selectedShape.Start.X, _selectedShape.End.X);
                double minY = Math.Min(_selectedShape.Start.Y, _selectedShape.End.Y);
                double maxY = Math.Max(_selectedShape.Start.Y, _selectedShape.End.Y);

                bool flipX = false;
                bool flipY = false;

                if (_activeHandle == HandleType.TopLeft) { minX += dx; minY += dy; if (minX > maxX) flipX = true; if (minY > maxY) flipY = true; }
                else if (_activeHandle == HandleType.TopRight) { maxX += dx; minY += dy; if (maxX < minX) flipX = true; if (minY > maxY) flipY = true; }
                else if (_activeHandle == HandleType.BottomLeft) { minX += dx; maxY += dy; if (minX > maxX) flipX = true; if (maxY < minY) flipY = true; }
                else if (_activeHandle == HandleType.BottomRight) { maxX += dx; maxY += dy; if (maxX < minX) flipX = true; if (maxY < minY) flipY = true; }
                else if (_activeHandle == HandleType.TopCenter) { minY += dy; if (minY > maxY) flipY = true; }
                else if (_activeHandle == HandleType.BottomCenter) { maxY += dy; if (maxY < minY) flipY = true; }
                else if (_activeHandle == HandleType.LeftCenter) { minX += dx; if (minX > maxX) flipX = true; }
                else if (_activeHandle == HandleType.RightCenter) { maxX += dx; if (maxX < minX) flipX = true; }
                
                bool startIsMinX = _selectedShape.Start.X <= _selectedShape.End.X;
                bool startIsMinY = _selectedShape.Start.Y <= _selectedShape.End.Y;

                double newMinX = Math.Min(minX, maxX);
                double newMaxX = Math.Max(minX, maxX);
                double newMinY = Math.Min(minY, maxY);
                double newMaxY = Math.Max(minY, maxY);

                _selectedShape.Start = new Point(startIsMinX ? newMinX : newMaxX, startIsMinY ? newMinY : newMaxY);
                _selectedShape.End = new Point(startIsMinX ? newMaxX : newMinX, startIsMinY ? newMaxY : newMinY);

                if (flipX)
                {
                    if (_activeHandle == HandleType.TopLeft) _activeHandle = HandleType.TopRight;
                    else if (_activeHandle == HandleType.TopRight) _activeHandle = HandleType.TopLeft;
                    else if (_activeHandle == HandleType.BottomLeft) _activeHandle = HandleType.BottomRight;
                    else if (_activeHandle == HandleType.BottomRight) _activeHandle = HandleType.BottomLeft;
                    else if (_activeHandle == HandleType.LeftCenter) _activeHandle = HandleType.RightCenter;
                    else if (_activeHandle == HandleType.RightCenter) _activeHandle = HandleType.LeftCenter;
                }

                if (flipY)
                {
                    if (_activeHandle == HandleType.TopLeft) _activeHandle = HandleType.BottomLeft;
                    else if (_activeHandle == HandleType.BottomLeft) _activeHandle = HandleType.TopLeft;
                    else if (_activeHandle == HandleType.TopRight) _activeHandle = HandleType.BottomRight;
                    else if (_activeHandle == HandleType.BottomRight) _activeHandle = HandleType.TopRight;
                    else if (_activeHandle == HandleType.TopCenter) _activeHandle = HandleType.BottomCenter;
                    else if (_activeHandle == HandleType.BottomCenter) _activeHandle = HandleType.TopCenter;
                }
            }
            
            _lastMousePos = point;
            InvalidateVisual();
        }
        else if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            UpdateCursor(point);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        bool shouldSave = false;

        switch (_interaction)
        {
            case CanvasInteraction.DrawingShape:
                if (_currentDrawingShape != null)
                {
                    double dx = _currentDrawingShape.End.X - _currentDrawingShape.Start.X;
                    double dy = _currentDrawingShape.End.Y - _currentDrawingShape.Start.Y;
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    if (distance >= Qapptia.Editor.Core.Constants.DrawMinDistance)
                    {
                        ViewModel?.Store.AddShape(_currentDrawingShape);
                        
                        if (ViewModel != null)
                        {
                            ViewModel.Store.ClearSelection();
                            _selectedShape = _currentDrawingShape;
                            _selectedShape.IsSelected = true;
                            shouldSave = true;
                        }
                    }
                    
                    _currentDrawingShape = null;
                    InvalidateVisual();
                }
                break;

            case CanvasInteraction.ManipulatingShape:
                if (!_hasDragged && ViewModel != null)
                {
                    // Clic limpio sobre forma existente
                    if (_selectedShape is TextShape textShape && ViewModel.ActiveTool == ToolType.Text)
                    {
                        ViewModel.StartTextEditing(textShape);
                    }
                }
                else if (_hasDragged)
                {
                    // Arrastre completado (mover o redimensionar)
                    shouldSave = true;
                }
                break;

            case CanvasInteraction.CreatingText:
                if (!_hasDragged && ViewModel != null)
                {
                    // Clic limpio en área vacía con herramienta Texto
                    var newTextShape = new TextShape 
                    { 
                        Start = _pointerPressedPoint, 
                        End = _pointerPressedPoint, 
                        Color = ViewModel.ActiveColor 
                    };
                    ViewModel.Store.AddShape(newTextShape);
                    newTextShape.IsSelected = true;
                    _selectedShape = newTextShape;
                    ViewModel.StartTextEditing(newTextShape);
                }
                break;

            case CanvasInteraction.CommittingText:
                // El clic fuera solo cerró la edición previa
                break;
        }

        _interaction = CanvasInteraction.None;
        _activeHandle = HandleType.None;

        if (shouldSave)
        {
            ViewModel?.SaveCurrentAnnotations();
        }
    }

    private void UpdateCursor(Point point)
    {
        if (ViewModel?.Store == null) return;

        if (_selectedShape != null)
        {
            var handle = _selectedShape.HitTest(point);
            if (handle != HandleType.None)
            {
                Cursor = GetCursorForHandle(handle);
                return;
            }
        }

        for (int i = ViewModel.Store.Shapes.Count - 1; i >= 0; i--)
        {
            var shape = ViewModel.Store.Shapes[i];
            var handle = shape.HitTest(point);
            if (handle != HandleType.None)
            {
                Cursor = new Cursor(StandardCursorType.SizeAll);
                return;
            }
        }

        Cursor = GetDefaultCursorForTool();
    }

    private Cursor GetDefaultCursorForTool()
    {
        if (ViewModel == null) return Cursor.Default;
        return ViewModel.ActiveTool switch
        {
            ToolType.Text => new Cursor(StandardCursorType.Ibeam),
            ToolType.Line or ToolType.Arrow or ToolType.Rectangle or ToolType.Ellipse or ToolType.Highlighter => new Cursor(StandardCursorType.Cross),
            _ => Cursor.Default
        };
    }

    private static Cursor GetCursorForHandle(HandleType handle)
    {
        return handle switch
        {
            HandleType.Body => new Cursor(StandardCursorType.SizeAll),
            HandleType.Start or HandleType.End => new Cursor(StandardCursorType.Cross),
            HandleType.TopLeft or HandleType.BottomRight => new Cursor(StandardCursorType.TopLeftCorner),
            HandleType.TopRight or HandleType.BottomLeft => new Cursor(StandardCursorType.TopRightCorner),
            HandleType.TopCenter or HandleType.BottomCenter => new Cursor(StandardCursorType.TopSide),
            HandleType.LeftCenter or HandleType.RightCenter => new Cursor(StandardCursorType.LeftSide),
            _ => Cursor.Default
        };
    }

    private static VectorShape? CreateShape(ToolType tool, Point startPos, Color color)
    {
        return tool switch
        {
            ToolType.Rectangle => new RectangleShape { Start = startPos, End = startPos, Color = color },
            ToolType.Ellipse => new EllipseShape { Start = startPos, End = startPos, Color = color },
            ToolType.Highlighter => new HighlighterShape { Start = startPos, End = startPos, Color = color },
            ToolType.Line => new LineShape { Start = startPos, End = startPos, Color = color },
            ToolType.Arrow => new ArrowShape { Start = startPos, End = startPos, Color = color },
            _ => null
        };
    }
}
