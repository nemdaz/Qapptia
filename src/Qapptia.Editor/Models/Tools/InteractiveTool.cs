using Avalonia.Input;
using Qapptia.Editor.Models;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Clase base para herramientas interactivas de manipulación temporal sobre el lienzo
/// (ej. Recorte, Selección de área, Borrador) que operan con geometría sin persistir como figuras vectoriales permanentes.
/// </summary>
public abstract class InteractiveTool : Tool
{
    public override ToolType Type => ToolType.Interactive;
    public override StandardCursorType DefaultCursor => StandardCursorType.Cross;
}
