using Qapptia.Editor.Models.Geometry;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Herramienta vectorial para trazar rectángulos de resaltado translúcido.
/// </summary>
public sealed class HighlighterTool : VectorTool<HighlighterGeometry>
{
    public override string Id => "Highlighter";
    public override string DisplayName => "Resaltador";
    public override string? Shortcut => "H";
}
