using System.Collections.Generic;
using Qapptia.Editor.Models;

namespace Qapptia.Editor.Services;

/// <summary>
/// Contrato del servicio de persistencia y serialización del estado integral del lienzo por imagen.
/// </summary>
public interface ICanvasStateService
{
    string? GetJsonPath(string? imagePath);
    CanvasState Load(string imagePath);
    void Save(CanvasState state, string imagePath);

    List<VectorShape> CreateShapes(IEnumerable<VectorShapeDto> dtos);
    List<VectorShapeDto> CreateDtos(IEnumerable<VectorShape> shapes);
}
