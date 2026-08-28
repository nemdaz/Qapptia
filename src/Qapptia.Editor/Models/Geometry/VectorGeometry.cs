using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Qapptia.Editor.Models.Geometry;

/// <summary>
/// Entidad geométrica pura (back). Responsable exclusivamente del estado y de los
/// cálculos matemáticos (geometría, hit-testing y transformaciones) de una figura
/// vectorial. No realiza ningún renderizado; el dibujado vive en la capa de presentación.
/// </summary>
public abstract class VectorGeometry
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

    public abstract HandleType HitTest(Point point);

    /// <summary>
    /// Traslada la figura en el lienzo por un diferencial (dx, dy).
    /// </summary>
    public virtual void Move(double dx, double dy)
    {
        Start = new Point(Start.X + dx, Start.Y + dy);
        End = new Point(End.X + dx, End.Y + dy);
    }

    /// <summary>
    /// Aplica una transformación por arrastre sobre una maneta de control seleccionada.
    /// </summary>
    public virtual void DragHandle(HandleType handle, double dx, double dy, ref HandleType activeHandle)
    {
        if (handle == HandleType.Body)
        {
            Move(dx, dy);
            return;
        }

        if (handle == HandleType.Start)
        {
            Start = new Point(Start.X + dx, Start.Y + dy);
            return;
        }

        if (handle == HandleType.End)
        {
            End = new Point(End.X + dx, End.Y + dy);
            return;
        }

        // Transformación rectangular 2D estándar
        double minX = Math.Min(Start.X, End.X);
        double maxX = Math.Max(Start.X, End.X);
        double minY = Math.Min(Start.Y, End.Y);
        double maxY = Math.Max(Start.Y, End.Y);

        bool flipX = false;
        bool flipY = false;

        if (handle == HandleType.TopLeft)
        { minX += dx; minY += dy; if (minX > maxX) flipX = true; if (minY > maxY) flipY = true; }
        else if (handle == HandleType.TopRight)
        { maxX += dx; minY += dy; if (maxX < minX) flipX = true; if (minY > maxY) flipY = true; }
        else if (handle == HandleType.BottomLeft)
        { minX += dx; maxY += dy; if (minX > maxX) flipX = true; if (maxY < minY) flipY = true; }
        else if (handle == HandleType.BottomRight)
        { maxX += dx; maxY += dy; if (maxX < minX) flipX = true; if (maxY < minY) flipY = true; }
        else if (handle == HandleType.TopCenter)
        { minY += dy; if (minY > maxY) flipY = true; }
        else if (handle == HandleType.BottomCenter)
        { maxY += dy; if (maxY < minY) flipY = true; }
        else if (handle == HandleType.LeftCenter)
        { minX += dx; if (minX > maxX) flipX = true; }
        else if (handle == HandleType.RightCenter)
        { maxX += dx; if (maxX < minX) flipX = true; }

        bool startIsMinX = Start.X <= End.X;
        bool startIsMinY = Start.Y <= End.Y;

        double newMinX = Math.Min(minX, maxX);
        double newMaxX = Math.Max(minX, maxX);
        double newMinY = Math.Min(minY, maxY);
        double newMaxY = Math.Max(minY, maxY);

        Start = new Point(startIsMinX ? newMinX : newMaxX, startIsMinY ? newMinY : newMaxY);
        End = new Point(startIsMinX ? newMaxX : newMinX, startIsMinY ? newMaxY : newMinY);

        if (flipX)
        {
            if (activeHandle == HandleType.TopLeft) activeHandle = HandleType.TopRight;
            else if (activeHandle == HandleType.TopRight)
                activeHandle = HandleType.TopLeft;
            else if (activeHandle == HandleType.BottomLeft)
                activeHandle = HandleType.BottomRight;
            else if (activeHandle == HandleType.BottomRight)
                activeHandle = HandleType.BottomLeft;
            else if (activeHandle == HandleType.LeftCenter)
                activeHandle = HandleType.RightCenter;
            else if (activeHandle == HandleType.RightCenter)
                activeHandle = HandleType.LeftCenter;
        }

        if (flipY)
        {
            if (activeHandle == HandleType.TopLeft) activeHandle = HandleType.BottomLeft;
            else if (activeHandle == HandleType.BottomLeft)
                activeHandle = HandleType.TopLeft;
            else if (activeHandle == HandleType.TopRight)
                activeHandle = HandleType.BottomRight;
            else if (activeHandle == HandleType.BottomRight)
                activeHandle = HandleType.TopRight;
            else if (activeHandle == HandleType.TopCenter)
                activeHandle = HandleType.BottomCenter;
            else if (activeHandle == HandleType.BottomCenter)
                activeHandle = HandleType.TopCenter;
        }
    }

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

    public virtual Rect BoundingBox => GetBoundingBox();

    protected virtual Rect GetBoundingBox()
    {
        double left = Math.Min(Start.X, End.X);
        double right = Math.Max(Start.X, End.X);
        double top = Math.Min(Start.Y, End.Y);
        double bottom = Math.Max(Start.Y, End.Y);
        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Rota la figura alrededor del punto de partida (<see cref="Start"/>), que representa
    /// la coordenada de posición actual persistida y requiere el menor cómputo.
    /// </summary>
    public void Rotate(double angleDegrees) => RotateAroundPoint(Start, angleDegrees);

    /// <summary>
    /// Rota la figura alrededor de un pivote arbitrario. Ángulo matemático (positivo = antihorario).
    /// </summary>
    public void RotateAroundPoint(Point pivot, double angleDegrees)
    {
        double rad = angleDegrees * Math.PI / 180.0;
        double cos = Math.Cos(rad);
        double sin = Math.Sin(rad);

        Start = RotatePoint(Start, pivot, cos, sin);
        End = RotatePoint(End, pivot, cos, sin);
    }

    /// <summary>
    /// Rota la figura alrededor del centro de su cuadro delimitador.
    /// </summary>
    public void RotateAroundCenter(double angleDegrees) => RotateAroundPoint(BoundingBox.Center, angleDegrees);

    private static Point RotatePoint(Point pt, Point pivot, double cos, double sin)
    {
        double dx = pt.X - pivot.X;
        double dy = pt.Y - pivot.Y;
        return new Point(pivot.X + dx * cos - dy * sin, pivot.Y + dx * sin + dy * cos);
    }
}
