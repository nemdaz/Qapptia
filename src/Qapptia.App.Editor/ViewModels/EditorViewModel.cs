using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qapptia.Core.Configuration;
using Qapptia.Editor.Models;
using Qapptia.Editor.Sidebar.Models;
using Qapptia.Editor.Sidebar.Services;
using Qapptia.Editor.Toolbar.Models;

namespace Qapptia.App.Editor.ViewModels;

public partial class EditorViewModel : ObservableObject, IDisposable
{
    private readonly EditorStateStore _stateStore;
    private readonly string _savePath;
    private readonly Qapptia.Core.Abstractions.IClipboardService? _clipboardService;
    private readonly Qapptia.Editor.Core.IFontProvider _fontProvider;
    private readonly ISidebarService _sidebarService;

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    public EditorViewModel(
        EditorStateStore stateStore,
        string savePath,
        Qapptia.Editor.Core.IFontProvider fontProvider,
        Qapptia.Core.Abstractions.IClipboardService? clipboardService = null,
        ISidebarService? sidebarService = null)
    {
        _stateStore = stateStore;
        _savePath = savePath;
        _fontProvider = fontProvider;
        _clipboardService = clipboardService;
        _sidebarService = sidebarService ?? new SidebarService(Serilog.Log.Logger.ForContext<SidebarService>());

        var state = _stateStore.Load();
        ActiveTextSize = state.Tools.TextToolSize;

        // Cargar última herramienta seleccionada
        if (System.Enum.TryParse<ToolType>(state.Tools.ActiveTool, true, out var savedTool))
        {
            _activeTool = savedTool;
        }

        // Cargar color activo: de la herramienta guardada, o global, o primer favorito
        if (state.Palette.ToolFavoriteColors.TryGetValue(_activeTool.ToString().ToLowerInvariant(), out var toolColorHex) &&
            Avalonia.Media.Color.TryParse(toolColorHex, out var parsedToolColor))
        {
            _activeColor = parsedToolColor;
        }
        else if (Avalonia.Media.Color.TryParse(state.Palette.ActiveFavoriteColor, out var color))
        {
            _activeColor = color;
        }
        else
        {
            _activeColor = Qapptia.Editor.Core.Constants.FavoriteColors[0];
        }

        AvailableColors = new ObservableCollection<PaletteColorItem>(
            Qapptia.Editor.Core.Constants.FavoriteColors.Select(c => new PaletteColorItem(c, c == _activeColor))
        );

        // Garantizar que siempre haya al menos un color seleccionado
        if (!AvailableColors.Any(c => c.IsSelected) && AvailableColors.Count > 0)
        {
            AvailableColors[0].IsSelected = true;
            _activeColor = AvailableColors[0].Color;
        }
        
        _activeBrush = new SolidColorBrush(_activeColor);
        _activeTypeface = _fontProvider.GetTypeface(Qapptia.Editor.Core.Constants.DefaultFontFileName);

        _sidebarService.StartWatching(_savePath, () =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () => await LoadSidebarImagesAsync());
        });
    }

    [ObservableProperty]
    private SkiaSharp.SKTypeface _activeTypeface;

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
        CommitCurrentState();

        OnPropertyChanged(nameof(IsLineToolActive));
        OnPropertyChanged(nameof(IsArrowToolActive));
        OnPropertyChanged(nameof(IsEllipseToolActive));
        OnPropertyChanged(nameof(IsRectangleToolActive));
        OnPropertyChanged(nameof(IsHighlighterToolActive));
        OnPropertyChanged(nameof(IsTextToolActive));

        // Persistir herramienta activa
        var state = _stateStore.Load();
        state.Tools.ActiveTool = value.ToString();
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
    private Avalonia.Rect _currentTextBounds;

    public ITextInputShape? ActiveTextInputShape { get; private set; }
    
    public float ActiveTextSize { get; private set; } = 24f;
    
    public event EventHandler? TextInputFocusRequested;

    partial void OnActiveColorChanged(Color value)
    {
        ActiveBrush = new SolidColorBrush(value);
        if (AvailableColors != null)
        {
            bool anyMatch = false;
            foreach (var item in AvailableColors)
            {
                item.IsSelected = (item.Color == value);
                if (item.IsSelected) anyMatch = true;
            }

            if (!anyMatch && AvailableColors.Count > 0)
            {
                AvailableColors[0].IsSelected = true;
            }
        }
        if (ActiveTextInputShape is VectorShape vectorShape)
        {
            vectorShape.Color = value;
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }
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
    
    public ObservableCollection<SidebarFolder> SidebarFolders { get; } = new();

    [ObservableProperty]
    private SidebarItem? _selectedNode;

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

    public string? CurrentImagePath => (SelectedNode as SidebarFile)?.FullPath ?? _currentImagePath;

    public string? CurrentImageId { get; private set; }

    partial void OnSelectedNodeChanged(SidebarItem? value)
    {
        if (!string.IsNullOrEmpty(_currentImagePath))
        {
            Store.SaveAnnotations(_currentImagePath);
            _currentImagePath = null;
        }

        if (value is SidebarFile file)
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
                state.Session.LastSelectedFile = NormalizePath(file.FullPath);
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

    public void TriggerRedraw()
    {
        RequestRedraw?.Invoke(this, EventArgs.Empty);
    }

    public void StartTextInput(ITextInputShape shape)
    {
        if (IsEditingText)
        {
            CommitCurrentState();
        }

        ActiveTextInputShape = shape;
        ActiveTextInputShape.FocusRequested += OnActiveShapeFocusRequested;
        shape.IsEditing = true;
        shape.CaretIndex = shape.Text.Length;
        shape.IsCaretVisible = true;
        
        CurrentTextBounds = shape.TextBounds;
        IsEditingText = true;
        RequestRedraw?.Invoke(this, EventArgs.Empty);
        shape.RequestFocus();
    }

    [RelayCommand]
    public void CommitCurrentState()
    {
        if (IsEditingText && ActiveTextInputShape != null)
        {
            ActiveTextInputShape.FocusRequested -= OnActiveShapeFocusRequested;
            ActiveTextInputShape.IsEditing = false;
            
            if (ActiveTextInputShape.IsEmpty && ActiveTextInputShape is VectorShape vectorShape)
            {
                Store.RemoveShape(vectorShape);
            }
            
            IsEditingText = false;
            
            if (ActiveTextInputShape.TextSize != ActiveTextSize)
            {
                ActiveTextSize = ActiveTextInputShape.TextSize;
                var state = _stateStore.Load();
                state.Tools.TextToolSize = ActiveTextSize;
                _stateStore.Save(state);
            }
            
            ActiveTextInputShape = null;
            Store.ClearSelection();
            SaveCurrentAnnotations();
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Store.ClearSelection();
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnActiveShapeFocusRequested(object? sender, EventArgs e)
    {
        TextInputFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    public ObservableCollection<PaletteColorItem> AvailableColors { get; }

    [RelayCommand]
    public void SelectTool(string toolName)
    {
        if (System.Enum.TryParse<ToolType>(toolName, out var tool))
        {
            ActiveTool = tool;
            
            var state = _stateStore.Load();
            if (state.Palette.ToolFavoriteColors.TryGetValue(toolName.ToLowerInvariant(), out var colorName) && 
                Avalonia.Media.Color.TryParse(colorName, out var parsedColor))
            {
                ActiveColor = parsedColor;
            }
        }
    }

    [RelayCommand]
    public void SelectColor(PaletteColorItem item)
    {
        ActiveColor = item.Color;
        
        var state = _stateStore.Load();
        state.Palette.ActiveFavoriteColor = $"#{item.Color.A:X2}{item.Color.R:X2}{item.Color.G:X2}{item.Color.B:X2}";
        state.Palette.ToolFavoriteColors[ActiveTool.ToString().ToLowerInvariant()] = state.Palette.ActiveFavoriteColor;
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
    public event EventHandler? RotateRequested;



    [ObservableProperty]
    private bool _isToastVisible;

    [ObservableProperty]
    private string _toastMessage = string.Empty;

    [ObservableProperty]
    private Qapptia.UI.Components.Controls.ToastNotificationType _toastType = Qapptia.UI.Components.Controls.ToastNotificationType.Success;

    private System.Threading.CancellationTokenSource? _toastCts;

    public void ShowToast(string message, NotificationType type)
    {
        ToastMessage = message;
        ToastType = type switch
        {
            NotificationType.Success => Qapptia.UI.Components.Controls.ToastNotificationType.Success,
            NotificationType.Error => Qapptia.UI.Components.Controls.ToastNotificationType.Error,
            NotificationType.Warning => Qapptia.UI.Components.Controls.ToastNotificationType.Warning,
            NotificationType.Info => Qapptia.UI.Components.Controls.ToastNotificationType.Info,
            _ => Qapptia.UI.Components.Controls.ToastNotificationType.Success
        };
        
        _toastCts?.Cancel();
        _toastCts = new System.Threading.CancellationTokenSource();
        var token = _toastCts.Token;

        IsToastVisible = true;

        System.Threading.Tasks.Task.Delay(2500, token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsToastVisible = false;
                });
            }
        }, token);
    }

#pragma warning disable CA1822
    [RelayCommand]
    public void Save()
    {
        if (SelectedNode is SidebarFile)
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
        
        var nodeToSelect = _sidebarService.FindNodeByPath(SidebarFolders, NormalizePath(path));
        if (nodeToSelect != null)
        {
            SelectedNode = nodeToSelect;
        }
    }

    [RelayCommand]
    public void DeleteSelected()
    {
        if (IsEditingText) return;

        if (Store.Shapes.Any(s => s.IsSelected))
        {
            Store.RemoveSelected();
            SaveCurrentAnnotations();
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    public void Copy()
    {
        CopyRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public async System.Threading.Tasks.Task CopyFile()
    {
        string? filePath = (SelectedNode as SidebarFile)?.FullPath ?? CurrentImagePath;
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) return;

        if (_clipboardService != null)
        {
            try
            {
                await _clipboardService.SetFileDropListAsync(new[] { filePath });
                ShowToast("Archivo copiado al portapapeles", NotificationType.Success);
            }
            catch
            {
                ShowToast("Error al copiar el archivo", NotificationType.Error);
            }
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
    public async Task LoadSidebarImagesAsync()
    {
        var savePath = _savePath;
        if (!Directory.Exists(savePath))
        {
            SidebarFolders.Clear();
            return;
        }

        var expandedFolders = _stateStore.Load().Layout.ExpandedFolders;
        var normalizedSavePath = NormalizePath(savePath);

        var rootFolder = await _sidebarService.BuildTreeAsync(savePath, expandedFolders);
        if (rootFolder == null)
        {
            SidebarFolders.Clear();
            return;
        }

        AttachFolderExpandedEvents(rootFolder);

        SidebarFolders.Clear();
        SidebarFolders.Add(rootFolder);

        if (rootFolder.IsExpanded && expandedFolders.Count == 0)
        {
            var stateToUpdate = _stateStore.Load();
            if (!stateToUpdate.Layout.ExpandedFolders.Contains(normalizedSavePath))
            {
                stateToUpdate.Layout.ExpandedFolders.Add(normalizedSavePath);
                _stateStore.Save(stateToUpdate);
            }
        }

        var selectedPath = (SelectedNode as SidebarFile)?.FullPath ?? _currentImagePath ?? _stateStore.Load().Session.LastSelectedFile;
        if (!string.IsNullOrEmpty(selectedPath))
        {
            var nodeToSelect = _sidebarService.FindNodeByPath(SidebarFolders, selectedPath);
            if (nodeToSelect != null)
            {
                SelectedNode = nodeToSelect;
            }
        }
    }

    private void AttachFolderExpandedEvents(SidebarFolder folder)
    {
        folder.PropertyChanged += OnFolderExpandedChanged;
        foreach (var item in folder.Items)
        {
            if (item is SidebarFolder subFolder)
            {
                AttachFolderExpandedEvents(subFolder);
            }
        }
    }

    private void OnFolderExpandedChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SidebarItem.IsExpanded) && sender is SidebarFolder folder)
        {
            var state = _stateStore.Load();
            var normalizedPath = NormalizePath(folder.FullPath);
            var exists = state.Layout.ExpandedFolders.Any(p => string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase));
            
            if (folder.IsExpanded)
            {
                if (!exists)
                {
                    state.Layout.ExpandedFolders.Add(normalizedPath);
                    _stateStore.Save(state);
                }
            }
            else
            {
                if (exists)
                {
                    state.Layout.ExpandedFolders.RemoveAll(p => string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase));
                    _stateStore.Save(state);
                }
            }
        }
    }

    public void Dispose()
    {
        _sidebarService.Dispose();
        _toastCts?.Dispose();
        _toastCts = null;
        GC.SuppressFinalize(this);
    }
}
