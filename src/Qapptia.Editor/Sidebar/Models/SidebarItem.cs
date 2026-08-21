using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Qapptia.Editor.Sidebar.Models;

public abstract partial class SidebarItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public DateTime EffectiveDateUtc { get; set; } = DateTime.MinValue;

    [ObservableProperty]
    private bool _isExpanded;
}
