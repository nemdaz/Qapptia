using System;
using Avalonia.Input;
using Qapptia.Editor.Models;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Clase base unificada para cualquier herramienta del editor.
/// </summary>
public abstract class Tool
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public virtual string IconKey => $"Icon{Id}";
    public virtual string? Shortcut => null;
    public abstract ToolType Type { get; }

    public virtual StandardCursorType DefaultCursor => StandardCursorType.Arrow;

    /// <summary>
    /// Tipo de figura vectorial que produce esta herramienta, o null si es una herramienta de transformación o comando.
    /// </summary>
    public virtual Type? TargetShapeType => null;

    /// <summary>
    /// Indica si la herramienta produce cambios visuales o de geometría persistibles sobre el lienzo.
    /// </summary>
    public virtual bool AltersCanvasGeometry => Type == ToolType.Vector || Type == ToolType.Interactive || Type == ToolType.Widget;

    /// <summary>
    /// Indica si la herramienta tiene comportamiento de alternancia (toggle), desactivándose al volver a pulsarse.
    /// </summary>
    public virtual bool IsToggleable => Type == ToolType.Interactive;

    /// <summary>
    /// Indica si la herramienta es una acción inmediata que no altera el modo de dibujo permanente del lienzo.
    /// </summary>
    public virtual bool IsAction => Type == ToolType.Action;

    public string ToolTipText => string.IsNullOrEmpty(Shortcut)
        ? DisplayName
        : $"{DisplayName} ({Shortcut})";
}
