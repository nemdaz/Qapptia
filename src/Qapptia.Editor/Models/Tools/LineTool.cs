using Qapptia.Editor.Models.Geometry;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Herramienta vectorial para trazar líneas rectas simples de 2 nodos.
/// </summary>
public sealed class LineTool : VectorTool<LineGeometry>
{
    public override string Id => "Line";
    public override string DisplayName => "Línea";
    public override string? Shortcut => "L";
}
