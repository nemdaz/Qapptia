using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Qapptia.Editor.Models;
using Serilog;

namespace Qapptia.Editor.Services;

/// <summary>
/// Servicio de persistencia y serialización no destructiva de anotaciones vectoriales en formato JSON (.annotations).
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

    public void LoadAnnotations(CanvasState canvasState, string imagePath)
    {
        ArgumentNullException.ThrowIfNull(canvasState);

        canvasState.Shapes.Clear();
        string? path = GetJsonPath(imagePath);

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        try
        {
            string json = File.ReadAllText(path);
            var dtos = JsonSerializer.Deserialize<List<VectorShapeDto>>(json);
            if (dtos == null) return;

            foreach (var dto in dtos)
            {
                VectorShape? shape = dto.Type switch
                {
                    "rect" => new RectangleShape(),
                    "arrow" => new ArrowShape(),
                    "ellipse" => new EllipseShape(),
                    "line" => new LineShape(),
                    "highlighter" => new HighlighterShape(),
                    "text" => new TextShape(),
                    _ => null
                };

                if (shape != null && dto.Coords.Count >= 4)
                {
                    shape.Start = new Point(dto.Coords[0], dto.Coords[1]);
                    shape.End = new Point(dto.Coords[2], dto.Coords[3]);
                    shape.Color = Qapptia.Editor.Core.Constants.ParseColorName(dto.Color);

                    if (shape is TextShape textShape && dto.Payload != null)
                    {
                        if (dto.Payload.TryGetValue("text", out var textVal) && textVal is JsonElement textElem && textElem.ValueKind == JsonValueKind.String)
                        {
                            textShape.Text = textElem.GetString() ?? string.Empty;
                        }
                        if (dto.Payload.TryGetValue("text_size", out var sizeVal) && sizeVal is JsonElement sizeElem && sizeElem.ValueKind == JsonValueKind.Number)
                        {
                            if (sizeElem.TryGetInt32(out int size))
                            {
                                textShape.TextSize = size;
                            }
                        }
                    }

                    canvasState.Shapes.Add(shape);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Error al deserializar anotaciones desde {Path}", path);
        }
    }

    public void SaveAnnotations(CanvasState canvasState, string imagePath)
    {
        ArgumentNullException.ThrowIfNull(canvasState);

        string? path = GetJsonPath(imagePath);
        if (string.IsNullOrEmpty(path)) return;

        if (canvasState.Shapes.Count == 0)
        {
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }
            return;
        }

        var dtos = canvasState.Shapes.Select(s => new VectorShapeDto
        {
            Type = s switch
            {
                RectangleShape => "rect",
                ArrowShape => "arrow",
                EllipseShape => "ellipse",
                LineShape => "line",
                HighlighterShape => "highlighter",
                TextShape => "text",
                _ => "unknown"
            },
            Id = s.Id.ToString(),
            Coords = new List<double> { s.Start.X, s.Start.Y, s.End.X, s.End.Y },
            Color = Qapptia.Editor.Core.Constants.GetColorName(s.Color),
            Payload = s is TextShape ts ? new Dictionary<string, object>
            {
                { "text", ts.Text },
                { "text_size", ts.TextSize }
            } : null
        }).ToList();

        try
        {
            string dir = Path.GetDirectoryName(path)!;
            if (!Directory.Exists(dir))
            {
                var dirInfo = Directory.CreateDirectory(dir);
                dirInfo.Attributes |= FileAttributes.Hidden;
            }

            string json = JsonSerializer.Serialize(dtos, s_jsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Error al guardar anotaciones en {Path}", path);
        }
    }
}
