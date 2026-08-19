using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Qapptia.Editor.Models;

public abstract class VectorShape
{
    public Guid Id { get; } = Guid.NewGuid();
    
    public Point Start { get; set; }
    public Point End { get; set; }
    
    public Color Color { get; set; } = Qapptia.Editor.Core.Constants.FavoriteColors[1];
    public double StrokeWidth { get; set; } = Qapptia.Editor.Core.Constants.DefaultStrokeWidth;
    
    // Indica si el vector está seleccionado
    public bool IsSelected { get; set; }

    // Indica si el vector se está quemando en la imagen (para ajustar sombras)
    public bool IsBurning { get; set; }

    /// <summary>
    /// Indica si la figura admite ingreso y edición de texto por teclado.
    /// </summary>
    public virtual bool SupportsTextInput => false;

    /// <summary>
    /// Indica si la figura inicia la entrada de texto inmediatamente tras su creación.
    /// </summary>
    public virtual bool AutoStartsTextInputOnCreation => false;

    public abstract void RenderSkia(SkiaSharp.SKCanvas canvas);
    public abstract HandleType HitTest(Point point);

    /// <summary>
    /// Maneja eventos de puntero cuando la figura está en modo de ingreso de texto activo.
    /// </summary>
    public virtual void OnPointerPressedInTextInput(Point point, KeyModifiers modifiers, int clickCount, out bool isSelecting)
    {
        isSelecting = false;
    }

    public virtual StandardCursorType? GetCursorType(Point point)
    {
        var handle = HitTest(point);
        if (handle == HandleType.None) return null;

        return handle switch
        {
            HandleType.Body => StandardCursorType.SizeAll,
            HandleType.Start or HandleType.End => StandardCursorType.Cross,
            HandleType.TopLeft or HandleType.BottomRight => StandardCursorType.TopLeftCorner,
            HandleType.TopRight or HandleType.BottomLeft => StandardCursorType.TopRightCorner,
            HandleType.TopCenter or HandleType.BottomCenter => StandardCursorType.TopSide,
            HandleType.LeftCenter or HandleType.RightCenter => StandardCursorType.LeftSide,
            _ => StandardCursorType.Arrow
        };
    }

    protected virtual Rect GetBoundingBox()
    {
        double left = Math.Min(Start.X, End.X);
        double right = Math.Max(Start.X, End.X);
        double top = Math.Min(Start.Y, End.Y);
        double bottom = Math.Max(Start.Y, End.Y);
        return new Rect(left, top, right - left, bottom - top);
    }
}
