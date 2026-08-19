using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using Qapptia.Editor.Core;
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
        DrawingShape,
        ManipulatingShape
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

        // 1. Si ya estamos en modo de ingreso de texto activo:
        if (ViewModel.IsEditingText && ViewModel.EditingTextShape != null)
        {
            var activeShape = ViewModel.EditingTextShape;
            var handle = activeShape.HitTest(point);
            if (handle != HandleType.None)
            {
                if (handle == HandleType.LeftCenter || handle == HandleType.RightCenter)
                {
                    // Arrastre en caliente de la maneta de ancho sin salir de la edición
                    _selectedShape = activeShape;
                    _activeHandle = handle;
                    _interaction = CanvasInteraction.ManipulatingShape;
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }

                // Clic en el cuerpo: posicionar cursor o selección de caracteres
                _interaction = CanvasInteraction.None;
                activeShape.OnPointerPressedInTextInput(point, e.KeyModifiers, e.ClickCount, out _isSelectingText);
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            else
            {
                bool wasEmpty = string.IsNullOrWhiteSpace(activeShape.Text);
                _isSelectingText = false;
                ViewModel.CommitTextEditing();
                UpdateCursor(point);

                // Si el texto contenía contenido, el clic fuera solo confirma y cierra la edición actual
                if (!wasEmpty)
                {
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
                // Si estaba vacío, continúa el flujo para crear/abrir el nuevo texto en la nueva posición
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
            // Si la figura es un texto, entrar a edición inmediatamente en 1 solo clic (con sus nodos y caret activos):
            if (_selectedShape.SupportsTextInput && _selectedShape is TextShape textShape)
            {
                ViewModel.StartTextEditing(textShape);
                if (_activeHandle == HandleType.LeftCenter || _activeHandle == HandleType.RightCenter)
                {
                    _interaction = CanvasInteraction.ManipulatingShape;
                }
                else
                {
                    _interaction = CanvasInteraction.None;
                    textShape.OnPointerPressedInTextInput(point, e.KeyModifiers, e.ClickCount, out _isSelectingText);
                }
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            
            _interaction = CanvasInteraction.ManipulatingShape;
        }
        else
        {
            var newShape = ShapeFactory.Create(ViewModel.ActiveTool, point, ViewModel.ActiveColor);
            if (newShape != null)
            {
                if (newShape.AutoStartsTextInputOnCreation)
                {
                    _interaction = CanvasInteraction.None;
                    ViewModel.Store.AddShape(newShape);
                    newShape.IsSelected = true;
                    _selectedShape = newShape;
                    ViewModel.StartTextEditing((TextShape)newShape);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
                else
                {
                    _interaction = CanvasInteraction.DrawingShape;
                    _currentDrawingShape = newShape;
                }
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
            
            _selectedShape.DragHandle(_activeHandle, dx, dy, ref _activeHandle);

            if (_selectedShape is TextShape textShape && ViewModel != null && ViewModel.IsEditingText)
            {
                double left = Math.Min(textShape.Start.X, textShape.End.X);
                double top = Math.Min(textShape.Start.Y, textShape.End.Y);
                ViewModel.CurrentTextBounds = new Rect(left, top - 32, textShape.BoxWidth, 32);
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
                if (_hasDragged)
                {
                    // Arrastre completado
                    shouldSave = true;
                }
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
}
