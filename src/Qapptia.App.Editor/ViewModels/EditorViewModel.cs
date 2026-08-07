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

public abstract class ExplorerNode : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
}

public partial class ExplorerFolder : ExplorerNode
{
    public ObservableCollection<ExplorerNode> Items { get; } = new();
    
    // Helper para expandir nodos del árbol
    public bool IsExpanded { get; set; }
}

public partial class ExplorerFile : ExplorerNode
{
}

public partial class EditorViewModel : ObservableObject
{
    [ObservableProperty]
    private ToolType _activeTool = ToolType.Arrow;

    [ObservableProperty]
    private Color _activeColor = Qapptia.Editor.Core.Constants.ColorGreen;

    [ObservableProperty]
    private float _zoomLevel = 1.0f;
    
    partial void OnZoomLevelChanged(float value)
    {
        var newStr = $"{(int)Math.Round(value * 100)}%";
        if (!ZoomOptions.Contains(newStr))
        {
            if (!string.IsNullOrEmpty(_lastCustomZoom) && ZoomOptions.Contains(_lastCustomZoom))
            {
                ZoomOptions.Remove(_lastCustomZoom);
            }
            
            // Insertar ordenadamente (opcional) o al final
            ZoomOptions.Add(newStr);
            _lastCustomZoom = newStr;
        }
        SelectedZoomString = newStr;
    }

    private string _lastCustomZoom = "";

    public ObservableCollection<string> ZoomOptions { get; } = new()
    {
        "25%", "50%", "75%", "100%", "125%", "150%", "200%", "300%", "400%", "500%"
    };

    [ObservableProperty]
    private string _selectedZoomString = "100%";

    partial void OnSelectedZoomStringChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, out int percentage) && percentage > 0)
        {
            var newZoom = percentage / 100.0f;
            
            // Limitamos a 500% (5.0f) y 10% (0.1f) para evitar números gigantescos
            newZoom = Math.Max(0.1f, Math.Min(newZoom, 5.0f));
            percentage = (int)Math.Round(newZoom * 100);

            if (Math.Abs(newZoom - ZoomLevel) > 0.01f)
            {
                ZoomLevel = newZoom;
            }
            else if (!value.EndsWith("%") || value != $"{percentage}%")
            {
                // Dispatch para asegurar que se actualice la UI después de que termine la edición
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    SelectedZoomString = $"{percentage}%";
                });
            }
        }
    }

    public VectorStore Store { get; } = new VectorStore();
    
    public ObservableCollection<ExplorerFolder> SidebarFolders { get; } = new();

    [ObservableProperty]
    private ExplorerNode? _selectedNode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoImage))]
    private bool _hasImage;

    public bool HasNoImage => !HasImage;

    [ObservableProperty]
    private double _imageWidth = 800;

    [ObservableProperty]
    private double _imageHeight = 600;

    public event EventHandler? ImageLoaded;

    partial void OnSelectedNodeChanged(ExplorerNode? value)
    {
        if (value is ExplorerFile file)
        {
            try
            {
                var bitmap = new Avalonia.Media.Imaging.Bitmap(file.FullPath);
                Store.SetBackground(bitmap);
                Store.Shapes.Clear(); // Limpiar formas anteriores
                
                ImageWidth = bitmap.Size.Width;
                ImageHeight = bitmap.Size.Height;
                HasImage = true;
                
                ImageLoaded?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // Fallar silenciosamente si la imagen no se puede cargar
            }
        }
    }

    public ObservableCollection<Color> AvailableColors { get; } = new()
    {
        Qapptia.Editor.Core.Constants.ColorGreen,
        Qapptia.Editor.Core.Constants.ColorRed,
        Qapptia.Editor.Core.Constants.ColorBlue,
        Qapptia.Editor.Core.Constants.ColorCyan,
        Qapptia.Editor.Core.Constants.ColorYellow,
        Qapptia.Editor.Core.Constants.ColorOrange,
        Qapptia.Editor.Core.Constants.ColorWhite,
        Qapptia.Editor.Core.Constants.ColorBlack
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
            "Green" => Qapptia.Editor.Core.Constants.ColorGreen,
            "Red" => Qapptia.Editor.Core.Constants.ColorRed,
            "Blue" => Qapptia.Editor.Core.Constants.ColorBlue,
            "Cyan" => Qapptia.Editor.Core.Constants.ColorCyan,
            "Yellow" => Qapptia.Editor.Core.Constants.ColorYellow,
            "Orange" => Qapptia.Editor.Core.Constants.ColorOrange,
            "White" => Qapptia.Editor.Core.Constants.ColorWhite,
            "Black" => Qapptia.Editor.Core.Constants.ColorBlack,
            _ => Qapptia.Editor.Core.Constants.ColorGreen
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
        ZoomLevel = 1.0f;
    }

    [RelayCommand]
    public void LoadSidebarImages()
    {
        SidebarFolders.Clear();
        
        var configService = new Qapptia.Core.Configuration.JsonConfigService("config.json");
        var savePath = configService.Current.SavePath;
        try
        {

            if (Directory.Exists(savePath))
            {
                var rootFolder = new ExplorerFolder 
                { 
                    Name = Path.GetFileName(savePath), 
                    FullPath = savePath,
                    IsExpanded = true 
                };
                
                if (string.IsNullOrEmpty(rootFolder.Name)) 
                    rootFolder.Name = savePath;

                PopulateFolder(rootFolder, savePath);
                
                SidebarFolders.Add(rootFolder);
            }
        }
        catch (Exception)
        {
            // Si hay error leyendo la config, fallamos silenciosamente
        }
    }

    private void PopulateFolder(ExplorerFolder folderNode, string path)
    {
        try
        {
            // 1. Obtener directorios, omitiendo ocultos (ej. ".annotations")
            var dirs = Directory.GetDirectories(path)
                .Where(d => !new DirectoryInfo(d).Name.StartsWith("."));

            foreach (var d in dirs)
            {
                var subFolder = new ExplorerFolder { Name = Path.GetFileName(d), FullPath = d };
                PopulateFolder(subFolder, d);
                
                // Agregamos la carpeta solo si tiene contenido util
                if (subFolder.Items.Count > 0)
                {
                    folderNode.Items.Add(subFolder);
                }
            }

            // 2. Obtener imágenes (png, jpg, jpeg)
            var extensions = new[] { ".png", ".jpg", ".jpeg" };
            var files = Directory.GetFiles(path)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

            foreach (var f in files)
            {
                folderNode.Items.Add(new ExplorerFile { Name = Path.GetFileName(f), FullPath = f });
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Ignorar carpetas sin permisos
        }
    }
}
