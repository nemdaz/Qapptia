using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Qapptia.Editor.Models;

/// <summary>
/// Representa un elemento de color en la paleta del editor.
/// </summary>
public partial class PaletteColorItem : ObservableObject
{
    public Color Color { get; }
    public SolidColorBrush Brush { get; }

    [ObservableProperty]
    private bool _isSelected;

    public PaletteColorItem(Color color, bool isSelected = false)
    {
        Color = color;
        Brush = new SolidColorBrush(color);
        _isSelected = isSelected;
    }
}
