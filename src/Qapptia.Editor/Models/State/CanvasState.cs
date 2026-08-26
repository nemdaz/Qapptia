using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;

namespace Qapptia.Editor.Models;

/// <summary>
/// Modelo de estado en memoria del lienzo de edición (imagen de fondo, figuras y selección).
/// </summary>
public class CanvasState : IDisposable
{
    public ObservableCollection<VectorShape> Shapes { get; } = new();
    
    public Bitmap? BackgroundImage { get; private set; }

    public void SetBackground(Bitmap? bitmap)
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

    public void RemoveSelected()
    {
        var selected = Shapes.Where(s => s.IsSelected).ToList();
        foreach (var shape in selected)
        {
            Shapes.Remove(shape);
        }
    }
    
    public void ClearSelection()
    {
        foreach (var shape in Shapes)
        {
            shape.IsSelected = false;
        }
    }

    public void SetBurningMode(bool isBurning)
    {
        foreach (var shape in Shapes)
        {
            shape.IsBurning = isBurning;
        }
    }

    public void Dispose()
    {
        BackgroundImage?.Dispose();
        BackgroundImage = null;
        GC.SuppressFinalize(this);
    }
}
