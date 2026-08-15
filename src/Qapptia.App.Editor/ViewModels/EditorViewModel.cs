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

public abstract partial class ExplorerNode : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    
    [ObservableProperty]
    private bool _isExpanded;
}

public partial class ExplorerFolder : ExplorerNode
{
    public ObservableCollection<ExplorerNode> Items { get; } = new();
}

public partial class ExplorerFile : ExplorerNode
{
}

public partial class EditorViewModel : ObservableObject
{
    private readonly EditorStateStore _stateStore;
    private readonly string _savePath;

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    public EditorViewModel(EditorStateStore stateStore, string savePath)
    {
        _stateStore = stateStore;
        _savePath = savePath;
        var state = _stateStore.Load();

        if (Avalonia.Media.Color.TryParse(state.ActiveFavoriteColor, out var color))
        {
            _activeColor = color;
        }
        else
        {
            _activeColor = Qapptia.Editor.Core.Constants.FavoriteColors[0];
        }
        
        _activeBrush = new SolidColorBrush(_activeColor);

        // Cargar última herramienta seleccionada
        if (System.Enum.TryParse<ToolType>(state.ActiveTool, true, out var savedTool))
        {
            _activeTool = savedTool;
        }
    }

    [ObservableProperty]
    private ToolType _activeTool = ToolType.Arrow;

    public bool IsLineToolActive => ActiveTool == ToolType.Line;
    public bool IsArrowToolActive => ActiveTool == ToolType.Arrow;
    public bool IsEllipseToolActive => ActiveTool == ToolType.Ellipse;
    public bool IsRectangleToolActive => ActiveTool == ToolType.Rectangle;
    public bool IsHighlighterToolActive => ActiveTool == ToolType.Highlighter;
    public bool IsTextToolActive => ActiveTool == ToolType.Text;

    partial void OnActiveToolChanged(ToolType value)
    {
        OnPropertyChanged(nameof(IsLineToolActive));
        OnPropertyChanged(nameof(IsArrowToolActive));
        OnPropertyChanged(nameof(IsEllipseToolActive));
        OnPropertyChanged(nameof(IsRectangleToolActive));
        OnPropertyChanged(nameof(IsHighlighterToolActive));
        OnPropertyChanged(nameof(IsTextToolActive));

        // Persistir herramienta activa
        var state = _stateStore.Load();
        state.ActiveTool = value.ToString();
        _stateStore.Save(state);
    }

    [ObservableProperty]
    private Color _activeColor;

    [ObservableProperty]
    private SolidColorBrush _activeBrush = new(Avalonia.Media.Colors.Transparent);

    [ObservableProperty]
    private float _zoomLevel = 1.0f;

    [ObservableProperty]
    private bool _isEditingText;

    [ObservableProperty]
    private string _currentTextContent = string.Empty;

    [ObservableProperty]
    private int _currentTextSize = 24;

    [ObservableProperty]
    private Avalonia.Rect _currentTextBounds;

    public TextShape? EditingTextShape { get; private set; }
    
    partial void OnActiveColorChanged(Color value)
    {
        ActiveBrush = new SolidColorBrush(value);
    }
    
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
    public event EventHandler? RequestRedraw;

    private string? _currentImagePath;

    public string? CurrentImageId { get; private set; }

    partial void OnSelectedNodeChanged(ExplorerNode? value)
    {
        if (!string.IsNullOrEmpty(_currentImagePath))
        {
            Store.SaveAnnotations(_currentImagePath);
            _currentImagePath = null;
        }

        if (value is ExplorerFile file)
        {
            try
            {
                byte[] fileBytes = System.IO.File.ReadAllBytes(file.FullPath);
                var ms = new System.IO.MemoryStream(fileBytes);
                var bitmap = new Avalonia.Media.Imaging.Bitmap(ms);
                Store.SetBackground(bitmap);
                
                Store.LoadAnnotations(file.FullPath);
                _currentImagePath = file.FullPath;
                
                System.Threading.Tasks.Task.Run(async () =>
                {
                    CurrentImageId = await Qapptia.Core.Services.ImageMetadataService.EnsureImageIdAsync(file.FullPath);
                });
                
                ImageWidth = bitmap.Size.Width;
                ImageHeight = bitmap.Size.Height;
                HasImage = true;
                
                var state = _stateStore.Load();
                state.LastSelectedFile = NormalizePath(file.FullPath);
                _stateStore.Save(state);

                ImageLoaded?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // Fallar silenciosamente si la imagen no se puede cargar
                Store.Shapes.Clear();
            }
        }
        else
        {
            Store.Shapes.Clear();
            Store.SetBackground(null!);
            HasImage = false;
        }
    }

    public void SaveCurrentAnnotations()
    {
        if (!string.IsNullOrEmpty(_currentImagePath))
        {
            Store.SaveAnnotations(_currentImagePath);
        }
    }

    public void StartTextEditing(TextShape shape)
    {
        if (IsEditingText)
        {
            CommitTextEditing();
        }

        EditingTextShape = shape;
        CurrentTextContent = shape.Text;
        CurrentTextSize = shape.TextSize;
        
        // Posición del widget (desfase 32px para la barra de herramientas Fila 0)
        CurrentTextBounds = new Avalonia.Rect(shape.Start.X, shape.Start.Y - 32, 200, 50); 
        IsEditingText = true;
        RequestRedraw?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void CommitTextEditing()
    {
        if (!IsEditingText || EditingTextShape == null) return;
        
        EditingTextShape.Text = CurrentTextContent;
        EditingTextShape.TextSize = CurrentTextSize;
        
        // Eliminar si el texto quedó vacío
        if (string.IsNullOrWhiteSpace(EditingTextShape.Text))
        {
            Store.RemoveShape(EditingTextShape);
        }
        
        IsEditingText = false;
        EditingTextShape = null;
        Store.ClearSelection();
        SaveCurrentAnnotations();
        RequestRedraw?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void CancelTextEditing()
    {
        if (!IsEditingText) return;

        // Eliminar si el texto está vacío
        if (EditingTextShape != null && string.IsNullOrWhiteSpace(EditingTextShape.Text))
        {
            Store.RemoveShape(EditingTextShape);
        }
        
        IsEditingText = false;
        EditingTextShape = null;
        Store.ClearSelection();
        RequestRedraw?.Invoke(this, EventArgs.Empty);
    }

    public ObservableCollection<SolidColorBrush> AvailableColors { get; } = new(
        System.Linq.Enumerable.Select(
            Qapptia.Editor.Core.Constants.FavoriteColors, 
            c => new SolidColorBrush(c)
        )
    );

    [RelayCommand]
    public void SelectTool(string toolName)
    {
        if (System.Enum.TryParse<ToolType>(toolName, out var tool))
        {
            ActiveTool = tool;
            
            var state = _stateStore.Load();
            if (state.ToolFavoriteColors.TryGetValue(toolName.ToLowerInvariant(), out var colorName) && 
                Avalonia.Media.Color.TryParse(colorName, out var parsedColor))
            {
                ActiveColor = parsedColor;
            }
        }
    }

    [RelayCommand]
    public void SelectColor(SolidColorBrush brush)
    {
        ActiveColor = brush.Color;
        
        var state = _stateStore.Load();
        var colorHex = Qapptia.Editor.Core.Constants.GetColorName(ActiveColor);
        state.ActiveFavoriteColor = colorHex;
        state.ToolFavoriteColors[ActiveTool.ToString().ToLowerInvariant()] = colorHex;
        _stateStore.Save(state);
        
        bool needsRedraw = false;
        foreach (var shape in Store.Shapes)
        {
            if (shape.IsSelected)
            {
                shape.Color = ActiveColor;
                needsRedraw = true;
            }
        }
        
        if (needsRedraw)
        {
            SaveCurrentAnnotations();
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SaveRequested;
    public event EventHandler? CopyRequested;
    public event EventHandler? CopyFileRequested;
    public event EventHandler? RotateRequested;



    [ObservableProperty]
    private bool _isToastVisible;

    [ObservableProperty]
    private string _toastMessage = string.Empty;

    [ObservableProperty]
    private Qapptia.UI.Shared.Controls.ToastNotificationType _toastType = Qapptia.UI.Shared.Controls.ToastNotificationType.Success;

    public void ShowToast(string message, NotificationType type)
    {
        ToastMessage = message;
        ToastType = type switch
        {
            NotificationType.Success => Qapptia.UI.Shared.Controls.ToastNotificationType.Success,
            NotificationType.Error => Qapptia.UI.Shared.Controls.ToastNotificationType.Error,
            NotificationType.Warning => Qapptia.UI.Shared.Controls.ToastNotificationType.Warning,
            NotificationType.Info => Qapptia.UI.Shared.Controls.ToastNotificationType.Info,
            _ => Qapptia.UI.Shared.Controls.ToastNotificationType.Success
        };
        IsToastVisible = true;


        System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsToastVisible = false;
            });
        });
    }

#pragma warning disable CA1822
    [RelayCommand]
    public void Save()
    {
        if (SelectedNode is ExplorerFile)
        {
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void OnBurnCompleted()
    {
        if (string.IsNullOrEmpty(_currentImagePath)) return;
        
        // Limpiamos los vectores actuales (ya que se quemaron en la imagen original)
        Store.Shapes.Clear();
        Store.SaveAnnotations(_currentImagePath);
        
        // Forzamos la recarga de la imagen para que Avalonia la lea de nuevo
        string path = _currentImagePath;
        SelectedNode = null;
        
        var nodeToSelect = FindNodeByPath(SidebarFolders, NormalizePath(path));
        if (nodeToSelect != null)
        {
            SelectedNode = nodeToSelect;
        }
    }

    [RelayCommand]
    public void Copy()
    {
        CopyRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void CopyFile()
    {
        if (SelectedNode is ExplorerFile)
        {
            CopyFileRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    public void Rotate()
    {
        RotateRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void FitImage()
    {
        // TODO: Implement Fit Image
    }
#pragma warning restore CA1822

    [RelayCommand]
    public void RealSize()
    {
        ZoomLevel = 1.0f;
    }

    [RelayCommand]
    public void LoadSidebarImages()
    {
        SidebarFolders.Clear();
        var expandedFolders = _stateStore.Load().ExpandedFolders;

        try
        {
            if (Directory.Exists(_savePath))
            {
                var normalizedSavePath = NormalizePath(_savePath);
                var rootFolder = new ExplorerFolder 
                { 
                    Name = Path.GetFileName(_savePath), 
                    FullPath = normalizedSavePath,
                    IsExpanded = expandedFolders.Any(p => string.Equals(p, normalizedSavePath, StringComparison.OrdinalIgnoreCase)) || expandedFolders.Count == 0 // Default true if empty
                };
                
                if (string.IsNullOrEmpty(rootFolder.Name)) 
                    rootFolder.Name = normalizedSavePath;

                rootFolder.PropertyChanged += OnFolderExpandedChanged;

                // Si se expandió por defecto (al estar vacía la lista), forzamos su guardado
                if (rootFolder.IsExpanded && expandedFolders.Count == 0)
                {
                    var stateToUpdate = _stateStore.Load();
                    if (!stateToUpdate.ExpandedFolders.Contains(normalizedSavePath))
                    {
                        stateToUpdate.ExpandedFolders.Add(normalizedSavePath);
                        _stateStore.Save(stateToUpdate);
                    }
                }

                PopulateFolder(rootFolder, _savePath, expandedFolders);
                
                SidebarFolders.Add(rootFolder);
                
                var state = _stateStore.Load();
                if (!string.IsNullOrEmpty(state.LastSelectedFile))
                {
                    var normalizedLastFile = NormalizePath(state.LastSelectedFile);
                    var nodeToSelect = FindNodeByPath(SidebarFolders, normalizedLastFile);
                    if (nodeToSelect != null)
                    {
                        SelectedNode = nodeToSelect;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Si hay error leyendo la config, fallamos silenciosamente
        }
    }

    private static ExplorerNode? FindNodeByPath(IEnumerable<ExplorerNode> nodes, string path)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.FullPath, path, StringComparison.OrdinalIgnoreCase))
                return node;
                
            if (node is ExplorerFolder folder)
            {
                var found = FindNodeByPath(folder.Items, path);
                if (found != null)
                    return found;
            }
        }
        return null;
    }

    private void PopulateFolder(ExplorerFolder folderNode, string path, List<string> expandedFolders)
    {
        try
        {
            // 1. Obtener directorios, omitiendo ocultos (ej. ".annotations")
            var dirs = Directory.GetDirectories(path)
                .Where(d => !new DirectoryInfo(d).Name.StartsWith('.'));

            foreach (var d in dirs)
            {
                var normalizedD = NormalizePath(d);
                var subFolder = new ExplorerFolder 
                { 
                    Name = Path.GetFileName(d), 
                    FullPath = normalizedD,
                    IsExpanded = expandedFolders.Any(p => string.Equals(p, normalizedD, StringComparison.OrdinalIgnoreCase))
                };
                
                subFolder.PropertyChanged += OnFolderExpandedChanged;
                
                PopulateFolder(subFolder, d, expandedFolders);
                
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
                folderNode.Items.Add(new ExplorerFile { Name = Path.GetFileName(f), FullPath = NormalizePath(f) });
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Ignorar carpetas sin permisos
        }
    }

    private void OnFolderExpandedChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExplorerNode.IsExpanded) && sender is ExplorerFolder folder)
        {
            var state = _stateStore.Load();
            var normalizedPath = NormalizePath(folder.FullPath);
            var exists = state.ExpandedFolders.Any(p => string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase));
            
            if (folder.IsExpanded)
            {
                if (!exists)
                    state.ExpandedFolders.Add(normalizedPath);
            }
            else
            {
                if (exists)
                    state.ExpandedFolders.RemoveAll(p => string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase));
            }
            _stateStore.Save(state);
        }
    }
}
