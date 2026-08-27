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
    public abstract string IconData { get; }
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

    public string ToolTipText => string.IsNullOrEmpty(Shortcut)
        ? DisplayName
        : $"{DisplayName} ({Shortcut})";
}
