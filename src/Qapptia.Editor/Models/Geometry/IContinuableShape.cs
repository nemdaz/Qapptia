using Avalonia;

namespace Qapptia.Editor.Models.Geometry;

/// <summary>
/// Contrato para cualquier figura vectorial que admite prolongación o continuación
/// interactiva de su trazado a partir de nodos extremos (ej. trazo libre, polilíneas).
/// </summary>
public interface IContinuableShape
{
    /// <summary>
    /// Intenta iniciar la continuación interactiva del trazo a partir de una maneta activada.
    /// Retorna verdadero si la maneta corresponde a un extremo admisible.
    /// </summary>
    bool TryStartContinuation(HandleType handle);

    /// <summary>
    /// Incorpora una nueva posición del cursor durante la continuación interactiva del trazo.
    /// </summary>
    void ContinueDrawing(Point point);

    /// <summary>
    /// Determina si la figura prolongada reúne las condiciones mínimas para ser confirmada al soltar el puntero.
    /// </summary>
    bool ShouldCommitContinuation();
}
