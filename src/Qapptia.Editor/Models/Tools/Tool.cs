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

    public string ToolTipText => string.IsNullOrEmpty(Shortcut)
        ? DisplayName
        : $"{DisplayName} ({Shortcut})";
}
