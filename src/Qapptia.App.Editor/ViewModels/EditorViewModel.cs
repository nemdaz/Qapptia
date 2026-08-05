using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qapptia.Editor.Models;
using Avalonia.Media;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Qapptia.Core.Configuration;
using System;
using System.Globalization;

namespace Qapptia.App.Editor.ViewModels;

public partial class ExplorerFolder : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<ExplorerFile> Files { get; } = new();
}

public partial class ExplorerFile : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
}

public partial class EditorViewModel : ObservableObject
{
    [ObservableProperty]
    private ToolType _activeTool = ToolType.Arrow;

    [ObservableProperty]
    private Color _activeColor = Colors.Green;

    [ObservableProperty]
    private float _zoomLevel = 1.0f;
    
    public VectorStore Store { get; } = new VectorStore();
    
    public ObservableCollection<ExplorerFolder> SidebarFolders { get; } = new();

    public ObservableCollection<Color> AvailableColors { get; } = new()
    {
        Colors.Lime, // Green preset
        Colors.Red,
        Color.Parse("#0078D7"), // Blue
        Color.Parse("#00B7C3"), // Cyan
        Colors.Yellow,
        Colors.Orange,
        Colors.White,
        Colors.Black
    };

    [RelayCommand]
    public void SelectTool(string toolName)
    {
        if (System.Enum.TryParse<ToolType>(toolName, out var tool))
        {
            ActiveTool = tool;
        }
    }

    [RelayCommand]
    public void SelectColor(string colorName)
    {
        ActiveColor = colorName switch
        {
            "Green" => Colors.Lime,
            "Red" => Colors.Red,
            "Blue" => Color.Parse("#0078D7"),
            "Cyan" => Color.Parse("#00B7C3"),
            "Yellow" => Colors.Yellow,
            "Orange" => Colors.Orange,
            "White" => Colors.White,
            "Black" => Colors.Black,
            _ => Colors.Lime
        };
    }

    [RelayCommand]
    public void Save()
    {
        // TODO: Implement Save
    }

    [RelayCommand]
    public void Copy()
    {
        // TODO: Implement Copy (Image)
    }

    [RelayCommand]
    public void CopyFile()
    {
        // TODO: Implement Copy File
    }

    [RelayCommand]
    public void Rotate()
    {
        // TODO: Implement Rotate
    }

    [RelayCommand]
    public void FitImage()
    {
        // TODO: Implement Fit Image
    }

    [RelayCommand]
    public void RealSize()
    {
        // TODO: Implement Real Size
    }

    [RelayCommand]
    public void LoadSidebarImages()
    {
        SidebarFolders.Clear();
        
        var configService = new JsonConfigService("config.json");
        var savePath = configService.Current.SavePath;
        
        if (string.IsNullOrWhiteSpace(savePath) || !Directory.Exists(savePath)) return;

        var files = Directory.GetFiles(savePath, "*.png")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        var grouped = files.GroupBy(f => f.CreationTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        foreach (var group in grouped)
        {
            var folder = new ExplorerFolder { Name = group.Key };
            foreach (var file in group)
            {
                folder.Files.Add(new ExplorerFile { Name = file.Name, FullPath = file.FullName });
            }
            SidebarFolders.Add(folder);
        }
    }
}
