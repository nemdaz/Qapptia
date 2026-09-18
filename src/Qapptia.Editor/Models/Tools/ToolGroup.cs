using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Representa una opción en la barra de herramientas que agrupa una o más herramientas bajo un mismo contrato unificado.
/// </summary>
public partial class ToolGroup : ObservableObject
{
    public string Id { get; }
    public string DisplayName { get; }
    public IReadOnlyList<Tool> Tools { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconKey))]
    [NotifyPropertyChangedFor(nameof(OtherTools))]
    private Tool _activeTool;

    /// <summary>
    /// Indica si el grupo contiene dos o más herramientas y requiere habilitar menú desplegable.
    /// </summary>
    public bool HasMultipleTools => Tools.Count > 1;

    /// <summary>
    /// Clave del icono vectorial de la herramienta activa del grupo para proyección en la vista.
    /// </summary>
    public string IconKey => ActiveTool.IconKey;

    /// <summary>
    /// Herramientas alternativas del grupo excluyendo la herramienta actualmente activa en el slot principal.
    /// </summary>
    public IEnumerable<Tool> OtherTools => Tools.Where(t => !string.Equals(t.Id, ActiveTool.Id, StringComparison.OrdinalIgnoreCase));

    public ToolGroup(string id, string displayName, IEnumerable<Tool> tools, Tool? initialTool = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(tools);

        Id = id;
        DisplayName = displayName;
        Tools = tools.ToList().AsReadOnly();

        if (Tools.Count == 0)
        {
            throw new ArgumentException("Un grupo de herramientas debe contener al menos una herramienta.", nameof(tools));
        }

        _activeTool = initialTool != null && Tools.Any(t => string.Equals(t.Id, initialTool.Id, StringComparison.OrdinalIgnoreCase))
            ? Tools.First(t => string.Equals(t.Id, initialTool.Id, StringComparison.OrdinalIgnoreCase))
            : Tools[0];
    }

    /// <summary>
    /// Comprueba si la herramienta indicada forma parte de este grupo.
    /// </summary>
    public bool ContainsTool(Tool tool)
    {
        return tool != null && Tools.Any(t => string.Equals(t.Id, tool.Id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Comprueba si el identificador indicado corresponde a una herramienta de este grupo.
    /// </summary>
    public bool ContainsTool(string toolId)
    {
        return !string.IsNullOrEmpty(toolId) && Tools.Any(t => string.Equals(t.Id, toolId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Selecciona la herramienta indicada como activa dentro de este grupo.
    /// </summary>
    public bool SelectTool(Tool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        var found = Tools.FirstOrDefault(t => string.Equals(t.Id, tool.Id, StringComparison.OrdinalIgnoreCase));
        if (found != null)
        {
            ActiveTool = found;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Selecciona la herramienta con el identificador indicado como activa dentro de este grupo.
    /// </summary>
    public bool SelectTool(string toolId)
    {
        if (string.IsNullOrEmpty(toolId)) return false;
        var found = Tools.FirstOrDefault(t => string.Equals(t.Id, toolId, StringComparison.OrdinalIgnoreCase));
        if (found != null)
        {
            ActiveTool = found;
            return true;
        }
        return false;
    }
}
