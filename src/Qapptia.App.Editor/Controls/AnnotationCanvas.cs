using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
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

    private readonly DispatcherTimer _caretTimer;

    public AnnotationCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        AddHandler(RequestBringIntoViewEvent, (s, e) => e.Handled = true, Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble);

        _caretTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _caretTimer.Tick += (s, e) =>
        {
            if (ViewModel?.EditingTextShape != null && ViewModel.IsEditingText)
            {
                ViewModel.EditingTextShape.IsCaretVisible = !ViewModel.EditingTextShape.IsCaretVisible;
                InvalidateVisual();
            }
        };
        _caretTimer.Start();
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
        if (ViewModel != null && !ViewModel.IsEditingText)
        {
            _isSelectingText = false;
        }
        UpdateCursor(_lastMousePos);
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

        // Delegar el dibujado de vectores a SkiaSharp (Monomotor puro)
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

        public void Dispose() { }

        public bool Equals(Avalonia.Rendering.SceneGraph.ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => _bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<Avalonia.Skia.ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null) return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            // Renderizar todos los vectores guardados
            foreach (var shape in _shapes)
            {
                shape.RenderSkia(canvas);
            }

            // Renderizar la figura que se está dibujando actualmente en vivo
            _currentShape?.RenderSkia(canvas);
        }
    }

    private bool _isSelectingText;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (ViewModel == null) return;

        var point = e.GetPosition(this);
        _lastMousePos = point;
        _pointerPressedPoint = point;
        _hasDragged = false;
        Focus();

        // 1. Si ya estamos editando texto:
        if (ViewModel.IsEditingText && ViewModel.EditingTextShape != null)
        {
            var activeShape = ViewModel.EditingTextShape;
            // Si el clic es dentro del texto activo, posicionar o seleccionar
            if (activeShape.HitTest(point) != HandleType.None)
            {
                int clickIdx = activeShape.GetCaretIndexFromPoint(point);
                activeShape.CaretIndex = clickIdx;
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    activeShape.SelectionEnd = clickIdx;
                }
                else if (e.ClickCount >= 2)
                {
                    activeShape.SelectAll();
                }
                else
                {
                    activeShape.SelectionStart = clickIdx;
                    activeShape.SelectionEnd = clickIdx;
                    _isSelectingText = true;
                }
                activeShape.IsCaretVisible = true;
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            else
            {
                // Clic fuera: confirmar edición previa
                _interaction = CanvasInteraction.CommittingText;
                _isSelectingText = false;
                ViewModel.CommitTextEditing();
                UpdateCursor(point);
                InvalidateVisual();
                return;
            }
        }

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

        // Selección de texto por arrastre del ratón
        if (_isSelectingText && ViewModel.EditingTextShape is { IsEditing: true } textShapeActive && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            int dragIdx = textShapeActive.GetCaretIndexFromPoint(point);
            textShapeActive.CaretIndex = dragIdx;
            textShapeActive.SelectionEnd = dragIdx;
            textShapeActive.IsCaretVisible = true;
            InvalidateVisual();
            return;
        }

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
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) &&
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
                    // Clic limpio sobre texto existente
                    if (_selectedShape is TextShape textShape && ViewModel.ActiveTool == ToolType.Text)
                    {
                        ViewModel.StartTextEditing(textShape);
                    }
                }
                else if (_hasDragged)
                {
                    // Arrastre completado
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
                break;
        }

        _interaction = CanvasInteraction.None;
        _activeHandle = HandleType.None;
        _isSelectingText = false;

        if (shouldSave)
        {
            ViewModel?.SaveCurrentAnnotations();
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (ViewModel?.EditingTextShape is { IsEditing: true } textShape && !string.IsNullOrEmpty(e.Text))
        {
            textShape.InsertText(e.Text);
            ViewModel.CurrentTextContent = textShape.Text;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (ViewModel?.EditingTextShape is { IsEditing: true } textShape)
        {
            // Atajos de portapapeles
            if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                var top = TopLevel.GetTopLevel(this);
                if (top?.Clipboard != null)
                {
                    var pasteText = await top.Clipboard.TryGetTextAsync();
                    if (!string.IsNullOrEmpty(pasteText))
                    {
                        textShape.InsertText(pasteText);
                        ViewModel.CurrentTextContent = textShape.Text;
                        InvalidateVisual();
                        e.Handled = true;
                    }
                }
                return;
            }

            if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                var top = TopLevel.GetTopLevel(this);
                string textToCopy = textShape.HasSelection ? textShape.SelectedText : textShape.Text;
                if (top?.Clipboard != null && !string.IsNullOrEmpty(textToCopy))
                {
                    await top.Clipboard.SetTextAsync(textToCopy);
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.X && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                var top = TopLevel.GetTopLevel(this);
                string textToCut = textShape.HasSelection ? textShape.SelectedText : textShape.Text;
                if (top?.Clipboard != null && !string.IsNullOrEmpty(textToCut))
                {
                    await top.Clipboard.SetTextAsync(textToCut);
                    if (textShape.HasSelection)
                    {
                        textShape.DeleteBackward();
                    }
                    else
                    {
                        textShape.Text = string.Empty;
                        textShape.CaretIndex = 0;
                    }
                    ViewModel.CurrentTextContent = textShape.Text;
                    InvalidateVisual();
                    e.Handled = true;
                }
                return;
            }

            // Delegar la manipulación de teclas a TextShape
            if (textShape.HandleKeyDown(e.Key, e.KeyModifiers, out bool shouldCommit))
            {
                if (shouldCommit)
                {
                    ViewModel.CommitTextEditing();
                }
                else
                {
                    ViewModel.CurrentTextContent = textShape.Text;
                    InvalidateVisual();
                }
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Delete && _selectedShape != null && ViewModel != null)
        {
            ViewModel.Store.RemoveShape(_selectedShape);
            _selectedShape = null;
            ViewModel.SaveCurrentAnnotations();
            InvalidateVisual();
            e.Handled = true;
        }
    }

    private void UpdateCursor(Point point)
    {
        if (ViewModel?.Store == null) return;

        // 1. Si hay una figura seleccionada (o en edición), delegar en su propio método polimórfico
        if (_selectedShape != null)
        {
            var cursorType = _selectedShape.GetCursorType(point);
            if (cursorType != null)
            {
                Cursor = new Cursor(cursorType.Value);
                return;
            }
        }

        // 2. Hover sobre las demás figuras del lienzo
        for (int i = ViewModel.Store.Shapes.Count - 1; i >= 0; i--)
        {
            var shape = ViewModel.Store.Shapes[i];
            var cursorType = shape.GetCursorType(point);
            if (cursorType != null)
            {
                Cursor = new Cursor(cursorType.Value);
                return;
            }
        }

        // 3. Cursor por defecto según la herramienta de dibujo
        Cursor = GetDefaultCursorForTool();
    }

    private Cursor GetDefaultCursorForTool()
    {
        if (ViewModel == null) return Cursor.Default;
        return ViewModel.ActiveTool switch
        {
            ToolType.Line or ToolType.Arrow or ToolType.Rectangle or ToolType.Ellipse or ToolType.Highlighter => new Cursor(StandardCursorType.Cross),
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
