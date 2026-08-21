using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Qapptia.Editor.Toolbar.Models;

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
