using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Qapptia.Editor.Models;
using Qapptia.Editor.Models.Geometry;
using Serilog;

namespace Qapptia.Editor.Services;

/// <summary>
/// Servicio de persistencia y serialización no destructiva del estado integral del lienzo en formato JSON.
/// </summary>
public sealed class CanvasStateService : ICanvasStateService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ILogger? _logger;

    public CanvasStateService(ILogger? logger = null)
    {
        _logger = logger;
    }

    public string? GetJsonPath(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return null;

        string parentDir = Path.GetDirectoryName(imagePath) ?? string.Empty;
        string baseName = Path.GetFileNameWithoutExtension(imagePath);
        string annotationDir = Path.Combine(parentDir, Qapptia.Core.Constants.DrawingExtension);
        return Path.Combine(annotationDir, $"{baseName}.json");
    }

    public CanvasState Load(string imagePath)
    {
        string? path = GetJsonPath(imagePath);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return new CanvasState();
        }

        try
        {
            string json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                // Retrocompatibilidad con formato legado de array plano
                var shapeDtos = JsonSerializer.Deserialize<List<VectorShapeDto>>(json);
                return new CanvasState { Shapes = shapeDtos ?? new() };
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var state = JsonSerializer.Deserialize<CanvasState>(json, s_jsonOptions);
                return state ?? new CanvasState();
            }

            return new CanvasState();
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Error al deserializar estado del lienzo desde {Path}. Devolviendo estado vacío.", path);
            return new CanvasState();
        }
    }

    public void Save(CanvasState state, string imagePath)
    {
        ArgumentNullException.ThrowIfNull(state);

        string? path = GetJsonPath(imagePath);
        if (string.IsNullOrEmpty(path)) return;

        if (!state.HasModifications)
        {
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }
            return;
        }

        try
        {
            string dir = Path.GetDirectoryName(path)!;
            if (!Directory.Exists(dir))
            {
                var dirInfo = Directory.CreateDirectory(dir);
                dirInfo.Attributes |= FileAttributes.Hidden;
            }

            string json = JsonSerializer.Serialize(state, s_jsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Error al guardar estado del lienzo en {Path}", path);
        }
    }

    public List<VectorGeometry> CreateShapes(IEnumerable<VectorShapeDto> dtos)
    {
        ArgumentNullException.ThrowIfNull(dtos);

        var shapes = new List<VectorGeometry>();
        foreach (var dto in dtos)
        {
            VectorGeometry? shape = dto.Type switch
            {
                "rect" => new RectangleGeometry(),
                "arrow" => new ArrowGeometry(),
                "ellipse" => new EllipseGeometry(),
                "line" => new LineGeometry(),
                "highlighter" => new HighlighterGeometry(),
                "text" => new TextGeometry(),
                _ => null
            };

            if (shape != null && dto.Coords.Count >= 4)
            {
                shape.Start = new Point(dto.Coords[0], dto.Coords[1]);
                shape.End = new Point(dto.Coords[2], dto.Coords[3]);
                shape.Color = Qapptia.Editor.Core.Constants.ParseColorName(dto.Color);

                if (shape is TextGeometry textShape && dto.Payload != null)
                {
                    if (dto.Payload.TryGetValue("text", out var textVal))
                    {
                        if (textVal is JsonElement textElem && textElem.ValueKind == JsonValueKind.String)
                        {
                            textShape.Text = textElem.GetString() ?? string.Empty;
                        }
                        else if (textVal is string str)
                        {
                            textShape.Text = str;
                        }
                    }
                    if (dto.Payload.TryGetValue("text_size", out var sizeVal))
                    {
                        if (sizeVal is JsonElement sizeElem && sizeElem.ValueKind == JsonValueKind.Number && sizeElem.TryGetInt32(out int size))
                        {
                            textShape.TextSize = size;
                        }
                        else if (sizeVal is int intVal)
                        {
                            textShape.TextSize = intVal;
                        }
                        else if (sizeVal is float floatVal)
                        {
                            textShape.TextSize = floatVal;
                        }
                        else if (sizeVal is double doubleVal)
                        {
                            textShape.TextSize = (float)doubleVal;
                        }
                    }
                }

                shapes.Add(shape);
            }
        }

        return shapes;
    }

    public List<VectorShapeDto> CreateDtos(IEnumerable<VectorGeometry> shapes)
    {
        ArgumentNullException.ThrowIfNull(shapes);

        return shapes.Select(s => new VectorShapeDto
        {
            Type = s switch
            {
                RectangleGeometry => "rect",
                ArrowGeometry => "arrow",
                EllipseGeometry => "ellipse",
                LineGeometry => "line",
                HighlighterGeometry => "highlighter",
                TextGeometry => "text",
                _ => "unknown"
            },
            Id = s.Id.ToString(),
            Coords = new List<double> { s.Start.X, s.Start.Y, s.End.X, s.End.Y },
            Color = Qapptia.Editor.Core.Constants.GetColorName(s.Color),
            Payload = s is TextGeometry ts ? new Dictionary<string, object>
            {
                { "text", ts.Text },
                { "text_size", ts.TextSize }
            } : null
        }).ToList();
    }
}
