using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using Qapptia.App.Editor.ViewModels;
using Qapptia.App.Editor.ViewModels.Shapes;
using Qapptia.Editor.Core;
using Qapptia.Editor.Models;
using Qapptia.Editor.Services;
using Qapptia.Editor.Tools;

namespace Qapptia.App.Editor.Controls;

public class BoardCanvas : Control
{
    public static readonly StyledProperty<EditorViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<BoardCanvas, EditorViewModel?>(nameof(ViewModel));

    public EditorViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private readonly DispatcherTimer _caretTimer;

    public BoardCanvas()
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
            if (ViewModel?.ActiveTextInputShape != null && ViewModel.IsEditingText)
            {
                ViewModel.ActiveTextInputShape.IsCaretVisible = !ViewModel.ActiveTextInputShape.IsCaretVisible;
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
                oldVm.TextInputFocusRequested -= OnTextInputFocusRequested;
            }

            if (change.NewValue is EditorViewModel newVm)
            {
                newVm.ImageLoaded += OnViewModelImageLoaded;
                newVm.RequestRedraw += OnViewModelImageLoaded;
                newVm.TextInputFocusRequested += OnTextInputFocusRequested;
            }
        }
    }

    private void OnTextInputFocusRequested(object? sender, EventArgs e)
    {
        this.Focus();
    }

    private void OnViewModelImageLoaded(object? sender, EventArgs e)
    {
        if (ViewModel != null && !ViewModel.IsEditingText)
        {
            _isSelectingText = false;
        }
        _cropPreviewRect = null;
        _cropActiveHandle = HandleType.None;
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
    private Rect? _cropPreviewRect;
    private HandleType _cropActiveHandle = HandleType.None;

    private Rect GetCropRect() => _cropPreviewRect ?? ViewModel?.ActiveCropRect ?? new Rect(0, 0, ViewModel?.ImageWidth ?? 0, ViewModel?.ImageHeight ?? 0);

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (ViewModel == null) return;

        if (ViewModel.BackgroundImage != null)
        {
            var rect = new Rect(0, 0, ViewModel.ImageWidth, ViewModel.ImageHeight);
            context.DrawImage(ViewModel.BackgroundImage, rect);
        }

        bool drawCropOverlay = !ViewModel.IsExporting && (ViewModel.IsCropToolActive || ViewModel.ActiveCropRect.HasValue);
        var cropRectToDraw = drawCropOverlay ? (Rect?)GetCropRect() : null;

        // Delegar el dibujado de vectores y overlay de recorte a SkiaSharp (Monomotor puro)
        context.Custom(new SkiaCanvasDrawOperation(
            new Rect(Bounds.Size),
            ViewModel.Shapes,
            _currentDrawingShape,
            cropRectToDraw,
            ViewModel.IsCropToolActive,
            new Rect(0, 0, ViewModel.ImageWidth, ViewModel.ImageHeight)));
    }

    private sealed class SkiaCanvasDrawOperation : Avalonia.Rendering.SceneGraph.ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly IEnumerable<VectorShape> _shapes;
        private readonly VectorShape? _currentShape;
        private readonly Rect? _cropRect;
        private readonly bool _isCropMode;
        private readonly Rect _imageBounds;

        public SkiaCanvasDrawOperation(
            Rect bounds,
            IEnumerable<VectorShape> shapes,
            VectorShape? currentShape,
            Rect? cropRect = null,
            bool isCropMode = false,
            Rect imageBounds = default)
        {
            _bounds = bounds;
            _shapes = shapes;
            _currentShape = currentShape;
            _cropRect = cropRect;
            _isCropMode = isCropMode;
            _imageBounds = imageBounds;
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

            // Renderizar overlay de recorte interactivo
            if (_cropRect != null && _imageBounds.Width > 0 && _imageBounds.Height > 0)
            {
                ShapeRenderHelper.DrawCropOverlay(canvas, _cropRect.Value, _imageBounds, _isCropMode);
            }
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

        // 0. Modo Recorte Activo:
        if (ViewModel.IsCropToolActive)
        {
            var cropRect = GetCropRect();
            var handle = HitTestEngine.HitTestCrop(point, cropRect);
            if (handle != HandleType.None)
            {
                _cropActiveHandle = handle;
                _interaction = CanvasInteraction.ManipulatingShape;
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            else
            {
                ViewModel.DeactivateCropTool();
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }

        // 1. Si ya estamos en modo de ingreso de texto activo:
        if (ViewModel.IsEditingText && ViewModel.ActiveTextInputShape is VectorShape activeShape && activeShape is ITextInputShape activeInput)
        {
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

                // Clic en el borde perimetral del recuadro: pasar a modo contenedor
                if (activeInput.IsOnBorder(point))
                {
                    ViewModel.CommitCurrentState();
                    activeShape.IsSelected = true;
                    _selectedShape = activeShape;
                    _activeHandle = HandleType.Body;
                    _interaction = CanvasInteraction.ManipulatingShape;
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }

                // Clic en el cuerpo del texto: posicionar cursor o selección de caracteres
                _interaction = CanvasInteraction.None;
                activeInput.OnPointerPressedInTextInput(point, e.KeyModifiers, e.ClickCount, out _isSelectingText);
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            else
            {
                bool wasEmpty = activeInput.IsEmpty;
                _isSelectingText = false;
                ViewModel.CommitCurrentState();
                UpdateCursor(point);

                // Si el texto contenía contenido, el clic fuera solo confirma y cierra la edición actual
                if (!wasEmpty)
                {
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
            }
        }

        HandleType hitHandle = HandleType.None;
        VectorShape? hitShape = null;

        for (int i = ViewModel.Shapes.Count - 1; i >= 0; i--)
        {
            var shape = ViewModel.Shapes[i];
            var handle = shape.HitTest(point);
            if (handle != HandleType.None)
            {
                hitShape = shape;
                hitHandle = handle;
                break;
            }
        }

        ViewModel.ClearSelection();
        _selectedShape = hitShape;
        _activeHandle = hitHandle;

        if (_selectedShape != null)
        {
            _selectedShape.IsSelected = true;
            // Si la figura admite ingreso de texto:
            if (_selectedShape.SupportsTextInput && _selectedShape is ITextInputShape inputShape)
            {
                if (_activeHandle == HandleType.LeftCenter || _activeHandle == HandleType.RightCenter)
                {
                    _interaction = CanvasInteraction.ManipulatingShape;
                }
                else if (inputShape.IsOnBorder(point))
                {
                    // Clic en el borde: seleccionar como contenedor vectorial
                    _interaction = CanvasInteraction.ManipulatingShape;
                }
                else
                {
                    // Clic en el interior del texto: entrar a modo edición de texto
                    ViewModel.StartTextInput(inputShape);
                    _interaction = CanvasInteraction.None;
                    inputShape.OnPointerPressedInTextInput(point, e.KeyModifiers, e.ClickCount, out _isSelectingText);
                }
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            _interaction = CanvasInteraction.ManipulatingShape;
        }
        else
        {
            if (ViewModel.ActiveTool is VectorTool vectorTool)
            {
                var geometry = vectorTool.CreateShape(point, ViewModel.ActiveColor);
                if (geometry != null)
                {
                    _interaction = CanvasInteraction.DrawingShape;
                    _currentDrawingShape = ShapeViewFactory.Wrap(geometry);
                }
            }
            else if (ViewModel.ActiveTool is TextWidgetTool textTool)
            {
                var geometry = textTool.CreateTextShape(point, ViewModel.ActiveColor, ViewModel.ActiveTextSize, null);
                if (geometry != null)
                {
                    var newShape = ShapeViewFactory.Wrap(geometry) as TextShape;
                    if (newShape == null)
                    {
                        return;
                    }
                    _interaction = CanvasInteraction.None;
                    ViewModel.Shapes.Add(newShape);
                    newShape.IsSelected = true;
                    _selectedShape = newShape;
                    ViewModel.StartTextInput(newShape);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
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
        if (_isSelectingText && ViewModel.ActiveTextInputShape is { IsEditing: true } and TextShape textShapeActive && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
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

        if (ViewModel.IsCropToolActive && _cropActiveHandle != HandleType.None && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            double dx = point.X - _lastMousePos.X;
            double dy = point.Y - _lastMousePos.Y;
            var cur = GetCropRect();
            double imgW = ViewModel.ImageWidth;
            double imgH = ViewModel.ImageHeight;

            _cropPreviewRect = CropTool.ResizeRect(_cropActiveHandle, cur, dx, dy, imgW, imgH);

            _lastMousePos = point;
            _hasDragged = true;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_interaction == CanvasInteraction.DrawingShape && _currentDrawingShape != null)
        {
            if (ViewModel.ActiveTool is VectorTool vectorTool)
            {
                vectorTool.UpdateDrawing(_currentDrawingShape.Geometry, point, e.KeyModifiers);
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

            if (_selectedShape is ITextInputShape inputShape && ViewModel != null && ViewModel.IsEditingText)
            {
                ViewModel.CurrentTextBounds = inputShape.TextBounds;
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

        if (ViewModel != null && ViewModel.IsCropToolActive)
        {
            if (_cropActiveHandle != HandleType.None && _hasDragged && _cropPreviewRect != null)
            {
                var finalCrop = _cropPreviewRect.Value;
                if (CropTool.ShouldApplyCrop(finalCrop, ViewModel.ImageWidth, ViewModel.ImageHeight))
                {
                    bool isFullImage = (Math.Abs(finalCrop.Width - ViewModel.ImageWidth) < 1 && Math.Abs(finalCrop.Height - ViewModel.ImageHeight) < 1 && finalCrop.X < 1 && finalCrop.Y < 1);
                    ViewModel.ActiveCropRect = isFullImage ? null : finalCrop;
                }
            }

            _cropActiveHandle = HandleType.None;
            _interaction = CanvasInteraction.None;
            _hasDragged = false;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        switch (_interaction)
        {
            case CanvasInteraction.DrawingShape:
                if (_currentDrawingShape != null)
                {
                    bool shouldCommit = (ViewModel?.ActiveTool is VectorTool vectorTool)
                        ? vectorTool.ShouldCommitOnRelease(_currentDrawingShape.Geometry)
                        : true;

                    if (shouldCommit)
                    {
                        ViewModel?.Shapes.Add(_currentDrawingShape);

                        // Seleccionamos la figura automáticamente para que pueda cambiar de color/editarse de inmediato
                        ViewModel?.ClearSelection();
                        _currentDrawingShape.IsSelected = true;
                        _selectedShape = _currentDrawingShape;

                        shouldSave = true;
                    }
                    _currentDrawingShape = null;
                }
                break;

            case CanvasInteraction.ManipulatingShape:
                if (_hasDragged)
                {
                    shouldSave = true;
                }
                break;
        }

        _interaction = CanvasInteraction.None;
        _activeHandle = HandleType.None;
        _isSelectingText = false;
        InvalidateVisual();

        if (shouldSave)
        {
            ViewModel?.SaveCurrentAnnotations();
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (ViewModel?.ActiveTextInputShape is { IsEditing: true } textInput && !string.IsNullOrEmpty(e.Text))
        {
            textInput.InsertText(e.Text);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (ViewModel?.ActiveTextInputShape is { IsEditing: true } textInput)
        {
            // Atajos de portapapeles
            if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                var top = TopLevel.GetTopLevel(this);
                if (top?.Clipboard != null)
                {
                    string? pasteText = null;
                    if (top.Clipboard is IAsyncDataTransfer dataTransfer)
                    {
                        pasteText = await dataTransfer.TryGetTextAsync();
                    }
                    if (!string.IsNullOrEmpty(pasteText))
                    {
                        textInput.InsertText(pasteText);
                        InvalidateVisual();
                        e.Handled = true;
                    }
                }
                return;
            }

            if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                var top = TopLevel.GetTopLevel(this);
                string textToCopy = textInput.HasSelection ? textInput.SelectedText : textInput.Text;
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
                string textToCut = textInput.HasSelection ? textInput.SelectedText : textInput.Text;
                if (top?.Clipboard != null && !string.IsNullOrEmpty(textToCut))
                {
                    await top.Clipboard.SetTextAsync(textToCut);
                    if (textInput.HasSelection)
                    {
                        textInput.DeleteBackward();
                    }
                    else
                    {
                        textInput.Text = string.Empty;
                        textInput.CaretIndex = 0;
                    }
                    InvalidateVisual();
                    e.Handled = true;
                }
                return;
            }

            // Delegar la manipulación de teclas a ITextInputShape
            if (textInput.HandleKeyDown(e.Key, e.KeyModifiers, out bool shouldCommit))
            {
                if (shouldCommit)
                {
                    if (e.Key == Key.Escape && textInput is VectorShape vs)
                    {
                        // Escape: confirma el texto y transiciona a modo contenedor (IsSelected = true, IsEditing = false)
                        ViewModel.CommitCurrentState();
                        vs.IsSelected = true;
                        _selectedShape = vs;
                        InvalidateVisual();
                    }
                    else
                    {
                        ViewModel.CommitCurrentState();
                    }
                }
                else
                {
                    InvalidateVisual();
                }
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape && ViewModel != null)
        {
            ViewModel.ClearSelection();
            _selectedShape = null;
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && ViewModel != null)
        {
            _selectedShape = null;
            ViewModel.DeleteSelectedCommand.Execute(null);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    private void UpdateCursor(Point point)
    {
        if (ViewModel == null) return;

        // 0. Modo Recorte
        if (ViewModel.IsCropToolActive)
        {
            var cropRect = GetCropRect();
            var handle = HitTestEngine.HitTestCrop(point, cropRect);
            Cursor = new Cursor(HitTestEngine.GetCursorForCropHandle(handle));
            return;
        }

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
        for (int i = ViewModel.Shapes.Count - 1; i >= 0; i--)
        {
            var shape = ViewModel.Shapes[i];
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
        return new Cursor(ViewModel.ActiveTool.DefaultCursor);
    }
}
