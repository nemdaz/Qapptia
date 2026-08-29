using System.Collections.Generic;
using Qapptia.Editor.Models;
using Qapptia.Editor.Models.Geometry;

namespace Qapptia.Editor.Services;

/// <summary>
/// Contrato del servicio de persistencia y serialización del estado integral del lienzo por imagen.
/// </summary>
public interface ICanvasStateService
{
    string? GetJsonPath(string? imagePath);
    CanvasState Load(string imagePath);
    CanvasState Load(string imagePath, string? mediaId);
    void Save(CanvasState state, string imagePath);
    string? FastExtractMediaId(string jsonPath);

    List<VectorGeometry> CreateShapes(IEnumerable<VectorShapeDto> dtos);
    List<VectorShapeDto> CreateDtos(IEnumerable<VectorGeometry> shapes);
}
