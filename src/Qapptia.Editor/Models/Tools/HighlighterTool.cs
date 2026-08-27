using Qapptia.Editor.Models;

namespace Qapptia.Editor.Tools;

/// <summary>
/// Herramienta vectorial para trazar rectángulos de resaltado translúcido.
/// </summary>
public sealed class HighlighterTool : VectorTool<HighlighterShape>
{
    public override string Id => "Highlighter";
    public override string DisplayName => "Resaltador";
    public override string IconData => IconCatalog.Highlighter;
    public override string? Shortcut => "H";
}
