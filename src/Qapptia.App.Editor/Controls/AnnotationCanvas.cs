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
        Focusable = true;
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

        // Aplicar Zoom y Paneo aquí en el futuro usando transformaciones
        // var transform = Matrix.CreateScale(ViewModel.ZoomLevel, ViewModel.ZoomLevel);
        // using (context.PushTransform(transform)) { ... }

        // Dibujar vectores
        foreach (var shape in ViewModel.Store.Shapes)
        {
            shape.Render(context);
        }
        
        // Dibujar vector en progreso
        _currentDrawingShape?.Render(context);
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
            _currentDrawingShape.End = point;
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
            ViewModel?.Store.AddShape(_currentDrawingShape);
            _currentDrawingShape = null;
            InvalidateVisual();
        }
    }

    private static VectorShape? CreateShape(ToolType tool, Point startPos, Color color)
    {
        return tool switch
        {
            ToolType.Rectangle => new RectangleShape { Start = startPos, End = startPos, Color = color },
            ToolType.Arrow => new ArrowShape { Start = startPos, End = startPos, Color = color },
            _ => null
        };
    }
}
