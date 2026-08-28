using System;
using Avalonia;
using Avalonia.Media;
using FluentAssertions;
using Qapptia.Editor.Models;
using Qapptia.Editor.Services;
using Qapptia.Editor.Tools;
using Xunit;
using EditorGeometry = Qapptia.Editor.Models.Geometry;

namespace Qapptia.Editor.Tests;

public sealed class RotationTests
{
    [Fact]
    public void RotateAroundPointUsesMathematicalSignConvention()
    {
        var line = new EditorGeometry.LineGeometry { Start = new Point(100, 100), End = new Point(200, 100) };

        // Rotación +90° (antihorario) sobre el Start (100,100): el End (200,100) -> (100,200)
        line.Rotate(+90);

        line.Start.Should().Be(new Point(100, 100));
        line.End.X.Should().BeApproximately(100, 0.0001);
        line.End.Y.Should().BeApproximately(200, 0.0001);
    }

    [Fact]
    public void RotateNegativeAngleRotatesClockwise()
    {
        var line = new EditorGeometry.LineGeometry { Start = new Point(100, 100), End = new Point(200, 100) };

        // Rotación -90° (horario) sobre Start (100,100): el End (200,100) -> (100,0)
        line.Rotate(-90);

        line.Start.Should().Be(new Point(100, 100));
        line.End.X.Should().BeApproximately(100, 0.0001);
        line.End.Y.Should().BeApproximately(0, 0.0001);
    }

    [Fact]
    public void RotateAroundCenterKeepsBoundingBoxCenter()
    {
        var rect = new EditorGeometry.RectangleGeometry { Start = new Point(0, 0), End = new Point(100, 50) };

        var centerBefore = rect.BoundingBox.Center;
        rect.RotateAroundCenter(90);

        var centerAfter = rect.BoundingBox.Center;
        centerAfter.X.Should().BeApproximately(centerBefore.X, 0.0001);
        centerAfter.Y.Should().BeApproximately(centerBefore.Y, 0.0001);
    }

    [Fact]
    public void RotateScene90ClockwisePreservesRelativePosition()
    {
        // Imagen de 800x600; una línea en la esquina superior-izquierda.
        var line = new EditorGeometry.LineGeometry { Start = new Point(10, 10), End = new Point(50, 10) };

        RotateTool.RotateScene90Clockwise(new[] { line }, imageHeight: 600);

        // (x, y) -> (600 - y, x)
        line.Start.Should().Be(new Point(590, 10));
        line.End.Should().Be(new Point(590, 50));
    }

    [Fact]
    public void RotateToolMetadataIsExposed()
    {
        var tool = ShapeFactory.Rotate;
        tool.Id.Should().Be("Rotate");
        tool.DisplayName.Should().Be("Rotar");
        tool.Type.Should().Be(ToolType.Action);
        tool.IconData.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RotateFourTimesNinetyDegreesReturnsToOrigin()
    {
        var line = new EditorGeometry.LineGeometry { Start = new Point(100, 100), End = new Point(200, 100) };
        var originalStart = line.Start;
        var originalEnd = line.End;

        line.RotateAroundCenter(90);
        line.RotateAroundCenter(90);
        line.RotateAroundCenter(90);
        line.RotateAroundCenter(90);

        line.Start.X.Should().BeApproximately(originalStart.X, 0.0001);
        line.Start.Y.Should().BeApproximately(originalStart.Y, 0.0001);
        line.End.X.Should().BeApproximately(originalEnd.X, 0.0001);
        line.End.Y.Should().BeApproximately(originalEnd.Y, 0.0001);
    }
}
