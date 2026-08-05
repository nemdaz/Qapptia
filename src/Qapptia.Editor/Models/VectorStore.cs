using System;
using System.Collections.ObjectModel;
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

    public void Dispose()
    {
        BackgroundImage?.Dispose();
        GC.SuppressFinalize(this);
    }
}
