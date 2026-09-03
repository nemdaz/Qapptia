using Avalonia;
using Qapptia.Editor.Core;
using Qapptia.Editor.Models;
using Qapptia.Editor.Models.Geometry;
using Qapptia.Editor.Services;
using Xunit;

namespace Qapptia.Editor.Tests;

public class HitTestZoomTests
{
    [Fact]
    public void HitTestHandleAtStandardZoomRespectsBaseGripRadius()
    {
        var center = new Point(100, 100);
        float baseHalfSize = (float)Constants.GripSize * 2.0f * 1.3f / 2.0f; // ~7.8 units

        // Punto justo dentro del radio base
        var inside = new Point(100 + baseHalfSize - 0.5, 100);
        // Punto claramente fuera del radio base
        var outside = new Point(100 + baseHalfSize + 2.0, 100);

        Assert.True(HitTestEngine.HitTestHandle(inside, center, 1.0f));
        Assert.False(HitTestEngine.HitTestHandle(outside, center, 1.0f));
    }

    [Fact]
    public void HitTestHandleAtZoom50PercentExpandsCanvasTolerance()
    {
        var center = new Point(100, 100);
        float baseHalfSize = (float)Constants.GripSize * 2.0f * 1.3f / 2.0f; // ~7.8 units

        // A 50% de zoom (Z=0.5), el tamaño en el lienzo se duplica (~15.6 units) para mantener el tamaño en pantalla
        var pointAt150PercentDist = new Point(100 + baseHalfSize * 1.5, 100);

        // A zoom 1.0 queda fuera, pero a zoom 0.5 queda dentro
        Assert.False(HitTestEngine.HitTestHandle(pointAt150PercentDist, center, 1.0f));
        Assert.True(HitTestEngine.HitTestHandle(pointAt150PercentDist, center, 0.5f));
    }

    [Fact]
    public void HitTestHandleAtZoom200PercentContractsCanvasTolerance()
    {
        var center = new Point(100, 100);
        float baseHalfSize = (float)Constants.GripSize * 2.0f * 1.3f / 2.0f; // ~7.8 units

        // A 200% de zoom (Z=2.0), la tolerancia en el lienzo se reduce a la mitad (~3.9 units)
        var pointAt75PercentDist = new Point(100 + baseHalfSize * 0.75, 100);

        // A zoom 1.0 queda dentro, pero a zoom 2.0 queda fuera (para no dispararse desde lejos en pantalla)
        Assert.True(HitTestEngine.HitTestHandle(pointAt75PercentDist, center, 1.0f));
        Assert.False(HitTestEngine.HitTestHandle(pointAt75PercentDist, center, 2.0f));
    }

    [Fact]
    public void RectangleGeometryHitTestCornersPropagatesZoomCorrectly()
    {
        var rectGeom = new RectangleGeometry
        {
            Start = new Point(50, 50),
            End = new Point(200, 200),
            IsSelected = true
        };

        float baseHalfSize = (float)Constants.GripSize * 2.0f * 1.3f / 2.0f;
        var ptNearTopLeft = new Point(50 + baseHalfSize * 1.4, 50);

        // A zoom 1.0 está fuera de la maneta TopLeft (retorna Body por tocar el contorno)
        Assert.NotEqual(HandleType.TopLeft, rectGeom.HitTest(ptNearTopLeft, 1.0f));

        // A zoom 0.5 (alejado) la tolerancia se expande y se detecta TopLeft
        Assert.Equal(HandleType.TopLeft, rectGeom.HitTest(ptNearTopLeft, 0.5f));
    }

    [Fact]
    public void TextGeometryHitTestSidesAndIsOnBorderRespectsZoom()
    {
        var textGeom = new TextGeometry
        {
            Start = new Point(50, 50),
            End = new Point(250, 50),
            Text = "Texto de prueba",
            IsSelected = true
        };

        var box = textGeom.BoundingBox;
        var rightCenter = new Point(box.Right, box.Center.Y);
        float baseHalfSize = (float)Constants.GripSize * 2.0f * 1.3f / 2.0f;

        // Maneta lateral con zoom 0.5
        var ptNearRightHandle = new Point(rightCenter.X + baseHalfSize * 1.5, rightCenter.Y);
        Assert.NotEqual(HandleType.RightCenter, textGeom.HitTest(ptNearRightHandle, 1.0f));
        Assert.Equal(HandleType.RightCenter, textGeom.HitTest(ptNearRightHandle, 0.5f));

        // IsOnBorder: a 8 unidades de distancia del borde
        // Con baseTolerance = 6.0, a zoom 1.0 está fuera, pero a zoom 0.5 (tolerancia efectiva 12.0) está dentro
        var ptJustOutsideBorder = new Point(box.Right + 8.0, box.Center.Y);
        Assert.False(textGeom.IsOnBorder(ptJustOutsideBorder, 1.0f));
        Assert.True(textGeom.IsOnBorder(ptJustOutsideBorder, 0.5f));
    }

    [Fact]
    public void HitTestCropCornersAndPerimeterRespectsZoom()
    {
        var cropRect = new Rect(100, 100, 300, 200);
        float baseHalfSize = (float)Constants.GripSize * 2.0f * 1.3f / 2.0f;

        // Punto cerca de TopLeft
        var ptNearTopLeft = new Point(100 + baseHalfSize * 1.4, 100);
        Assert.NotEqual(HandleType.TopLeft, HitTestEngine.HitTestCrop(ptNearTopLeft, cropRect, 1.0f));
        Assert.Equal(HandleType.TopLeft, HitTestEngine.HitTestCrop(ptNearTopLeft, cropRect, 0.5f));

        // Perímetro (cuerpo de recorte para arrastrar el marco completo)
        // A 9 unidades de la arista superior: con gripSize 6.0 a zoom 1.0 está fuera; a zoom 0.5 (tolerancia 12.0) es Body
        var ptNearTopEdge = new Point(200, 100 - 9.0);
        Assert.Equal(HandleType.None, HitTestEngine.HitTestCrop(ptNearTopEdge, cropRect, 1.0f));
        Assert.Equal(HandleType.Body, HitTestEngine.HitTestCrop(ptNearTopEdge, cropRect, 0.5f));
    }
}
