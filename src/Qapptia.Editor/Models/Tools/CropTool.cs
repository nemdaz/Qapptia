using Avalonia.Input;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Herramienta interactiva de recorte dinámico directo sobre el lienzo.
/// </summary>
public sealed class CropTool : InteractiveTool
{
    public override string Id => "Crop";
    public override string DisplayName => "Recortar";
    public override string IconData => IconCatalog.Crop;
    public override StandardCursorType DefaultCursor => StandardCursorType.Cross;
}
