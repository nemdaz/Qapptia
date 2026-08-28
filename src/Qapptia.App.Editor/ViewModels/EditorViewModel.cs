using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qapptia.App.Editor.ViewModels.Shapes;
using Qapptia.Core.Configuration;
using Qapptia.Editor.Core;
using Qapptia.Editor.Models;
using Qapptia.Editor.Models.Navigation;
using Qapptia.Editor.Services;
using Qapptia.Editor.Tools;

namespace Qapptia.App.Editor.ViewModels;

public partial class EditorViewModel : ObservableObject, IDisposable
{
    private readonly IEditorStateService _stateService;
    private readonly string _savePath;
    private readonly Qapptia.Core.Abstractions.IClipboardService? _clipboardService;
    private readonly Qapptia.Editor.Core.IFontProvider _fontProvider;
    private readonly INavigationService _navigationService;
    private readonly ICanvasStateService _canvasStateService;

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    public static IReadOnlyList<Tool> AvailableTools { get; } = new Tool[]
    {
        ShapeFactory.Line,
        ShapeFactory.Arrow,
        ShapeFactory.Ellipse,
        ShapeFactory.Rectangle,
        ShapeFactory.Highlighter,
        ShapeFactory.Text,
        ShapeFactory.Rotate,
        ShapeFactory.Crop
    };

    [ObservableProperty]
    private Rect? _activeCropRect;

    [ObservableProperty]
    private bool _isExporting;

    public EditorViewModel(
        IEditorStateService stateService,
        string savePath,
        Qapptia.Editor.Core.IFontProvider fontProvider,
        Qapptia.Core.Abstractions.IClipboardService? clipboardService = null,
        INavigationService? navigationService = null,
        ICanvasStateService? canvasStateService = null)
    {
        _stateService = stateService;
        _savePath = savePath;
        _fontProvider = fontProvider;
        _clipboardService = clipboardService;
        _navigationService = navigationService ?? new NavigationService(Serilog.Log.Logger.ForContext<NavigationService>());
        _canvasStateService = canvasStateService ?? new CanvasStateService(Serilog.Log.Logger.ForContext<CanvasStateService>());

        var state = _stateService.Load();
        ActiveTextSize = state.Tools.TextToolSize;

        // Cargar última herramienta seleccionada
        var foundTool = AvailableTools.FirstOrDefault(t => string.Equals(t.Id, state.Tools.ActiveTool, StringComparison.OrdinalIgnoreCase));
        _activeTool = foundTool ?? ShapeFactory.Arrow;

        // Cargar color activo: de la herramienta guardada, o global, o primer favorito
        if (state.Palette.ToolFavoriteColors.TryGetValue(_activeTool.Id.ToLowerInvariant(), out var toolColorHex) &&
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

        _navigationService.StartWatching(_savePath, () =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () => await LoadSidebarImagesAsync());
        });
    }

    [ObservableProperty]
    private SkiaSharp.SKTypeface _activeTypeface;

    [ObservableProperty]
    private Tool _activeTool = ShapeFactory.Arrow;

    public bool IsLineToolActive => ActiveTool is LineTool;
    public bool IsArrowToolActive => ActiveTool is ArrowTool;
    public bool IsEllipseToolActive => ActiveTool is EllipseTool;
    public bool IsRectangleToolActive => ActiveTool is RectangleTool;
    public bool IsHighlighterToolActive => ActiveTool is HighlighterTool;
    public bool IsTextToolActive => ActiveTool is TextWidgetTool;
    public bool IsCropToolActive => ActiveTool is CropTool;

    partial void OnActiveToolChanged(Tool value)
    {
        CommitCurrentState();

        OnPropertyChanged(nameof(IsLineToolActive));
        OnPropertyChanged(nameof(IsArrowToolActive));
        OnPropertyChanged(nameof(IsEllipseToolActive));
        OnPropertyChanged(nameof(IsRectangleToolActive));
        OnPropertyChanged(nameof(IsHighlighterToolActive));
        OnPropertyChanged(nameof(IsTextToolActive));
        OnPropertyChanged(nameof(IsCropToolActive));

        // Persistir herramienta activa y restaurar su color favorito
        var state = _stateService.Load();
        state.Tools.ActiveTool = value.Id;

        // Cargar color específico de la herramienta seleccionada si existe
        if (state.Palette.ToolFavoriteColors.TryGetValue(value.Id.ToLowerInvariant(), out var toolColorHex) &&
            Avalonia.Media.Color.TryParse(toolColorHex, out var parsedToolColor))
        {
            ActiveColor = parsedToolColor;
        }

        _stateService.Save(state);
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

            ZoomOptions.Add(newStr);
            _lastCustomZoom = newStr;
        }
        SelectedZoomString = newStr;
    }

    private string _lastCustomZoom = "";

    public ObservableCollection<string> ZoomOptions { get; } = new()
    {
        "25%", "50%", "75%", "100%", "125%", "150%", "200%", "300%", "400%", "500%", "700%"
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

            // Limitamos a 9999% (99.99f) y 10% (0.1f)
            newZoom = Math.Max(0.1f, Math.Min(newZoom, 99.99f));
            percentage = (int)Math.Round(newZoom * 100);

            if (Math.Abs(newZoom - ZoomLevel) > 0.01f)
            {
                ZoomLevel = newZoom;
            }
            else if (!value.EndsWith("%") || value != $"{percentage}%")
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    SelectedZoomString = $"{percentage}%";
                });
            }
        }
    }

    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? _backgroundImage;

    public ObservableCollection<VectorShape> Shapes { get; } = new();

    public ObservableCollection<FolderItem> SidebarFolders { get; } = new();

    [ObservableProperty]
    private NavigationItem? _selectedNode;

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
    private List<double>? _currentCrop;
    private int _currentRotation;

    public string? CurrentImagePath => (SelectedNode as FileItem)?.FullPath ?? _currentImagePath;

    public string? CurrentImageId { get; private set; }

    partial void OnSelectedNodeChanged(NavigationItem? value)
    {
        if (!string.IsNullOrEmpty(_currentImagePath))
        {
            SaveCurrentAnnotations();
            _currentImagePath = null;
        }

        ActiveCropRect = null;

        if (value is FileItem file)
        {
            try
            {
                byte[] fileBytes = System.IO.File.ReadAllBytes(file.FullPath);
                var ms = new System.IO.MemoryStream(fileBytes);
                var baseBitmap = new Avalonia.Media.Imaging.Bitmap(ms);

                var canvasState = _canvasStateService.Load(file.FullPath);
                _currentRotation = canvasState.Rotation;
                _currentCrop = canvasState.Crop;

                Avalonia.Media.Imaging.Bitmap processedBitmap = baseBitmap;

                // 1. Restaurar rotación persistida si existe
                if (_currentRotation % 360 != 0)
                {
                    processedBitmap = RotateBitmap(baseBitmap, _currentRotation);
                    baseBitmap.Dispose();
                }

                // 2. Restaurar recorte persistido no destructivo si existe
                if (_currentCrop != null && _currentCrop.Count >= 4)
                {
                    ActiveCropRect = new Rect(_currentCrop[0], _currentCrop[1], _currentCrop[2], _currentCrop[3]);
                }
                else
                {
                    ActiveCropRect = null;
                }

                BackgroundImage?.Dispose();
                BackgroundImage = processedBitmap;

                Shapes.Clear();
                var loadedShapes = _canvasStateService.CreateShapes(canvasState.Shapes);
                foreach (var geometry in loadedShapes)
                {
                    Shapes.Add(ShapeViewFactory.Wrap(geometry));
                }

                _currentImagePath = file.FullPath;

                System.Threading.Tasks.Task.Run(async () =>
                {
                    CurrentImageId = await Qapptia.Core.Services.ImageMetadataService.EnsureImageIdAsync(file.FullPath);
                });

                ImageWidth = processedBitmap.Size.Width;
                ImageHeight = processedBitmap.Size.Height;
                HasImage = true;

                var state = _stateService.Load();
                state.Session.LastSelectedFile = NormalizePath(file.FullPath);
                _stateService.Save(state);

                ImageLoaded?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                Shapes.Clear();
                _currentCrop = null;
                _currentRotation = 0;
            }
        }
        else
        {
            Shapes.Clear();
            _currentCrop = null;
            _currentRotation = 0;
            BackgroundImage?.Dispose();
            BackgroundImage = null;
            HasImage = false;
        }
    }

    public void SaveCurrentAnnotations()
    {
        if (string.IsNullOrEmpty(_currentImagePath)) return;

        var state = new CanvasState
        {
            Crop = _currentCrop,
            Rotation = _currentRotation,
            Shapes = _canvasStateService.CreateDtos(Shapes.Select(s => s.Geometry))
        };

        _canvasStateService.Save(state, _currentImagePath);
    }

    partial void OnActiveCropRectChanged(Rect? value)
    {
        if (value.HasValue)
        {
            var r = value.Value;
            _currentCrop = new List<double> { r.X, r.Y, r.Width, r.Height };
        }
        else
        {
            _currentCrop = null;
        }
        SaveCurrentAnnotations();
    }



    public void RotateImage()
    {
        if (BackgroundImage == null) return;

        // Confirma edición de texto antes de rotar para preservar estado del caret y selección.
        CommitCurrentState();

        var oldBmp = BackgroundImage;
        int w = oldBmp.PixelSize.Width;
        int h = oldBmp.PixelSize.Height;

        var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(h, w), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        {
            var transform = Matrix.CreateTranslation(0, 0) * Matrix.CreateRotation(Math.PI / 2) * Matrix.CreateTranslation(h, 0);
            using (ctx.PushTransform(transform))
            {
                ctx.DrawImage(oldBmp, new Rect(0, 0, w, h));
            }
        }

        BackgroundImage?.Dispose();
        BackgroundImage = rtb;
        ImageWidth = h;
        ImageHeight = w;

        _currentRotation = (_currentRotation + 90) % 360;

        RotateTool.RotateScene90Clockwise(Shapes.Select(s => s.Geometry), h);

        SaveCurrentAnnotations();
        ShowToast("Imagen rotada 90°", NotificationType.Info);
        TriggerRedraw();
    }

    private static Avalonia.Media.Imaging.Bitmap RotateBitmap(Avalonia.Media.Imaging.Bitmap src, int degrees)
    {
        int times = (degrees % 360) / 90;
        var current = src;
        for (int i = 0; i < times; i++)
        {
            int w = current.PixelSize.Width;
            int h = current.PixelSize.Height;
            var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(h, w), new Vector(96, 96));
            using (var ctx = rtb.CreateDrawingContext())
            {
                var transform = Matrix.CreateTranslation(0, 0) * Matrix.CreateRotation(Math.PI / 2) * Matrix.CreateTranslation(h, 0);
                using (ctx.PushTransform(transform))
                {
                    ctx.DrawImage(current, new Rect(0, 0, w, h));
                }
            }
            if (current != src) current.Dispose();
            current = rtb;
        }
        return current;
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
                Shapes.Remove(vectorShape);
            }

            IsEditingText = false;

            if (ActiveTextInputShape.TextSize != ActiveTextSize)
            {
                ActiveTextSize = ActiveTextInputShape.TextSize;
                var state = _stateService.Load();
                state.Tools.TextToolSize = ActiveTextSize;
                _stateService.Save(state);
            }

            ActiveTextInputShape = null;
            ClearSelection();
            SaveCurrentAnnotations();
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            ClearSelection();
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClearSelection()
    {
        foreach (var shape in Shapes)
        {
            shape.IsSelected = false;
        }
    }

    public void SetBurningMode(bool isBurning)
    {
        foreach (var shape in Shapes)
        {
            shape.IsBurning = isBurning;
        }
    }

    private void OnActiveShapeFocusRequested(object? sender, EventArgs e)
    {
        TextInputFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    public ObservableCollection<PaletteColorItem> AvailableColors { get; }

    private Tool? _previousTool;

    [RelayCommand]
    public void SelectTool(string toolName)
    {
        var tool = AvailableTools.FirstOrDefault(t => string.Equals(t.Id, toolName, StringComparison.OrdinalIgnoreCase));
        if (tool != null)
        {
            SelectTool(tool);
        }
    }

    public void SelectTool(Tool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        // Si se vuelve a pulsar la herramienta Crop estando activa, se desactiva (toggle off)
        if (tool is CropTool && ActiveTool is CropTool)
        {
            DeactivateCropTool();
            return;
        }

        if (ActiveTool is not CropTool)
        {
            _previousTool = ActiveTool;
        }

        ActiveTool = tool;
    }

    public void DeactivateCropTool()
    {
        if (ActiveTool is CropTool)
        {
            ActiveTool = _previousTool ?? ShapeFactory.Arrow;
        }
    }

    [RelayCommand]
    public void SelectColor(PaletteColorItem item)
    {
        ActiveColor = item.Color;

        var state = _stateService.Load();
        state.Palette.ActiveFavoriteColor = $"#{item.Color.A:X2}{item.Color.R:X2}{item.Color.G:X2}{item.Color.B:X2}";
        state.Palette.ToolFavoriteColors[ActiveTool.Id.ToLowerInvariant()] = state.Palette.ActiveFavoriteColor;
        _stateService.Save(state);

        bool needsRedraw = false;
        foreach (var shape in Shapes)
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
    public event EventHandler? FitImageRequested;

    [RelayCommand]
    public void OpenConfig()
    {
        try
        {
            var exeName = Qapptia.Core.Constants.ConfigExecutableName;
            var exePath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, exeName);
            if (System.IO.File.Exists(exePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exePath) { UseShellExecute = true });
            }
            else
            {
                ShowToast("No se encontró la aplicación de configuración.", NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            ShowToast("Error al abrir configuración.", NotificationType.Error);
            Serilog.Log.Error(ex, "Error opening config app from editor");
        }
    }

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
        if (SelectedNode is FileItem)
        {
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void OnBurnCompleted()
    {
        if (string.IsNullOrEmpty(_currentImagePath)) return;

        // Limpiamos los vectores actuales y reseteamos el estado acumulado
        Shapes.Clear();
        ActiveCropRect = null;
        _currentRotation = 0;
        IsExporting = false;
        _canvasStateService.Save(new CanvasState(), _currentImagePath);

        // Forzamos la recarga de la imagen para que Avalonia la lea de nuevo
        string path = _currentImagePath;
        SelectedNode = null;

        var nodeToSelect = _navigationService.FindNodeByPath(SidebarFolders, NormalizePath(path));
        if (nodeToSelect != null)
        {
            SelectedNode = nodeToSelect;
        }
    }

    [RelayCommand]
    public void DeleteSelected()
    {
        if (IsEditingText) return;

        var selected = Shapes.Where(s => s.IsSelected).ToList();
        if (selected.Count > 0)
        {
            foreach (var shape in selected)
            {
                Shapes.Remove(shape);
            }
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
        string? filePath = (SelectedNode as FileItem)?.FullPath ?? CurrentImagePath;
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
        FitImageRequested?.Invoke(this, EventArgs.Empty);
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

        var expandedFolders = _stateService.Load().Layout.ExpandedFolders;
        var normalizedSavePath = NormalizePath(savePath);

        var rootFolder = await _navigationService.BuildTreeAsync(savePath, expandedFolders);
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
            var stateToUpdate = _stateService.Load();
            if (!stateToUpdate.Layout.ExpandedFolders.Contains(normalizedSavePath))
            {
                stateToUpdate.Layout.ExpandedFolders.Add(normalizedSavePath);
                _stateService.Save(stateToUpdate);
            }
        }

        var selectedPath = (SelectedNode as FileItem)?.FullPath ?? _currentImagePath ?? _stateService.Load().Session.LastSelectedFile;
        if (!string.IsNullOrEmpty(selectedPath))
        {
            var nodeToSelect = _navigationService.FindNodeByPath(SidebarFolders, selectedPath);
            if (nodeToSelect != null)
            {
                SelectedNode = nodeToSelect;
            }
        }
    }

    private void AttachFolderExpandedEvents(FolderItem folder)
    {
        folder.PropertyChanged += OnFolderExpandedChanged;
        foreach (var item in folder.Items)
        {
            if (item is FolderItem subFolder)
            {
                AttachFolderExpandedEvents(subFolder);
            }
        }
    }

    private void OnFolderExpandedChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavigationItem.IsExpanded) && sender is FolderItem folder)
        {
            var state = _stateService.Load();
            var normalizedPath = NormalizePath(folder.FullPath);
            var exists = state.Layout.ExpandedFolders.Any(p => string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase));

            if (folder.IsExpanded)
            {
                if (!exists)
                {
                    state.Layout.ExpandedFolders.Add(normalizedPath);
                    _stateService.Save(state);
                }
            }
            else
            {
                if (exists)
                {
                    state.Layout.ExpandedFolders.RemoveAll(p => string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase));
                    _stateService.Save(state);
                }
            }
        }
    }

    public void Dispose()
    {
        BackgroundImage?.Dispose();
        BackgroundImage = null;
        _navigationService.Dispose();
        _toastCts?.Dispose();
        _toastCts = null;
        GC.SuppressFinalize(this);
    }
}
