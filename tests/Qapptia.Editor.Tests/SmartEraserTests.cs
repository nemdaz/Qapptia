using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Qapptia.App.Editor.Services;
using Qapptia.App.Editor.ViewModels.Shapes;
using Qapptia.Editor.Models;
using Qapptia.Editor.Models.Geometry;
using Qapptia.Editor.Services;
using Qapptia.Editor.Tools;
using Xunit;

namespace Qapptia.Editor.Tests;

public class SmartEraserTests
{
    [Fact]
    public void SmartEraserToolHasCorrectMetadataAndDefaults()
    {
        var tool = ShapeFactory.SmartEraser;
        Assert.Equal("SmartEraser", tool.Id);
        Assert.Equal("Borrador inteligente", tool.DisplayName);
        Assert.Equal("W", tool.Shortcut);
        Assert.Equal(ToolType.Vector, tool.Type);
        Assert.Equal(StandardCursorType.Cross, tool.DefaultCursor);
    }

    [Fact]
    public void SmartEraserToolResolveInitialColorUsesCanvasCallbackOrFallback()
    {
        var tool = ShapeFactory.SmartEraser;
        var startPoint = new Point(50, 60);
        var expectedSampledColor = Color.FromRgb(128, 64, 32);

        // Con callback de muestreo
        var resolvedWithCallback = tool.ResolveInitialColor(
            Colors.Red,
            startPoint,
            p => p == startPoint ? expectedSampledColor : Colors.Black);

        Assert.Equal(expectedSampledColor, resolvedWithCallback);

        // Sin callback de muestreo
        var resolvedFallback = tool.ResolveInitialColor(Colors.Blue, startPoint, null);
        Assert.Equal(Colors.Blue, resolvedFallback);
    }

    [Fact]
    public void SmartEraserToolUpdateDrawingAppliesShiftSquareConstraint()
    {
        var tool = ShapeFactory.SmartEraser;
        var shape = tool.CreateShape(new Point(20, 20), Colors.White);

        // Arrastre normal
        tool.UpdateDrawing(shape, new Point(80, 50), KeyModifiers.None);
        Assert.Equal(new Point(80, 50), shape.End);

        // Arrastre con Shift: dx = 60, dy = 30 -> lado 60x60
        tool.UpdateDrawing(shape, new Point(80, 50), KeyModifiers.Shift);
        Assert.Equal(new Point(80, 80), shape.End);
    }

    [Fact]
    public void SmartEraserGeometryContractAndDefaults()
    {
        // 1. Geometría regular (Rectángulo): contorno hover apagado y sin fondo sólido por defecto
        var rectGeom = new Qapptia.Editor.Models.Geometry.RectangleGeometry();
        Assert.False(rectGeom.ShowsHoverOutline);
        Assert.False(rectGeom.HasSolidBackground);
        Assert.False(rectGeom.IsHovered);

        // 2. Geometría Borrador Inteligente: trazo cero, contorno hover y fondo sólido encendidos
        var smartGeom = new SmartEraserGeometry();
        Assert.Equal(0, smartGeom.StrokeWidth);
        Assert.True(smartGeom.ShowsHoverOutline);
        Assert.True(smartGeom.HasSolidBackground);
        Assert.False(smartGeom.IsHovered);

        // 3. Estado efímero de hover y envoltura en VectorShape
        smartGeom.IsHovered = true;
        Assert.True(smartGeom.IsHovered);

        var shape = ShapeViewFactory.Wrap(smartGeom);
        Assert.True(shape.ShowsHoverOutline);
        Assert.True(shape.HasSolidBackground);
        Assert.True(shape.IsHovered);

        shape.IsHovered = false;
        Assert.False(smartGeom.IsHovered);
    }

    [Fact]
    public void SmartEraserShapeRenderSkiaExecutesCleanlyInAllModes()
    {
        var geom = new SmartEraserGeometry
        {
            Start = new Point(10, 10),
            End = new Point(50, 50),
            Color = Colors.White
        };
        var shape = new SmartEraserShape(geom);

        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(100, 100));
        var canvas = surface.Canvas;

        // 1. Modo normal sin selección
        shape.IsSelected = false;
        shape.IsHovered = false;
        shape.RenderSkia(canvas);

        // 2. Modo Hover (contorno bicolor de alto contraste activo)
        shape.IsHovered = true;
        shape.RenderSkia(canvas);

        // 3. Modo Seleccionado (contorno bicolor + manetas)
        shape.IsSelected = true;
        shape.RenderSkia(canvas);

        // 4. Modo Quemado / Exportación (relleno plano sin contornos ni manetas)
        shape.IsBurning = true;
        shape.RenderSkia(canvas);

        // 5. Bounding box vacío retorna anticipadamente sin error
        geom.Start = new Point(10, 10);
        geom.End = new Point(10, 10);
        shape.RenderSkia(canvas);
    }

    [Fact]
    public void SmartEraserGeometryHitTestWhenUnselectedHitsInteriorToAllowSelection()
    {
        var geometry = new SmartEraserGeometry
        {
            Start = new Point(100, 100),
            End = new Point(200, 200),
            IsSelected = false
        };

        // Centro interior (150, 150) -> Body (permite que el clic en hover seleccione la figura)
        var hitInterior = geometry.HitTest(new Point(150, 150));
        Assert.Equal(HandleType.Body, hitInterior);

        // Borde superior (150, 100) -> Body
        var hitBorder = geometry.HitTest(new Point(150, 100));
        Assert.Equal(HandleType.Body, hitBorder);

        // Fuera de la figura (50, 50) -> None
        var hitOutside = geometry.HitTest(new Point(50, 50));
        Assert.Equal(HandleType.None, hitOutside);

        // Cursor cuando no está seleccionada: Hand si está dentro, null si está fuera
        Assert.Equal(StandardCursorType.Hand, geometry.GetCursorType(new Point(150, 150)));
        Assert.Null(geometry.GetCursorType(new Point(50, 50)));
    }

    [Fact]
    public void SmartEraserGeometryHitTestWhenSelectedHitsCornersAndInterior()
    {
        var geometry = new SmartEraserGeometry
        {
            Start = new Point(100, 100),
            End = new Point(200, 200),
            IsSelected = true
        };

        // Esquinas (manetas)
        Assert.Equal(HandleType.TopLeft, geometry.HitTest(new Point(100, 100)));
        Assert.Equal(HandleType.TopRight, geometry.HitTest(new Point(200, 100)));
        Assert.Equal(HandleType.BottomLeft, geometry.HitTest(new Point(100, 200)));
        Assert.Equal(HandleType.BottomRight, geometry.HitTest(new Point(200, 200)));

        // Centro interior cuando está seleccionada -> Body (permite moverla)
        Assert.Equal(HandleType.Body, geometry.HitTest(new Point(150, 150)));

        // Cursors cuando está seleccionada: esquinas retornan resize, centro retorna SizeAll
        Assert.Equal(StandardCursorType.TopLeftCorner, geometry.GetCursorType(new Point(100, 100)));
        Assert.Equal(StandardCursorType.TopRightCorner, geometry.GetCursorType(new Point(200, 100)));
        Assert.Equal(StandardCursorType.SizeAll, geometry.GetCursorType(new Point(150, 150)));
    }

    [Fact]
    public void SmartEraserGeometryDragHandleAndMove()
    {
        var geometry = new SmartEraserGeometry
        {
            Start = new Point(10, 10),
            End = new Point(50, 50)
        };

        // Mover cuerpo
        geometry.Move(5, 10);
        Assert.Equal(new Point(15, 20), geometry.Start);
        Assert.Equal(new Point(55, 60), geometry.End);

        // Arrastrar maneta BottomRight
        var activeHandle = HandleType.BottomRight;
        geometry.DragHandle(HandleType.BottomRight, 10, 20, ref activeHandle);
        Assert.Equal(new Point(15, 20), geometry.Start);
        Assert.Equal(new Point(65, 80), geometry.End);
    }

    [Fact]
    public void ImageColorSamplerFallbackAndReusingColor()
    {
        // Null o imagen inválida retorna White
        var fallbackColor = ImageColorSampler.SampleColor(null, null, new Point(10, 10), 800, 600);
        Assert.Equal(Colors.White, fallbackColor);

        // Reutilización de color si cae sobre un SmartEraserShape previo
        var eraserGeometry = new SmartEraserGeometry
        {
            Start = new Point(10, 10),
            End = new Point(100, 100),
            Color = Color.FromRgb(240, 240, 240)
        };
        var eraserShape = new SmartEraserShape(eraserGeometry);
        var shapes = new List<VectorShape> { eraserShape };

        var reusedColor = ImageColorSampler.SampleColor(null, shapes, new Point(50, 50), 800, 600);
        Assert.Equal(Color.FromRgb(240, 240, 240), reusedColor);
    }

    [Fact]
    public void CanvasStateServiceSerializationAndDeserializationSmartEraser()
    {
        var service = new CanvasStateService();

        var original = new SmartEraserGeometry
        {
            Start = new Point(30, 40),
            End = new Point(150, 90),
            Color = Color.FromRgb(255, 248, 220)
        };

        // CreateDtos
        var dtos = service.CreateDtos(new[] { original });
        Assert.Single(dtos);
        var dto = dtos[0];
        Assert.Equal("smart_eraser", dto.Type);
        Assert.Equal(4, dto.Coords.Count);
        Assert.Equal(30, dto.Coords[0]);
        Assert.Equal(40, dto.Coords[1]);
        Assert.Equal(150, dto.Coords[2]);
        Assert.Equal(90, dto.Coords[3]);

        // CreateShapes
        var reconstructedList = service.CreateShapes(dtos);
        Assert.Single(reconstructedList);
        var reconstructed = Assert.IsType<SmartEraserGeometry>(reconstructedList[0]);
        Assert.Equal(new Point(30, 40), reconstructed.Start);
        Assert.Equal(new Point(150, 90), reconstructed.End);
        Assert.Equal(Color.FromRgb(255, 248, 220), reconstructed.Color);
    }

    [Fact]
    public void ShapeFactoryAndShapeViewFactoryResolveSmartEraser()
    {
        // ShapeFactory por ID y por Tool
        var geomFromId = ShapeFactory.Create("smarteraser", new Point(0, 0), Colors.White);
        Assert.NotNull(geomFromId);
        var smartGeom = Assert.IsType<SmartEraserGeometry>(geomFromId);

        var geomFromTool = ShapeFactory.Create(ShapeFactory.SmartEraser, new Point(5, 5), Colors.Gray);
        Assert.NotNull(geomFromTool);
        Assert.IsType<SmartEraserGeometry>(geomFromTool);

        // ShapeViewFactory Wrap
        var shape = ShapeViewFactory.Wrap(smartGeom);
        Assert.NotNull(shape);
        Assert.IsType<SmartEraserShape>(shape);
    }
}
