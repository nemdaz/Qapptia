using System;
using System.IO;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using FluentAssertions;
using Qapptia.Editor.Models;
using Qapptia.Editor.Services;
using Qapptia.Editor.Tools;
using Xunit;
using EditorGeometry = Qapptia.Editor.Models.Geometry;

namespace Qapptia.Editor.Tests;

public sealed class CropToolTests : IDisposable
{
    private readonly string _testDir;
    private readonly CanvasStateService _canvasStateService;

    public CropToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Qapptia_CropTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _canvasStateService = new CanvasStateService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void CropToolMetadataIsCorrect()
    {
        var tool = ShapeFactory.Crop;
        tool.Id.Should().Be("Crop");
        tool.DisplayName.Should().Be("Recortar");
        tool.Type.Should().Be(ToolType.Interactive);
        tool.DefaultCursor.Should().Be(StandardCursorType.Cross);
        tool.IconKey.Should().Be("IconCrop");
    }

    [Fact]
    public void HitTestEngineReturnsContextualCropCursors()
    {
        HitTestEngine.GetCursorForCropHandle(HandleType.TopCenter).Should().Be(StandardCursorType.TopSide);
        HitTestEngine.GetCursorForCropHandle(HandleType.BottomCenter).Should().Be(StandardCursorType.BottomSide);
        HitTestEngine.GetCursorForCropHandle(HandleType.LeftCenter).Should().Be(StandardCursorType.LeftSide);
        HitTestEngine.GetCursorForCropHandle(HandleType.RightCenter).Should().Be(StandardCursorType.RightSide);
        HitTestEngine.GetCursorForCropHandle(HandleType.TopLeft).Should().Be(StandardCursorType.TopLeftCorner);
        HitTestEngine.GetCursorForCropHandle(HandleType.TopRight).Should().Be(StandardCursorType.TopRightCorner);
        HitTestEngine.GetCursorForCropHandle(HandleType.BottomLeft).Should().Be(StandardCursorType.BottomLeftCorner);
        HitTestEngine.GetCursorForCropHandle(HandleType.BottomRight).Should().Be(StandardCursorType.BottomRightCorner);
        HitTestEngine.GetCursorForCropHandle(HandleType.Body).Should().Be(StandardCursorType.SizeAll);
    }

    [Fact]
    public void VectorShapeMovePreservesRelativeOffset()
    {
        var rect = new EditorGeometry.RectangleGeometry
        {
            Start = new Point(100, 100),
            End = new Point(200, 200),
            Color = Colors.Blue
        };

        // Desplazamiento por recorte (-50, -50)
        rect.Move(-50, -50);

        rect.Start.Should().Be(new Point(50, 50));
        rect.End.Should().Be(new Point(150, 150));
        rect.BoundingBox.Should().Be(new Rect(50, 50, 100, 100));
    }

    [Fact]
    public void CropResizeRectClampsToImageBounds()
    {
        var tool = ShapeFactory.Crop;
        var rect = new Rect(100, 100, 200, 200);

        // Arrastrar la maneta derecha más allá del límite de la imagen (ancho 250).
        var resized = CropTool.ResizeRect(HandleType.RightCenter, rect, dx: 500, dy: 0, imageWidth: 250, imageHeight: 300);

        resized.Right.Should().Be(250);
    }

    [Fact]
    public void CropResizeRectRespectsMinimumSize()
    {
        var tool = ShapeFactory.Crop;
        var rect = new Rect(100, 100, 200, 200);

        // Colapsar por la izquierda; el ancho no debe caer por debajo del mínimo (10).
        var resized = CropTool.ResizeRect(HandleType.LeftCenter, rect, dx: 1000, dy: 0, imageWidth: 500, imageHeight: 500);

        resized.Width.Should().BeGreaterOrEqualTo(Qapptia.Editor.Core.Constants.CropMinSize);
    }

    [Fact]
    public void CropShouldApplyValidatesMinimumSize()
    {
        var tool = ShapeFactory.Crop;

        CropTool.ShouldApplyCrop(new Rect(0, 0, 5, 5), imageWidth: 1000, imageHeight: 800).Should().BeFalse();
        CropTool.ShouldApplyCrop(new Rect(0, 0, 1000, 800), imageWidth: 1000, imageHeight: 800).Should().BeTrue();
        CropTool.ShouldApplyCrop(new Rect(0, 0, 500, 400), imageWidth: 1000, imageHeight: 800).Should().BeTrue();
    }

    [Fact]
    public void HitTestCropReturnsBodyOnlyOnPerimeter()
    {
        var cropRect = new Rect(100, 100, 200, 200);

        // Centro interior: No debe ser Body (debe ser None para confirmar al hacer clic)
        HitTestEngine.HitTestCrop(new Point(200, 200), cropRect).Should().Be(HandleType.None);

        // Exterior lejano: Debe ser None
        HitTestEngine.HitTestCrop(new Point(50, 50), cropRect).Should().Be(HandleType.None);

        // Perímetro entre esquinas y centros: Debe ser Body (para arrastre perimetral)
        HitTestEngine.HitTestCrop(new Point(150, 100), cropRect).Should().Be(HandleType.Body);
        HitTestEngine.HitTestCrop(new Point(300, 150), cropRect).Should().Be(HandleType.Body);

        // Esquina superior izquierda: Debe ser TopLeft
        HitTestEngine.HitTestCrop(new Point(100, 100), cropRect).Should().Be(HandleType.TopLeft);
    }

    [Fact]
    public void HitTestEngineReturnsArrowCursorForNoneHandle()
    {
        HitTestEngine.GetCursorForCropHandle(HandleType.None).Should().Be(StandardCursorType.Arrow);
    }
}
