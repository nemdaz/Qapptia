using System.Collections.ObjectModel;

namespace Qapptia.Editor.Sidebar.Models;

public partial class SidebarFolder : SidebarItem
{
    public ObservableCollection<SidebarItem> Items { get; } = new();
}
