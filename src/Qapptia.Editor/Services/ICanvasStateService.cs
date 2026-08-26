using Qapptia.Editor.Models;

namespace Qapptia.Editor.Services;

/// <summary>
/// Contrato del servicio de persistencia y serialización del estado de anotaciones del lienzo por imagen.
/// </summary>
public interface ICanvasStateService
{
    string? GetJsonPath(string? imagePath);
    void LoadAnnotations(CanvasState canvasState, string imagePath);
    void SaveAnnotations(CanvasState canvasState, string imagePath);
}
