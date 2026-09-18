using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qapptia.Editor.Core;
using Qapptia.Editor.Models;
using Qapptia.Editor.Services;
using Qapptia.Editor.Tools;

namespace Qapptia.App.Editor.ViewModels;

public partial class ToolbarViewModel : ObservableObject
{
    private readonly IEditorStateService _stateService;
    private Tool? _previousTool;

    public static IReadOnlyList<Tool> AvailableTools { get; } = new Tool[]
    {
        ShapeFactory.Line,
        ShapeFactory.Arrow,
        ShapeFactory.Ellipse,
        ShapeFactory.Rectangle,
        ShapeFactory.Highlighter,
        ShapeFactory.Text,
        ShapeFactory.Rotate,
        ShapeFactory.Crop
    };

    public ObservableCollection<ToolGroup> Groups { get; }

    [ObservableProperty]
    private Tool _activeTool = ShapeFactory.Arrow;

    public bool IsLineToolActive => string.Equals(ActiveTool.Id, ShapeFactory.Line.Id, StringComparison.OrdinalIgnoreCase);
    public bool IsArrowToolActive => string.Equals(ActiveTool.Id, ShapeFactory.Arrow.Id, StringComparison.OrdinalIgnoreCase);
    public bool IsEllipseToolActive => string.Equals(ActiveTool.Id, ShapeFactory.Ellipse.Id, StringComparison.OrdinalIgnoreCase);
    public bool IsRectangleToolActive => string.Equals(ActiveTool.Id, ShapeFactory.Rectangle.Id, StringComparison.OrdinalIgnoreCase);
    public bool IsHighlighterToolActive => string.Equals(ActiveTool.Id, ShapeFactory.Highlighter.Id, StringComparison.OrdinalIgnoreCase);
    public bool IsTextToolActive => string.Equals(ActiveTool.Id, ShapeFactory.Text.Id, StringComparison.OrdinalIgnoreCase);
    public bool IsCropToolActive => string.Equals(ActiveTool.Id, ShapeFactory.Crop.Id, StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    private Color _activeColor;

    [ObservableProperty]
    private SolidColorBrush _activeBrush = new(Colors.Transparent);

    public ObservableCollection<PaletteColorItem> AvailableColors { get; }

    public event EventHandler<Tool>? ToolChanged;
    public event EventHandler<Color>? ColorChanged;

    public ToolbarViewModel(IEditorStateService stateService)
    {
        _stateService = stateService;

        Groups = new ObservableCollection<ToolGroup>
        {
            new ToolGroup("Line", "Línea", new[] { ShapeFactory.Line }),
            new ToolGroup("Arrow", "Flecha", new[] { ShapeFactory.Arrow }),
            new ToolGroup("Ellipse", "Elipse", new[] { ShapeFactory.Ellipse }),
            new ToolGroup("Rectangle", "Rectángulo", new[] { ShapeFactory.Rectangle }),
            new ToolGroup("Highlighter", "Resaltador", new[] { ShapeFactory.Highlighter }),
            new ToolGroup("Text", "Texto", new[] { ShapeFactory.Text })
        };

        var state = _stateService.Load();

        // Cargar última herramienta seleccionada
        var foundTool = AvailableTools.FirstOrDefault(t => string.Equals(t.Id, state.Tools.ActiveTool, StringComparison.OrdinalIgnoreCase));
        _activeTool = foundTool ?? ShapeFactory.Arrow;

        // Sincronizar herramienta activa en el grupo correspondiente
        var activeGroup = Groups.FirstOrDefault(g => g.ContainsTool(_activeTool));
        activeGroup?.SelectTool(_activeTool);

        // Cargar color activo: de la herramienta guardada, o global, o primer favorito
        if (state.Palette.ToolFavoriteColors.TryGetValue(_activeTool.Id.ToLowerInvariant(), out var toolColorHex) &&
            Color.TryParse(toolColorHex, out var parsedToolColor))
        {
            _activeColor = parsedToolColor;
        }
        else if (Color.TryParse(state.Palette.ActiveFavoriteColor, out var color))
        {
            _activeColor = color;
        }
        else
        {
            _activeColor = Constants.FavoriteColors[0];
        }

        AvailableColors = new ObservableCollection<PaletteColorItem>(
            Constants.FavoriteColors.Select(c => new PaletteColorItem(c, c == _activeColor))
        );

        // Garantizar que siempre haya al menos un color seleccionado
        if (!AvailableColors.Any(c => c.IsSelected) && AvailableColors.Count > 0)
        {
            AvailableColors[0].IsSelected = true;
            _activeColor = AvailableColors[0].Color;
        }

        _activeBrush = new SolidColorBrush(_activeColor);
    }

    partial void OnActiveToolChanged(Tool value)
    {
        OnPropertyChanged(nameof(IsLineToolActive));
        OnPropertyChanged(nameof(IsArrowToolActive));
        OnPropertyChanged(nameof(IsEllipseToolActive));
        OnPropertyChanged(nameof(IsRectangleToolActive));
        OnPropertyChanged(nameof(IsHighlighterToolActive));
        OnPropertyChanged(nameof(IsTextToolActive));
        OnPropertyChanged(nameof(IsCropToolActive));

        // 1. Notificar inmediatamente para confirmar estado previo y limpiar selección del lienzo
        ToolChanged?.Invoke(this, value);

        // 2. Persistir herramienta activa y restaurar su color favorito únicamente si altera geometría continua
        if (value.AltersCanvasGeometry)
        {
            var state = _stateService.Load();
            state.Tools.ActiveTool = value.Id;

            if (state.Palette.ToolFavoriteColors.TryGetValue(value.Id.ToLowerInvariant(), out var toolColorHex) &&
                Color.TryParse(toolColorHex, out var parsedToolColor))
            {
                ActiveColor = parsedToolColor;
            }

            _stateService.Save(state);
        }
    }

    partial void OnActiveColorChanged(Color value)
    {
        ActiveBrush = new SolidColorBrush(value);
        if (AvailableColors != null)
        {
            bool anyMatch = false;
            foreach (var item in AvailableColors)
            {
                item.IsSelected = (item.Color == value);
                if (item.IsSelected) anyMatch = true;
            }

            if (!anyMatch && AvailableColors.Count > 0)
            {
                AvailableColors[0].IsSelected = true;
            }
        }

        ColorChanged?.Invoke(this, value);
    }

    [RelayCommand]
    public void SelectTool(string toolName)
    {
        var tool = AvailableTools.FirstOrDefault(t => string.Equals(t.Id, toolName, StringComparison.OrdinalIgnoreCase));
        if (tool != null)
        {
            SelectTool(tool);
        }
    }

    public void SelectTool(Tool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        // 1. Herramientas de acción inmediata (ActionTool): se ejecutan sin alterar ActiveTool permanente del lienzo
        if (tool.IsAction)
        {
            if (tool is ActionTool actionTool)
            {
                _ = actionTool.ExecuteAsync();
            }
            var actionGroup = Groups.FirstOrDefault(g => g.ContainsTool(tool));
            actionGroup?.SelectTool(tool);
            return;
        }

        // 2. Herramientas alternables (toggle / Interactive): si ya está activa, se desactiva
        if (tool.IsToggleable && string.Equals(ActiveTool.Id, tool.Id, StringComparison.OrdinalIgnoreCase))
        {
            DeactivateCropTool();
            return;
        }

        if (!ActiveTool.IsToggleable)
        {
            _previousTool = ActiveTool;
        }

        // 3. Sincronizar el grupo correspondiente
        var targetGroup = Groups.FirstOrDefault(g => g.ContainsTool(tool));
        targetGroup?.SelectTool(tool);

        ActiveTool = tool;
    }

    public void DeactivateCropTool()
    {
        if (ActiveTool.IsToggleable)
        {
            ActiveTool = _previousTool ?? ShapeFactory.Arrow;
        }
    }

    [RelayCommand]
    public void SelectColor(PaletteColorItem item)
    {
        ActiveColor = item.Color;

        if (ActiveTool.AltersCanvasGeometry)
        {
            var state = _stateService.Load();
            state.Palette.ActiveFavoriteColor = $"#{item.Color.A:X2}{item.Color.R:X2}{item.Color.G:X2}{item.Color.B:X2}";
            state.Palette.ToolFavoriteColors[ActiveTool.Id.ToLowerInvariant()] = state.Palette.ActiveFavoriteColor;
            _stateService.Save(state);
        }
    }
}
