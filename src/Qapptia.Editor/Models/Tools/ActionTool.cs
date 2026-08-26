using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Qapptia.Editor.Models;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Clase para herramientas de acción inmediata que ejecutan un comando o delegado sin estado persistente en el lienzo.
/// </summary>
public class ActionTool : Tool
{
    private readonly Func<Task>? _asyncAction;
    private readonly Action? _syncAction;

    public override string Id { get; }
    public override string DisplayName { get; }
    public override string IconData { get; }
    public override string? Shortcut { get; }
    public override ToolType Type => ToolType.Action;

    public ActionTool(string id, string displayName, string iconData, string? shortcut, Action action)
    {
        Id = id;
        DisplayName = displayName;
        IconData = iconData;
        Shortcut = shortcut;
        _syncAction = action ?? throw new ArgumentNullException(nameof(action));
    }

    public ActionTool(string id, string displayName, string iconData, string? shortcut, Func<Task> asyncAction)
    {
        Id = id;
        DisplayName = displayName;
        IconData = iconData;
        Shortcut = shortcut;
        _asyncAction = asyncAction ?? throw new ArgumentNullException(nameof(asyncAction));
    }

    public virtual async Task ExecuteAsync()
    {
        if (_asyncAction != null)
        {
            await _asyncAction();
        }
        else
        {
            _syncAction?.Invoke();
        }
    }
}
