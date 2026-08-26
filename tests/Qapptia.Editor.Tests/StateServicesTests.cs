using System;
using System.IO;
using Avalonia;
using Avalonia.Media;
using FluentAssertions;
using Qapptia.Editor.Models;
using Qapptia.Editor.Services;
using Xunit;

namespace Qapptia.Editor.Tests;

public sealed class StateServicesTests : IDisposable
{
    private readonly string _testDir;
    private readonly EditorStateService _editorStateService;
    private readonly CanvasStateService _canvasStateService;

    public StateServicesTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Qapptia_StateTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _editorStateService = new EditorStateService(_testDir, "editor_state.json");
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
    public void EditorStateServiceSavesAndLoadsStateCorrectly()
    {
        var state = new EditorState();
        state.Layout.SidebarWidth = 320;
        state.Tools.ActiveTool = "Rectangle";
        state.Palette.ActiveFavoriteColor = "#FF0000";

        _editorStateService.Save(state);

        var loaded = _editorStateService.Load();
        loaded.Layout.SidebarWidth.Should().Be(320);
        loaded.Tools.ActiveTool.Should().Be("Rectangle");
        loaded.Palette.ActiveFavoriteColor.Should().Be("#FF0000");
    }

    [Fact]
    public void CanvasStateServiceSavesAndLoadsAnnotationsCorrectly()
    {
        var imagePath = Path.Combine(_testDir, "test_capture.png");
        File.WriteAllBytes(imagePath, new byte[] { 1, 2, 3 });

        var canvasState = new CanvasState();
        var rect = new RectangleShape
        {
            Start = new Point(10, 10),
            End = new Point(100, 100),
            Color = Colors.Red
        };
        canvasState.AddShape(rect);

        _canvasStateService.SaveAnnotations(canvasState, imagePath);

        var jsonPath = _canvasStateService.GetJsonPath(imagePath);
        jsonPath.Should().NotBeNull();
        File.Exists(jsonPath).Should().BeTrue();

        var loadedCanvasState = new CanvasState();
        _canvasStateService.LoadAnnotations(loadedCanvasState, imagePath);

        loadedCanvasState.Shapes.Should().HaveCount(1);
        var loadedShape = loadedCanvasState.Shapes[0].Should().BeOfType<RectangleShape>().Subject;
        loadedShape.Start.Should().Be(new Point(10, 10));
        loadedShape.End.Should().Be(new Point(100, 100));
    }
}
