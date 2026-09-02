using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Qapptia.Editor.Core;
using Qapptia.Editor.Models;
using Qapptia.Editor.Services;
using Qapptia.Editor.Tools;
using Xunit;
using EditorGeometry = Qapptia.Editor.Models.Geometry;

namespace Qapptia.Editor.Tests;

public class ToolHierarchyTests
{
    [Fact]
    public void LineToolCreatesLineShapeWithCorrectDefaults()
    {
        var tool = ShapeFactory.Line;
        Assert.Equal(ToolType.Vector, tool.Type);
        Assert.Equal("Line", tool.Id);
        Assert.Equal(StandardCursorType.Cross, tool.DefaultCursor);

        var shape = tool.CreateShape(new Point(10, 20), Colors.Red);
        Assert.NotNull(shape);
        var line = Assert.IsType<EditorGeometry.LineGeometry>(shape);
        Assert.Equal(new Point(10, 20), line.Start);
        Assert.Equal(new Point(10, 20), line.End);
    }

    [Fact]
    public void RectangleToolShiftConstraintForcesSquare()
    {
        var tool = ShapeFactory.Rectangle;
        var shape = tool.CreateShape(new Point(10, 10), Colors.Green);
        Assert.NotNull(shape);

        // Arrastre con Shift: ancho 100, alto 40 -> debe forzar 100x100
        tool.UpdateDrawing(shape, new Point(110, 50), KeyModifiers.Shift);

        Assert.Equal(10, shape.Start.X);
        Assert.Equal(10, shape.Start.Y);
        Assert.Equal(110, shape.End.X);
        Assert.Equal(110, shape.End.Y);
    }

    [Fact]
    public void EllipseToolShiftConstraintForcesCircle()
    {
        var tool = ShapeFactory.Ellipse;
        var shape = tool.CreateShape(new Point(50, 50), Colors.Blue);
        Assert.NotNull(shape);

        // Arrastre con Shift: ancho 30, alto 80 -> debe forzar 80x80
        tool.UpdateDrawing(shape, new Point(80, 130), KeyModifiers.Shift);

        Assert.Equal(50, shape.Start.X);
        Assert.Equal(50, shape.Start.Y);
        Assert.Equal(130, shape.End.X);
        Assert.Equal(130, shape.End.Y);
    }

    [Fact]
    public void TextWidgetToolCreatesAlignedShape()
    {
        var tool = ShapeFactory.Text;
        Assert.Equal(ToolType.Widget, tool.Type);
        Assert.Equal(StandardCursorType.Ibeam, tool.DefaultCursor);

        var textShape = tool.CreateTextShape(new Point(100, 200), Colors.Yellow, 24f, SkiaSharp.SKTypeface.Default);
        Assert.NotNull(textShape);
        Assert.True(textShape.SupportsTextInput);
        Assert.True(textShape.AutoStartsTextInputOnCreation);
    }

    [Fact]
    public async Task ActionToolExecutesActionsCorrectly()
    {
        bool syncExecuted = false;
        var syncTool = new ActionTool("Sync", "Sync Action", null, () => syncExecuted = true);
        await syncTool.ExecuteAsync();
        Assert.True(syncExecuted);

        bool asyncExecuted = false;
        var asyncTool = new ActionTool("Async", "Async Action", null, () =>
        {
            asyncExecuted = true;
            return Task.CompletedTask;
        });
        await asyncTool.ExecuteAsync();
        Assert.True(asyncExecuted);
    }

    [Fact]
    public void ShapeFactoryStringResolutionWorksCorrectly()
    {
        var shape = ShapeFactory.Create("arrow", new Point(5, 5), Colors.Magenta);
        Assert.NotNull(shape);
        Assert.IsType<EditorGeometry.ArrowGeometry>(shape);
    }
}
