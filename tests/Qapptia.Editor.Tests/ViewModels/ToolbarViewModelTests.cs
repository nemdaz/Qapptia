using System;
using System.IO;
using Avalonia.Media;
using FluentAssertions;
using Qapptia.App.Editor.ViewModels;
using Qapptia.Editor.Models;
using Qapptia.Editor.Services;
using Qapptia.Editor.Tools;
using Xunit;

namespace Qapptia.Editor.Tests.ViewModels;

public sealed class ToolbarViewModelTests : IDisposable
{
    private readonly string _testDir;
    private readonly EditorStateService _stateService;

    public ToolbarViewModelTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Qapptia_ToolbarTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _stateService = new EditorStateService(_testDir, "state.json");
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
    public void ToolbarViewModelInitializesFromStateService()
    {
        var state = new EditorState();
        state.Tools.ActiveTool = "Rectangle";
        _stateService.Save(state);

        var vm = new ToolbarViewModel(_stateService);

        vm.ActiveTool.Should().BeOfType<RectangleTool>();
        vm.IsRectangleToolActive.Should().BeTrue();
        vm.IsArrowToolActive.Should().BeFalse();
        vm.AvailableColors.Should().NotBeEmpty();
    }

    [Fact]
    public void ToolbarViewModelSelectToolUpdatesPropertiesAndRaisesEvent()
    {
        var vm = new ToolbarViewModel(_stateService);
        Tool? raisedTool = null;
        vm.ToolChanged += (s, tool) => raisedTool = tool;

        vm.SelectTool(ShapeFactory.Ellipse);

        vm.ActiveTool.Should().BeOfType<EllipseTool>();
        vm.IsEllipseToolActive.Should().BeTrue();
        vm.IsArrowToolActive.Should().BeFalse();
        raisedTool.Should().Be(ShapeFactory.Ellipse);

        var savedState = _stateService.Load();
        savedState.Tools.ActiveTool.Should().Be("Ellipse");
    }

    [Fact]
    public void ToolbarViewModelSelectToolByNameFindsAndSelectsTool()
    {
        var vm = new ToolbarViewModel(_stateService);

        vm.SelectTool("Line");

        vm.ActiveTool.Should().BeOfType<LineTool>();
        vm.IsLineToolActive.Should().BeTrue();
    }

    [Fact]
    public void ToolbarViewModelCropToolToggleRestoresPreviousTool()
    {
        var vm = new ToolbarViewModel(_stateService);
        vm.SelectTool(ShapeFactory.Highlighter);
        vm.IsHighlighterToolActive.Should().BeTrue();

        vm.SelectTool(ShapeFactory.Crop);
        vm.IsCropToolActive.Should().BeTrue();

        // Pulsar Crop de nuevo debe restaurar Highlighter
        vm.SelectTool(ShapeFactory.Crop);
        vm.IsHighlighterToolActive.Should().BeTrue();
    }

    [Fact]
    public void ToolbarViewModelSelectColorUpdatesColorAndPersistsInState()
    {
        var vm = new ToolbarViewModel(_stateService);
        Color? raisedColor = null;
        vm.ColorChanged += (s, color) => raisedColor = color;

        var targetItem = vm.AvailableColors[1];
        vm.SelectColor(targetItem);

        vm.ActiveColor.Should().Be(targetItem.Color);
        targetItem.IsSelected.Should().BeTrue();
        raisedColor.Should().Be(targetItem.Color);

        var savedState = _stateService.Load();
        savedState.Palette.ToolFavoriteColors[vm.ActiveTool.Id.ToLowerInvariant()]
            .Should().Be($"#{targetItem.Color.A:X2}{targetItem.Color.R:X2}{targetItem.Color.G:X2}{targetItem.Color.B:X2}");
    }
}
