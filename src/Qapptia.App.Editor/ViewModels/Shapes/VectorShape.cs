using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Qapptia.Editor.Models;
using Qapptia.Editor.Models.Geometry;
using SkiaSharp;

namespace Qapptia.App.Editor.ViewModels.Shapes;

/// <summary>
/// Clase base de presentación (front) para figuras vectoriales. Envuelve una
/// <see cref="VectorGeometry"/> (back) y añade el renderizado SkiaSharp. Toda la lógica
/// de cálculo y transformación se delega polimórficamente a la geometría subyacente.
/// </summary>
public abstract class VectorShape
{
    protected VectorShape(VectorGeometry geometry)
    {
        Geometry = geometry ?? throw new System.ArgumentNullException(nameof(geometry));
    }

    /// <summary>
    /// Geometría pura (back) asociada a esta forma renderizable.
    /// </summary>
    public VectorGeometry Geometry { get; }

    public abstract void RenderSkia(SKCanvas canvas, float zoom = 1.0f);

    // Passthroughs — delegan en la geometría para mantener la fuente única de verdad.

    public Guid Id => Geometry.Id;
    public Point Start { get => Geometry.Start; set => Geometry.Start = value; }
    public Point End { get => Geometry.End; set => Geometry.End = value; }
    public Color Color { get => Geometry.Color; set => Geometry.Color = value; }
    public double StrokeWidth { get => Geometry.StrokeWidth; set => Geometry.StrokeWidth = value; }
    public bool IsSelected { get => Geometry.IsSelected; set => Geometry.IsSelected = value; }
    public bool IsBurning { get => Geometry.IsBurning; set => Geometry.IsBurning = value; }
    public bool SupportsTextInput => Geometry.SupportsTextInput;
    public bool AutoStartsTextInputOnCreation => Geometry.AutoStartsTextInputOnCreation;
    public Rect BoundingBox => Geometry.BoundingBox;

    public virtual void Move(double dx, double dy) => Geometry.Move(dx, dy);
    public virtual void DragHandle(HandleType handle, double dx, double dy, ref HandleType activeHandle) => Geometry.DragHandle(handle, dx, dy, ref activeHandle);
    public virtual HandleType HitTest(Point point, float zoom = 1.0f) => Geometry.HitTest(point, zoom);
    public virtual void OnPointerPressedInTextInput(Point point, KeyModifiers modifiers, int clickCount, out bool isSelecting) => Geometry.OnPointerPressedInTextInput(point, modifiers, clickCount, out isSelecting);
    public virtual StandardCursorType? GetCursorType(Point point, float zoom = 1.0f) => Geometry.GetCursorType(point, zoom);

    public void Rotate(double angleDegrees) => Geometry.Rotate(angleDegrees);
    public void RotateAroundPoint(Point pivot, double angleDegrees) => Geometry.RotateAroundPoint(pivot, angleDegrees);
    public void RotateAroundCenter(double angleDegrees) => Geometry.RotateAroundCenter(angleDegrees);
}
