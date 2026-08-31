using System;
using System.IO;
using Avalonia;
using Avalonia.Media;
using FluentAssertions;
using Qapptia.App.Editor.ViewModels;
using Qapptia.App.Editor.ViewModels.Shapes;
using Qapptia.Editor.Models;
using Qapptia.Editor.Services;
using Xunit;

namespace Qapptia.Editor.Tests.ViewModels;

public sealed class CanvasBoardViewModelTests : IDisposable
{
    private readonly string _testDir;
    private readonly EditorStateService _stateService;
    private readonly CanvasStateService _canvasStateService;

    public CanvasBoardViewModelTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Qapptia_BoardTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _stateService = new EditorStateService(_testDir, "state.json");
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
    public void CanvasBoardViewModelInitialStateIsEmpty()
    {
        var vm = new CanvasBoardViewModel(_canvasStateService, _stateService);

        vm.HasImage.Should().BeFalse();
        vm.HasNoImage.Should().BeTrue();
        vm.Shapes.Should().BeEmpty();
        vm.BackgroundImage.Should().BeNull();
        vm.ActiveCropRect.Should().BeNull();
    }

    [Fact]
    public void CanvasBoardViewModelClearSelectionDeselectsAllShapes()
    {
        var vm = new CanvasBoardViewModel(_canvasStateService, _stateService);
        var shape1 = new ArrowShape { Start = new Point(0, 0), End = new Point(10, 10), Color = Colors.Red, IsSelected = true };
        var shape2 = new RectangleShape { Start = new Point(5, 5), End = new Point(20, 20), Color = Colors.Blue, IsSelected = true };

        vm.Shapes.Add(shape1);
        vm.Shapes.Add(shape2);

        vm.ClearSelection();

        shape1.IsSelected.Should().BeFalse();
        shape2.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void CanvasBoardViewModelSetBurningModeSetsFlagOnAllShapes()
    {
        var vm = new CanvasBoardViewModel(_canvasStateService, _stateService);
        var shape = new LineShape { Start = new Point(0, 0), End = new Point(10, 10), Color = Colors.Green };
        vm.Shapes.Add(shape);

        vm.SetBurningMode(true);
        shape.IsBurning.Should().BeTrue();

        vm.SetBurningMode(false);
        shape.IsBurning.Should().BeFalse();
    }

    [Fact]
    public void CanvasBoardViewModelDeleteSelectedRemovesSelectedShapes()
    {
        var vm = new CanvasBoardViewModel(_canvasStateService, _stateService);
        var shape1 = new ArrowShape { Start = new Point(0, 0), End = new Point(10, 10), Color = Colors.Red, IsSelected = true };
        var shape2 = new RectangleShape { Start = new Point(5, 5), End = new Point(20, 20), Color = Colors.Blue, IsSelected = false };

        vm.Shapes.Add(shape1);
        vm.Shapes.Add(shape2);

        vm.DeleteSelected();

        vm.Shapes.Should().HaveCount(1);
        vm.Shapes.Should().Contain(shape2);
        vm.Shapes.Should().NotContain(shape1);
    }

    [Fact]
    public void CanvasBoardViewModelClearImageResetsAllProperties()
    {
        var vm = new CanvasBoardViewModel(_canvasStateService, _stateService);
        vm.Shapes.Add(new LineShape { Start = new Point(0, 0), End = new Point(10, 10), Color = Colors.Green });
        vm.ActiveCropRect = new Rect(0, 0, 100, 100);

        vm.ClearImage();

        vm.HasImage.Should().BeFalse();
        vm.HasNoImage.Should().BeTrue();
        vm.Shapes.Should().BeEmpty();
        vm.ActiveCropRect.Should().BeNull();
        vm.BackgroundImage.Should().BeNull();
    }
}
