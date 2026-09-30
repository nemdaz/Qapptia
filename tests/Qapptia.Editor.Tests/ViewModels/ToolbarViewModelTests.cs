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
    public void ToolbarViewModelInitializesGroupsCorrectly()
    {
        var vm = new ToolbarViewModel(_stateService);

        vm.Groups.Should().HaveCount(8);
        
        // Cada grupo tiene su herramienta configurada sin glifo por defecto (HasMultipleTools == false)
        var lineGroup = vm.Groups.First(g => g.Id == "Line");
        lineGroup.HasMultipleTools.Should().BeFalse();
        lineGroup.Tools.Should().HaveCount(1);
        lineGroup.ActiveTool.Should().Be(ShapeFactory.Line);
        lineGroup.IconKey.Should().Be("IconLine");

        var freehandGroup = vm.Groups.First(g => g.Id == "FreehandLine");
        freehandGroup.HasMultipleTools.Should().BeFalse();
        freehandGroup.Tools.Should().HaveCount(1);
        freehandGroup.ActiveTool.Should().Be(ShapeFactory.FreehandLine);
        freehandGroup.IconKey.Should().Be("IconFreehandLine");

        // Todos los grupos actuales tienen 1 herramienta y HasMultipleTools en false
        vm.Groups.All(g => !g.HasMultipleTools).Should().BeTrue();
        vm.Groups.All(g => g.Tools.Count == 1).Should().BeTrue();

        var arrowGroup = vm.Groups.FirstOrDefault(g => g.Id == "Arrow");
        arrowGroup.Should().NotBeNull();
        arrowGroup!.ActiveTool.Should().Be(ShapeFactory.Arrow);
        arrowGroup.IconKey.Should().Be("IconArrow");

        var smartEraserGroup = vm.Groups.FirstOrDefault(g => g.Id == "SmartEraser");
        smartEraserGroup.Should().NotBeNull();
        smartEraserGroup!.ActiveTool.Should().Be(ShapeFactory.SmartEraser);
        smartEraserGroup.IconKey.Should().Be("IconSmartEraser");
    }

    [Fact]
    public void ToolbarViewModelSelectingFreehandLineActivatesFreehandToolIndependently()
    {
        var vm = new ToolbarViewModel(_stateService);

        vm.SelectTool(ShapeFactory.FreehandLine);

        vm.ActiveTool.Should().Be(ShapeFactory.FreehandLine);
        vm.IsFreehandLineToolActive.Should().BeTrue();
        vm.IsLineToolActive.Should().BeFalse();
        vm.FreehandLineGroup.ActiveTool.Should().Be(ShapeFactory.FreehandLine);
        vm.FreehandLineGroup.IconKey.Should().Be("IconFreehandLine");

        var savedState = _stateService.Load();
        savedState.Tools.ActiveTool.Should().Be("FreehandLine");
    }

    [Fact]
    public void ToolbarViewModelSelectToolByNameFreehandLineSelectsIndependently()
    {
        var vm = new ToolbarViewModel(_stateService);

        vm.SelectTool("FreehandLine");

        vm.ActiveTool.Should().Be(ShapeFactory.FreehandLine);
        vm.IsFreehandLineToolActive.Should().BeTrue();
        vm.IsLineToolActive.Should().BeFalse();
        vm.FreehandLineGroup.ActiveTool.Should().Be(ShapeFactory.FreehandLine);
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

    [Fact]
    public void ToolGroupOtherToolsExcludesActiveToolAndUpdatesWhenActiveToolChanges()
    {
        var group = new ToolGroup("Line", "Línea", new Tool[] { ShapeFactory.Line, ShapeFactory.FreehandLine });

        // Inicialmente Line es activa -> OtherTools solo contiene FreehandLine
        group.ActiveTool.Should().Be(ShapeFactory.Line);
        group.OtherTools.Should().ContainSingle().Which.Should().Be(ShapeFactory.FreehandLine);

        // Al seleccionar FreehandLine -> OtherTools solo contiene Line
        group.SelectTool(ShapeFactory.FreehandLine);
        group.ActiveTool.Should().Be(ShapeFactory.FreehandLine);
        group.OtherTools.Should().ContainSingle().Which.Should().Be(ShapeFactory.Line);
    }

    [Fact]
    public void ToolbarViewModelSmartEraserDisablesColorSelectionAndClearsPaletteSelection()
    {
        var vm = new ToolbarViewModel(_stateService);
        vm.SelectTool(ShapeFactory.Arrow);
        vm.IsColorSelectionEnabled.Should().BeTrue();
        vm.AvailableColors.Any(c => c.IsSelected).Should().BeTrue();

        vm.SelectTool(ShapeFactory.SmartEraser);

        vm.ActiveTool.Should().Be(ShapeFactory.SmartEraser);
        vm.ActiveTool.SupportsColor.Should().BeFalse();
        vm.IsSmartEraserToolActive.Should().BeTrue();
        vm.IsColorSelectionEnabled.Should().BeFalse();
        vm.AvailableColors.All(c => !c.IsSelected).Should().BeTrue();

        var state = _stateService.Load();
        state.Tools.ActiveTool.Should().Be("SmartEraser");
        state.Palette.ToolFavoriteColors.ContainsKey("smarteraser").Should().BeFalse();
    }

    [Fact]
    public void ToolbarViewModelSelectColorIgnoredWhenSmartEraserIsActive()
    {
        var vm = new ToolbarViewModel(_stateService);
        vm.SelectTool(ShapeFactory.SmartEraser);
        var initialColor = vm.ActiveColor;

        var targetColor = vm.AvailableColors[2];
        vm.SelectColor(targetColor);

        // No debe cambiar el color activo ni marcarse como seleccionado
        vm.ActiveColor.Should().Be(initialColor);
        targetColor.IsSelected.Should().BeFalse();

        var state = _stateService.Load();
        state.Palette.ToolFavoriteColors.ContainsKey("smarteraser").Should().BeFalse();
    }

    [Fact]
    public void ToolbarViewModelSwitchingBetweenSmartEraserAndColorToolsRestoresSelection()
    {
        var vm = new ToolbarViewModel(_stateService);
        vm.SelectTool(ShapeFactory.Rectangle);
        var targetColor = vm.AvailableColors[1];
        vm.SelectColor(targetColor);
        vm.ActiveColor.Should().Be(targetColor.Color);
        targetColor.IsSelected.Should().BeTrue();

        // Cambiar a SmartEraser: se deshabilita y se limpia la selección visual de la paleta
        vm.SelectTool(ShapeFactory.SmartEraser);
        vm.IsColorSelectionEnabled.Should().BeFalse();
        vm.AvailableColors.All(c => !c.IsSelected).Should().BeTrue();

        // Volver a Rectangle: se restaura el color favorito persistido y se marca en la paleta
        vm.SelectTool(ShapeFactory.Rectangle);
        vm.IsColorSelectionEnabled.Should().BeTrue();
        vm.ActiveColor.Should().Be(targetColor.Color);
        vm.AvailableColors.Single(c => c.IsSelected).Color.Should().Be(targetColor.Color);
    }

    [Fact]
    public void ToolbarViewModelRemembersToolColorAcrossToolSwitches()
    {
        var vm = new ToolbarViewModel(_stateService);

        // 1. Configurar Línea en Amarillo
        vm.SelectTool(ShapeFactory.Line);
        var yellowItem = vm.AvailableColors.First(c => c.Color == Color.Parse("#F7EB0C"));
        vm.SelectColor(yellowItem);
        vm.ActiveColor.Should().Be(yellowItem.Color);

        // 2. Conmutar a Flecha en Rojo
        vm.SelectTool(ShapeFactory.Arrow);
        var redItem = vm.AvailableColors.First(c => c.Color == Color.Parse("#FF0000"));
        vm.SelectColor(redItem);
        vm.ActiveColor.Should().Be(redItem.Color);

        // 3. Conmutar a Rectángulo
        vm.SelectTool(ShapeFactory.Rectangle);

        // 4. Volver a Línea: debe recordar exactamente Amarillo
        vm.SelectTool(ShapeFactory.Line);
        vm.ActiveColor.Should().Be(yellowItem.Color);
        vm.AvailableColors.Single(c => c.IsSelected).Color.Should().Be(yellowItem.Color);
    }

    [Fact]
    public void ToolbarViewModelPersistsEachToolColorIndependentlyAcrossSwitches()
    {
        var vm = new ToolbarViewModel(_stateService);

        // 1. Configurar Línea en Amarillo
        vm.SelectTool(ShapeFactory.Line);
        var yellowItem = vm.AvailableColors.First(c => c.Color == Color.Parse("#F7EB0C"));
        vm.SelectColor(yellowItem);
        vm.ActiveColor.Should().Be(yellowItem.Color);

        // 2. Configurar Mano Alzada en Cian
        vm.SelectTool(ShapeFactory.FreehandLine);
        var cyanItem = vm.AvailableColors.First(c => c.Color == Color.Parse("#00B7C3"));
        vm.SelectColor(cyanItem);
        vm.ActiveColor.Should().Be(cyanItem.Color);

        // 3. Conmutar a Flecha en Rojo
        vm.SelectTool(ShapeFactory.Arrow);
        var redItem = vm.AvailableColors.First(c => c.Color == Color.Parse("#FF0000"));
        vm.SelectColor(redItem);
        vm.ActiveColor.Should().Be(redItem.Color);

        // 4. Volver a Línea: debe recordar Amarillo
        vm.SelectTool(ShapeFactory.Line);
        vm.ActiveColor.Should().Be(yellowItem.Color);
        vm.AvailableColors.Single(c => c.IsSelected).Color.Should().Be(yellowItem.Color);

        // 5. Volver a Mano Alzada: debe recordar Cian
        vm.SelectTool(ShapeFactory.FreehandLine);
        vm.ActiveColor.Should().Be(cyanItem.Color);
        vm.AvailableColors.Single(c => c.IsSelected).Color.Should().Be(cyanItem.Color);
    }
}


