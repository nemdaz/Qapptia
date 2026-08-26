using System.Collections.ObjectModel;

namespace Qapptia.Editor.Models.Navigation;

/// <summary>
/// Representa un directorio o carpeta contenedora dentro de la jerarquía de navegación.
/// </summary>
public sealed class FolderItem : NavigationItem
{
    public ObservableCollection<NavigationItem> Items { get; } = new();
}
