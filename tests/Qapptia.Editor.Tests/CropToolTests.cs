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
        tool.IconData.Should().NotBeNullOrWhiteSpace();
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
        var rect = new RectangleShape
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
}
