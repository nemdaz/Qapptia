using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Qapptia.Editor.Models;

public class VectorStore : IDisposable
{
    public ObservableCollection<VectorShape> Shapes { get; } = new();
    
    public Bitmap? BackgroundImage { get; private set; }

    public void SetBackground(Bitmap bitmap)
    {
        BackgroundImage?.Dispose();
        BackgroundImage = bitmap;
    }

    public void AddShape(VectorShape shape)
    {
        Shapes.Add(shape);
    }

    public void RemoveShape(VectorShape shape)
    {
        Shapes.Remove(shape);
    }
    
    public void ClearSelection()
    {
        foreach (var shape in Shapes)
        {
            shape.IsSelected = false;
        }
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new() 
    { 
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string? GetJsonPath(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return null;
        
        string parentDir = Path.GetDirectoryName(imagePath) ?? string.Empty;
        string baseName = Path.GetFileNameWithoutExtension(imagePath);
        string annotationDir = Path.Combine(parentDir, Qapptia.Editor.Core.Constants.DrawingExtension);
        return Path.Combine(annotationDir, $"{baseName}.json");
    }

    public void LoadAnnotations(string imagePath)
    {
        Shapes.Clear();
        string? path = GetJsonPath(imagePath);
        
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        try
        {
            string json = File.ReadAllText(path);
            var dtos = JsonSerializer.Deserialize<System.Collections.Generic.List<VectorShapeDto>>(json);
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
                    _ => null
                };

                if (shape != null && dto.Coords.Count >= 4)
                {
                    shape.Start = new Point(dto.Coords[0], dto.Coords[1]);
                    shape.End = new Point(dto.Coords[2], dto.Coords[3]);
                    
                    shape.Color = Qapptia.Editor.Core.Constants.ParseColorName(dto.Color);
                    
                    Shapes.Add(shape);
                }
            }
        }
        catch
        {
            // Fallar silenciosamente si el JSON es inválido
        }
    }

    public void SaveAnnotations(string imagePath)
    {
        string? path = GetJsonPath(imagePath);
        if (string.IsNullOrEmpty(path)) return;

        if (Shapes.Count == 0)
        {
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }
            return;
        }

        var dtos = Shapes.Select(s => new VectorShapeDto
        {
            Type = s switch
            {
                RectangleShape => "rect",
                ArrowShape => "arrow",
                EllipseShape => "ellipse",
                LineShape => "line",
                HighlighterShape => "highlighter",
                _ => "unknown"
            },
            Id = s.Id.ToString(),
            Coords = new System.Collections.Generic.List<double> { s.Start.X, s.Start.Y, s.End.X, s.End.Y },
            Color = Qapptia.Editor.Core.Constants.GetColorName(s.Color)
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
        catch
        {
            // Error al guardar, ignorar
        }
    }

    public void Dispose()
    {
        BackgroundImage?.Dispose();
        GC.SuppressFinalize(this);
    }
}
