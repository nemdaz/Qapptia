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

    [Fact]
    public void ToolbarViewModelInitializesGroupsWithSingleTools()
    {
        var vm = new ToolbarViewModel(_stateService);

        vm.Groups.Should().HaveCount(6);
        vm.Groups.All(g => !g.HasMultipleTools).Should().BeTrue();
        vm.Groups.All(g => g.Tools.Count == 1).Should().BeTrue();

        var arrowGroup = vm.Groups.FirstOrDefault(g => g.Id == "Arrow");
        arrowGroup.Should().NotBeNull();
        arrowGroup!.ActiveTool.Should().Be(ShapeFactory.Arrow);
        arrowGroup.IconKey.Should().Be("IconArrow");
    }

    [Fact]
    public void ToolbarViewModelSelectingToolUpdatesCorrespondingGroupActiveTool()
    {
        var vm = new ToolbarViewModel(_stateService);

        vm.SelectTool(ShapeFactory.Rectangle);

        var rectangleGroup = vm.Groups.First(g => g.Id == "Rectangle");
        rectangleGroup.ActiveTool.Should().Be(ShapeFactory.Rectangle);
        rectangleGroup.IconKey.Should().Be("IconRectangle");
        vm.ActiveTool.Should().Be(ShapeFactory.Rectangle);
    }

    [Fact]
    public void ToolbarViewModelActionToolExecutesWithoutAlteringCanvasActiveToolOrSession()
    {
        var vm = new ToolbarViewModel(_stateService);
        vm.SelectTool(ShapeFactory.Line);
        vm.ActiveTool.Should().Be(ShapeFactory.Line);

        bool actionExecuted = false;
        var customActionTool = new ActionTool("CustomAction", "Acción Personalizada", null, () => { actionExecuted = true; });

        vm.SelectTool(customActionTool);

        actionExecuted.Should().BeTrue();
        vm.ActiveTool.Should().Be(ShapeFactory.Line, "Las herramientas de acción no deben cambiar la herramienta de dibujo permanente del lienzo");

        var state = _stateService.Load();
        state.Tools.ActiveTool.Should().Be("Line", "Las herramientas de acción no deben persistir en sesión como herramienta de dibujo");
    }

    [Fact]
    public void ToolGroupMultipleToolsSelectionUpdatesSlotAndIconKey()
    {
        var group = new ToolGroup("TestGroup", "Grupo de Prueba", new Tool[] { ShapeFactory.Line, ShapeFactory.Arrow });

        group.HasMultipleTools.Should().BeTrue();
        group.ActiveTool.Should().Be(ShapeFactory.Line);
        group.IconKey.Should().Be("IconLine");

        bool eventRaised = false;
        group.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ToolGroup.IconKey) || e.PropertyName == nameof(ToolGroup.ActiveTool))
            {
                eventRaised = true;
            }
        };

        var selected = group.SelectTool(ShapeFactory.Arrow);
        selected.Should().BeTrue();
        group.ActiveTool.Should().Be(ShapeFactory.Arrow);
        group.IconKey.Should().Be("IconArrow");
        eventRaised.Should().BeTrue();
    }
}
