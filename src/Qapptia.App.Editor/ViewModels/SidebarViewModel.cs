using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using DynamicData;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qapptia.App.Editor.Common;
using Qapptia.Core.Abstractions;
using Qapptia.Core.Services;
using Qapptia.Editor.Models;
using Qapptia.Editor.Models.Navigation;
using Qapptia.Editor.Services;

namespace Qapptia.App.Editor.ViewModels;

/// <summary>
/// Modos de visualización del panel lateral de navegación.
/// </summary>
public enum SidebarViewMode
{
    Tree,
    Calendar
}

public partial class SidebarViewModel : ObservableObject, IDisposable
{
    private readonly INavigationService _navigationService;
    private readonly IEditorStateService _stateService;
    private readonly IShellService _shellService;
    private readonly string _savePath;
    private readonly Action<Action> _uiDispatcher;
    private bool _isLoading;
    private bool _reloadQueued;

    public ObservableCollection<GroupItem> TreeGroups { get; } = new();
    public ObservableCollection<GroupItem> CalendarGroups { get; } = new();
    public ObservableCollection<GroupItem> SidebarGroups { get; } = new();

    private readonly FlatTreeAdapter _treeFlatAdapter;
    private readonly FlatTreeAdapter _calendarFlatAdapter;
    private readonly HashSet<GroupItem> _trackedGroups = new();

    public ReadOnlyObservableCollection<NavigationItem> FlatTreeItems => _treeFlatAdapter.FlatItems;
    public ReadOnlyObservableCollection<NavigationItem> FlatCalendarItems => _calendarFlatAdapter.FlatItems;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTreeViewActive))]
    [NotifyPropertyChangedFor(nameof(IsCalendarViewActive))]
    [NotifyPropertyChangedFor(nameof(ActiveFlatItems))]
    private SidebarViewMode _viewMode = SidebarViewMode.Calendar;

    public bool IsTreeViewActive => ViewMode == SidebarViewMode.Tree;
    public bool IsCalendarViewActive => ViewMode == SidebarViewMode.Calendar;
    public ReadOnlyObservableCollection<NavigationItem> ActiveFlatItems => IsTreeViewActive ? FlatTreeItems : FlatCalendarItems;

    [ObservableProperty]
    private NavigationItem? _selectedNode;

    [ObservableProperty]
    private string? _activeFilePath;

    public event EventHandler<FileItem?>? FileSelected;
    public event Action<string, NotificationType>? ToastRequested;

    public SidebarViewModel(
        INavigationService navigationService,
        IEditorStateService stateService,
        string savePath,
        IShellService? shellService = null,
        Action<Action>? uiDispatcher = null)
    {
        _navigationService = navigationService;
        _stateService = stateService;
        _savePath = savePath;
        _shellService = shellService ?? NullShellService.Instance;
        _uiDispatcher = uiDispatcher ?? (action =>
        {
            try
            {
                if (Dispatcher.UIThread.CheckAccess())
                {
                    action();
                }
                else
                {
                    Dispatcher.UIThread.Post(action);
                }
            }
            catch
            {
                action();
            }
        });

        var state = _stateService.Load();
        _activeFilePath = state.Session.LastSelectedFile;
        if (Enum.TryParse<SidebarViewMode>(state.Layout.SidebarViewMode, true, out var parsedMode))
        {
            _viewMode = parsedMode;
        }
        else
        {
            _viewMode = SidebarViewMode.Calendar;
        }

        void HookCollection(ObservableCollection<GroupItem> col)
        {
            col.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (var item in e.NewItems.OfType<GroupItem>())
                    {
                        AttachGroupExpandedEvents(item);
                    }
                }
            };
        }

        HookCollection(SidebarGroups);
        HookCollection(TreeGroups);
        HookCollection(CalendarGroups);

        CalendarGroups.CollectionChanged += (s, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<GroupItem>())
                {
                    SidebarGroups.Remove(item);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null && ViewMode == SidebarViewMode.Calendar)
            {
                foreach (var item in e.NewItems.OfType<GroupItem>())
                {
                    if (!SidebarGroups.Contains(item))
                    {
                        SidebarGroups.Add(item);
                    }
                }
            }
        };

        _treeFlatAdapter = new FlatTreeAdapter(TreeGroups, _uiDispatcher);
        _calendarFlatAdapter = new FlatTreeAdapter(CalendarGroups, _uiDispatcher);

        ((INotifyCollectionChanged)_treeFlatAdapter.FlatItems).CollectionChanged += OnFlatItemsCollectionChanged;
        ((INotifyCollectionChanged)_calendarFlatAdapter.FlatItems).CollectionChanged += OnFlatItemsCollectionChanged;
    }

    private FolderItem? _cachedTreeRoot;
    private IReadOnlyList<GroupItem>? _cachedCalendarYears;

    [RelayCommand]
    public async Task SetViewMode(SidebarViewMode mode)
    {
        if (ViewMode == mode && (TreeGroups.Count > 0 || CalendarGroups.Count > 0 || SidebarGroups.Count > 0)) return;

        // Capturamos el archivo seleccionado antes de alternar
        var selectedFile = SelectedNode as FileItem;
        var selectedPath = selectedFile?.FullPath ?? ActiveFilePath ?? _stateService.Load().Session.LastSelectedFile;

        var state = _stateService.Load();
        state.Layout.SidebarViewMode = mode.ToString();
        _stateService.Save(state);

        if (_cachedTreeRoot != null && _cachedCalendarYears != null)
        {
            FileItem? targetFile = null;
            if (!string.IsNullOrEmpty(selectedPath))
            {
                ActiveFilePath = selectedPath;
                targetFile = EnsureFileInActiveTopology(selectedPath, mode);
            }

            ViewMode = mode;
            UpdateActiveSidebarGroups();

            if (targetFile != null)
            {
                if (AreAncestorsExpanded(targetFile))
                {
                    SelectedNode = targetFile;
                }
                else
                {
                    SelectedNode = null;
                }

                var parentDir = Path.GetDirectoryName(selectedPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    _ = Task.Run(() => _navigationService.RequestPriorityFolder(parentDir));
                }
            }
            else if (!string.IsNullOrEmpty(selectedPath))
            {
                var activeCollection = mode == SidebarViewMode.Tree ? TreeGroups : CalendarGroups;
                var fallback = _navigationService.FindNodeByPath(activeCollection, selectedPath) as FileItem ?? selectedFile;
                if (fallback != null && AreAncestorsExpanded(fallback))
                {
                    SelectedNode = fallback;
                }
                else
                {
                    SelectedNode = null;
                }
            }
            return;
        }

        ViewMode = mode;
        await LoadSidebarImagesCoreAsync(expandAncestorsForSelected: false);
    }

    private FileItem? EnsureFileInActiveTopology(string filePath, SidebarViewMode? targetMode = null)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

        var mode = targetMode ?? ViewMode;
        var activeCollection = mode == SidebarViewMode.Tree ? TreeGroups : CalendarGroups;
        var targetNode = _navigationService.FindNodeByPath(activeCollection, filePath) as FileItem;
        if (targetNode != null)
        {
            return targetNode;
        }

        var fileInfo = new FileInfo(filePath);
        var normPath = NavigationService.NormalizePath(fileInfo.FullName);

        if (mode == SidebarViewMode.Tree)
        {
            var parentPath = NavigationService.NormalizePath(Path.GetDirectoryName(filePath) ?? string.Empty);
            if (_navigationService.FindNodeByPath(TreeGroups, parentPath) is FolderItem parentFolder)
            {
                var existing = parentFolder.ItemsSource.Items.OfType<FileItem>()
                    .FirstOrDefault(f => string.Equals(NavigationService.NormalizePath(f.FullPath), normPath, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    return existing;
                }

                var singleFile = new FileItem
                {
                    Name = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    EffectiveDateUtc = Qapptia.Core.Services.ImageMetadataService.GetEffectiveDate(fileInfo),
                    Parent = parentFolder
                };

                NavigationService.InsertFileSorted(parentFolder, singleFile);
                return singleFile;
            }
        }
        else
        {
            var effDate = Qapptia.Core.Services.ImageMetadataService.GetEffectiveDate(fileInfo);
            var localDate = effDate.Kind == DateTimeKind.Utc ? effDate.ToLocalTime() : effDate;
            var dayNode = _navigationService.FindCalendarDay(localDate.Date) 
                ?? FindCalendarDayNode(localDate)
                ?? _navigationService.GetOrCreateCalendarDay(localDate.Date, CalendarGroups);
            if (dayNode != null)
            {
                var existing = dayNode.ItemsSource.Items.OfType<FileItem>()
                    .FirstOrDefault(f => string.Equals(NavigationService.NormalizePath(f.FullPath), normPath, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    return existing;
                }

                var singleFile = new FileItem
                {
                    Name = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    EffectiveDateUtc = effDate,
                    Parent = dayNode
                };

                NavigationService.InsertFileSorted(dayNode, singleFile);
                return singleFile;
            }
        }

        return null;
    }

    private CalendarGroupItem? FindCalendarDayNode(DateTime localDate)
    {
        var indexedDay = _navigationService.FindCalendarDay(localDate.Date);
        if (indexedDay != null) return indexedDay;

        int targetYear = localDate.Year;
        int targetMonth = localDate.Month;

        var sourceCollection = CalendarGroups.Count > 0 ? CalendarGroups : SidebarGroups;
        var yearGroup = sourceCollection.OfType<CalendarGroupItem>().FirstOrDefault(y => y.Year == targetYear);
        if (yearGroup == null) return null;

        var monthGroup = yearGroup.ItemsSource.Items.OfType<CalendarGroupItem>().FirstOrDefault(m => m.Month == targetMonth);
        if (monthGroup == null) return null;

        foreach (var weekNode in monthGroup.ItemsSource.Items.OfType<CalendarGroupItem>())
        {
            var dayGroup = weekNode.ItemsSource.Items.OfType<CalendarGroupItem>().FirstOrDefault(d => d.Date.HasValue && d.Date.Value.Date == localDate.Date);
            if (dayGroup != null)
            {
                return dayGroup;
            }
        }
        return null;
    }

    private void ApplyCachedView()
    {
        foreach (var group in _trackedGroups)
        {
            group.PropertyChanged -= OnGroupExpandedChanged;
            ((INotifyCollectionChanged)group.Items).CollectionChanged -= OnGroupItemsCollectionChanged;
        }
        _trackedGroups.Clear();

        TreeGroups.Clear();
        if (_cachedTreeRoot != null)
        {
            TreeGroups.Add(_cachedTreeRoot);
        }

        CalendarGroups.Clear();
        if (_cachedCalendarYears != null)
        {
            foreach (var yearGroup in _cachedCalendarYears)
            {
                CalendarGroups.Add(yearGroup);
            }
        }

        UpdateActiveSidebarGroups();
    }

    private void UpdateActiveSidebarGroups()
    {
        SidebarGroups.Clear();
        if (ViewMode == SidebarViewMode.Tree)
        {
            if (TreeGroups.Count > 0)
            {
                SidebarGroups.Add(TreeGroups[0]);
            }
        }
        else
        {
            foreach (var yearGroup in CalendarGroups)
            {
                SidebarGroups.Add(yearGroup);
            }
        }

        _treeFlatAdapter.Rebuild();
        _calendarFlatAdapter.Rebuild();
    }

    [RelayCommand]
    public static void ToggleGroupExpansion(GroupItem? group)
    {
        if (group == null) return;
        FlatTreeAdapter.ToggleExpand(group);
    }

    [RelayCommand]
    public void OpenFile(FileItem? item)
    {
        var target = item ?? SelectedNode as FileItem;
        if (target == null || string.IsNullOrWhiteSpace(target.FullPath)) return;

        if (!File.Exists(target.FullPath))
        {
            ToastRequested?.Invoke(Constants.ToastFileNotFound, NotificationType.Warning);
            return;
        }

        bool success = _shellService.OpenFile(target.FullPath);
        if (!success)
        {
            ToastRequested?.Invoke(Constants.ToastOpenFileError, NotificationType.Error);
        }
    }

    [RelayCommand]
    public void ShowInFolder(FileItem? item)
    {
        var target = item ?? SelectedNode as FileItem;
        if (target == null || string.IsNullOrWhiteSpace(target.FullPath)) return;

        var parentDir = Path.GetDirectoryName(target.FullPath);
        if (!File.Exists(target.FullPath) && (string.IsNullOrEmpty(parentDir) || !Directory.Exists(parentDir)))
        {
            ToastRequested?.Invoke(Constants.ToastFolderNotFound, NotificationType.Warning);
            return;
        }

        bool success = _shellService.ShowInFolder(target.FullPath);
        if (!success)
        {
            ToastRequested?.Invoke(Constants.ToastShowInFolderError, NotificationType.Error);
        }
    }

    public void StartWatching(Action onFolderChanged)
    {
        _navigationService.StartWatching(_savePath, OnFileWatcherEvent, onFolderChanged);
    }

    private void OnFileWatcherEvent(string fullPath, WatcherChangeTypes changeType)
    {
        _uiDispatcher(() =>
        {
            var preservedSelection = SelectedNode;
            var preservedPath = (SelectedNode as FileItem)?.FullPath ?? ActiveFilePath;

            if (changeType == WatcherChangeTypes.Created)
            {
                bool handled = InjectCreatedFile(fullPath);
                if (!handled)
                {
                    // Si la carpeta contenedora no existía aún en el árbol, recargar topología
                    _ = LoadSidebarImagesAsync();
                }

                if (SelectedNode == null && (!string.IsNullOrEmpty(preservedPath) || preservedSelection != null))
                {
                    var activeCollection = ViewMode == SidebarViewMode.Tree ? TreeGroups : CalendarGroups;
                    SelectedNode = preservedSelection ?? (preservedPath != null ? _navigationService.FindNodeByPath(activeCollection, preservedPath) : null);
                }
            }
            else if (changeType == WatcherChangeTypes.Deleted)
            {
                RemoveDeletedFile(fullPath);
            }
        });
    }

    public bool InjectCreatedFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;
        if (!NavigationService.IsNavigablePath(filePath)) return false;

        var preservedSelection = SelectedNode;
        var preservedPath = (SelectedNode as FileItem)?.FullPath ?? ActiveFilePath;

        var fileInfo = new FileInfo(filePath);
        var effDate = Qapptia.Core.Services.ImageMetadataService.GetEffectiveDate(fileInfo);

        bool handledInTree = false;

        // 1. Inyección inmediata en Árbol de Carpetas
        var parentPath = NavigationService.NormalizePath(Path.GetDirectoryName(filePath) ?? string.Empty);
        if (_navigationService.FindNodeByPath(TreeGroups, parentPath) is FolderItem parentFolder)
        {
            handledInTree = true;
            var alreadyExists = parentFolder.ItemsSource.Items.OfType<FileItem>()
                .Any(f => string.Equals(NavigationService.NormalizePath(f.FullPath), NavigationService.NormalizePath(filePath), StringComparison.OrdinalIgnoreCase));
            if (!alreadyExists)
            {
                var treeFile = new FileItem
                {
                    Name = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    EffectiveDateUtc = effDate,
                    Parent = parentFolder
                };
                NavigationService.InsertFileSorted(parentFolder, treeFile);
            }
        }

        // 2. Inyección inmediata en Árbol de Calendario (desacoplada de carpetas físicas en disco)
        bool handledInCalendar = false;
        var localDate = effDate.Kind == DateTimeKind.Utc ? effDate.ToLocalTime() : effDate;
        var dayNode = _navigationService.FindCalendarDay(localDate.Date) 
            ?? FindCalendarDayNode(localDate)
            ?? _navigationService.GetOrCreateCalendarDay(localDate.Date, CalendarGroups);
        if (dayNode != null)
        {
            handledInCalendar = true;
            var alreadyExists = dayNode.ItemsSource.Items.OfType<FileItem>()
                .Any(f => string.Equals(NavigationService.NormalizePath(f.FullPath), NavigationService.NormalizePath(filePath), StringComparison.OrdinalIgnoreCase));
            if (!alreadyExists)
            {
                var calFile = new FileItem
                {
                    Name = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    EffectiveDateUtc = effDate,
                    Parent = dayNode
                };
                NavigationService.InsertFileSorted(dayNode, calFile);
            }
        }

        // 3. Garantizar que si había un archivo previamente seleccionado, continúe seleccionado
        if (SelectedNode == null && (!string.IsNullOrEmpty(preservedPath) || preservedSelection != null))
        {
            var activeCollection = ViewMode == SidebarViewMode.Tree ? TreeGroups : CalendarGroups;
            SelectedNode = preservedSelection ?? (preservedPath != null ? _navigationService.FindNodeByPath(activeCollection, preservedPath) : null);
        }

        return handledInTree || (ViewMode == SidebarViewMode.Calendar && handledInCalendar);
    }

    public void RemoveDeletedFile(string filePath)
    {
        var normTarget = NavigationService.NormalizePath(filePath);

        // 1. Remover del Árbol
        var parentPath = NavigationService.NormalizePath(Path.GetDirectoryName(filePath) ?? string.Empty);
        if (_navigationService.FindNodeByPath(TreeGroups, parentPath) is FolderItem parentFolder)
        {
            var itemToRemove = parentFolder.ItemsSource.Items.OfType<FileItem>()
                .FirstOrDefault(f => string.Equals(NavigationService.NormalizePath(f.FullPath), normTarget, StringComparison.OrdinalIgnoreCase));
            if (itemToRemove != null)
            {
                parentFolder.ItemsSource.Remove(itemToRemove);
            }
        }

        // 2. Remover del Calendario
        foreach (var year in CalendarGroups)
        {
            var itemToRemove = FindFileRecursive(year, normTarget);
            if (itemToRemove != null && itemToRemove.Parent is CalendarGroupItem calGroup)
            {
                calGroup.ItemsSource.Remove(itemToRemove);
                break;
            }
        }
    }

    partial void OnSelectedNodeChanged(NavigationItem? value)
    {
        if (value is FileItem file)
        {
            ActiveFilePath = file.FullPath;
            var state = _stateService.Load();
            state.Session.LastSelectedFile = file.FullPath;
            _stateService.Save(state);
        }
        else if (value is GroupItem)
        {
            // Un GroupItem no debe deseleccionar el archivo activo si este está visible
            Dispatcher.UIThread.Post(() =>
            {
                if (SelectedNode is GroupItem && !string.IsNullOrEmpty(ActiveFilePath))
                {
                    var match = ActiveFlatItems.OfType<FileItem>().FirstOrDefault(f => string.Equals(NavigationService.NormalizePath(f.FullPath), NavigationService.NormalizePath(ActiveFilePath), StringComparison.OrdinalIgnoreCase));
                    if (match != null && AreAncestorsExpanded(match))
                    {
                        SelectedNode = match;
                    }
                    else
                    {
                        SelectedNode = null;
                    }
                }
            }, DispatcherPriority.Background);
        }
        else if (value == null && !string.IsNullOrEmpty(ActiveFilePath))
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (SelectedNode == null && !string.IsNullOrEmpty(ActiveFilePath))
                {
                    var activeCollection = ViewMode == SidebarViewMode.Tree ? TreeGroups : CalendarGroups;
                    var match = _navigationService.FindNodeByPath(activeCollection, ActiveFilePath) as FileItem
                        ?? ActiveFlatItems.OfType<FileItem>().FirstOrDefault(f => string.Equals(NavigationService.NormalizePath(f.FullPath), NavigationService.NormalizePath(ActiveFilePath), StringComparison.OrdinalIgnoreCase));
                    if (match != null && AreAncestorsExpanded(match))
                    {
                        SelectedNode = match;
                    }
                }
            }, DispatcherPriority.Background);
        }

        FileSelected?.Invoke(this, value as FileItem);
    }

    [RelayCommand]
    public async Task LoadSidebarImagesAsync()
    {
        await LoadSidebarImagesCoreAsync(expandAncestorsForSelected: false);
    }

    public async Task LoadSidebarImagesCoreAsync(bool expandAncestorsForSelected = false)
    {
        if (_isLoading)
        {
            _reloadQueued = true;
            return;
        }
        _isLoading = true;

        try
        {
            var savePath = _savePath;
            if (!Directory.Exists(savePath))
            {
                SidebarGroups.Clear();
                return;
            }

            var state = _stateService.Load();
            var selectedPath = (SelectedNode as FileItem)?.FullPath ?? ActiveFilePath ?? state.Session.LastSelectedFile;

            // FASE 1: Construcción paralela de topologías para habilitar Swap O(1) instantáneo
            var effectiveExpandedFolders = new HashSet<string>(state.Layout.ExpandedFolders, StringComparer.OrdinalIgnoreCase);
            var effectiveExpandedCalendar = new HashSet<string>(state.Layout.ExpandedCalendarGroups, StringComparer.OrdinalIgnoreCase);

            bool shouldExpandTreeAncestors = expandAncestorsForSelected || effectiveExpandedFolders.Count == 0;
            bool shouldExpandCalendarAncestors = expandAncestorsForSelected || effectiveExpandedCalendar.Count == 0;

            if (!string.IsNullOrEmpty(selectedPath) && File.Exists(selectedPath))
            {
                if (shouldExpandTreeAncestors)
                {
                    foreach (var dir in GetAncestorDirectories(selectedPath, savePath))
                    {
                        effectiveExpandedFolders.Add(dir);
                    }
                }

                if (shouldExpandCalendarAncestors)
                {
                    foreach (var uri in GetCalendarAncestorUris(selectedPath))
                    {
                        effectiveExpandedCalendar.Add(uri);
                    }
                }
            }

            var treeTask = _navigationService.BuildTreeAsync(savePath, effectiveExpandedFolders.ToList());
            var calendarTask = _navigationService.BuildCalendarTreeAsync(savePath, effectiveExpandedCalendar.ToList(), Constants.CalendarWeekLabel);

            await Task.WhenAll(treeTask, calendarTask);

            _cachedTreeRoot = await treeTask;
            _cachedCalendarYears = await calendarTask;

            if (_cachedTreeRoot != null)
            {
                AttachGroupExpandedEvents(_cachedTreeRoot);
            }

            if (_cachedCalendarYears != null)
            {
                foreach (var yearGroup in _cachedCalendarYears)
                {
                    AttachGroupExpandedEvents(yearGroup);
                }
            }

            FileItem? startupTargetFile = null;
            if (!string.IsNullOrEmpty(selectedPath))
            {
                ActiveFilePath = selectedPath;
                startupTargetFile = EnsureFileInActiveTopology(selectedPath, ViewMode);
                var otherMode = ViewMode == SidebarViewMode.Tree ? SidebarViewMode.Calendar : SidebarViewMode.Tree;
                EnsureFileInActiveTopology(selectedPath, otherMode);

                if (startupTargetFile != null)
                {
                    bool shouldExpand = ViewMode == SidebarViewMode.Tree ? shouldExpandTreeAncestors : shouldExpandCalendarAncestors;
                    if (shouldExpand)
                    {
                        ExpandAncestorsForFile(startupTargetFile, ViewMode);
                    }
                }
            }

            ApplyCachedView();

            if (!string.IsNullOrEmpty(selectedPath))
            {
                var activeCollection = ViewMode == SidebarViewMode.Tree ? TreeGroups : CalendarGroups;
                var targetNode = startupTargetFile ?? _navigationService.FindNodeByPath(activeCollection, selectedPath) as FileItem;
                if (targetNode != null)
                {
                    bool shouldExpand = ViewMode == SidebarViewMode.Tree ? shouldExpandTreeAncestors : shouldExpandCalendarAncestors;
                    if (shouldExpand)
                    {
                        ExpandAncestorsForFile(targetNode, ViewMode);
                    }

                    if (AreAncestorsExpanded(targetNode))
                    {
                        SelectedNode = targetNode;
                    }
                    else
                    {
                        SelectedNode = null;
                    }
                }
            }
            
            // FASE 2: Iniciar Indexador Dual-Channel en background alimentando ambas estructuras
            var normSelected = !string.IsNullOrEmpty(selectedPath) ? NavigationService.NormalizePath(selectedPath) : null;
            bool selectedMatched = false;

            Action<FileItem>? onFileDispatched = file =>
            {
                var targetPath = ActiveFilePath ?? normSelected;
                if ((SelectedNode == null || !selectedMatched) && targetPath != null)
                {
                    bool isForActiveView = ViewMode == SidebarViewMode.Calendar
                        ? (file.Parent is CalendarGroupItem || file.Parent == null)
                        : (file.Parent is FolderItem || file.Parent == null);

                    if (isForActiveView && string.Equals(NavigationService.NormalizePath(file.FullPath), NavigationService.NormalizePath(targetPath), StringComparison.OrdinalIgnoreCase))
                    {
                        var activeCollection = ViewMode == SidebarViewMode.Tree ? TreeGroups : CalendarGroups;
                        var targetNode = _navigationService.FindNodeByPath(activeCollection, file.FullPath) ?? file;
                        selectedMatched = true;
                        bool shouldExpand = ViewMode == SidebarViewMode.Tree ? shouldExpandTreeAncestors : shouldExpandCalendarAncestors;
                        if (shouldExpand)
                        {
                            ExpandAncestorsForFile(targetNode as FileItem ?? file);
                            SelectedNode = targetNode;
                        }
                        else if (AreAncestorsExpanded(targetNode))
                        {
                            SelectedNode = targetNode;
                        }
                    }
                }
            };

            _navigationService.StartIndexer(savePath, CalendarGroups, _cachedTreeRoot, _uiDispatcher, onFileDispatched);
            
            // Priorizar la carpeta que contiene el archivo seleccionado si existe (en background)
            if (!string.IsNullOrEmpty(selectedPath) && File.Exists(selectedPath))
            {
                var parentDir = Path.GetDirectoryName(selectedPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    _ = Task.Run(() => _navigationService.RequestPriorityFolder(parentDir));
                }
            }

            // Reconectar estado previo: Enviar las carpetas que ya venían expandidas al tope de Prioridad (en background)
            if (_cachedTreeRoot != null && _cachedTreeRoot.IsExpanded)
            {
                _ = Task.Run(() => _navigationService.RequestPriorityFolder(savePath));
            }

            foreach (var folder in state.Layout.ExpandedFolders)
            {
                if (!string.Equals(NavigationService.NormalizePath(folder), NavigationService.NormalizePath(savePath), StringComparison.OrdinalIgnoreCase))
                {
                    _ = Task.Run(() => _navigationService.RequestPriorityFolder(folder));
                }
            }
        }
        finally
        {
            _isLoading = false;
            if (_reloadQueued)
            {
                _reloadQueued = false;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    _uiDispatcher(() => _ = LoadSidebarImagesAsync());
                });
            }
        }
    }

    public NavigationItem? FindNodeByPath(string path)
    {
        return _navigationService.FindNodeByPath(SidebarGroups, NavigationService.NormalizePath(path));
    }

    private void AttachGroupExpandedEvents(GroupItem group)
    {
        if (!_trackedGroups.Add(group)) return;

        group.PropertyChanged += OnGroupExpandedChanged;
        foreach (var item in group.Items)
        {
            if (item is GroupItem subGroup)
            {
                AttachGroupExpandedEvents(subGroup);
            }
        }
        ((INotifyCollectionChanged)group.Items).CollectionChanged += OnGroupItemsCollectionChanged;
    }

    private void OnGroupItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<GroupItem>())
            {
                AttachGroupExpandedEvents(item);
            }

            GroupItem? senderGroup = null;
            foreach (var g in _trackedGroups)
            {
                if (ReferenceEquals(g.Items, sender))
                {
                    senderGroup = g;
                    break;
                }
            }

            if (senderGroup != null && senderGroup.IsExpanded)
            {
                TrySelectActiveFileInGroup(senderGroup);
            }
        }
    }

    private void OnGroupExpandedChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavigationItem.IsExpanded) && sender is GroupItem group)
        {
            if (group.IsExpanded)
            {
                if (!group.IsScanCompleted && (group.Kind == GroupKind.Folder || group.Kind == GroupKind.Day))
                {
                    group.IsLoading = true;
                }

                if (group.Kind == GroupKind.Folder)
                {
                    _ = Task.Run(() => _navigationService.RequestPriorityFolder(group.FullPath));
                }

                TrySelectActiveFileInGroup(group);
            }
            else
            {
                // Si el nodo se colapsa, apagar el spinner si estuviese activo
                group.IsLoading = false;
            }

            var state = _stateService.Load();
            if (ViewMode == SidebarViewMode.Tree)
            {
                var normalizedPath = NavigationService.NormalizePath(group.FullPath);
                var exists = state.Layout.ExpandedFolders.Any(p => string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase));

                if (group.IsExpanded && !exists)
                {
                    state.Layout.ExpandedFolders.Add(normalizedPath);
                    _stateService.Save(state);
                }
                else if (!group.IsExpanded && exists)
                {
                    state.Layout.ExpandedFolders.RemoveAll(p => string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase));
                    _stateService.Save(state);
                }
            }
            else
            {
                var uri = group.FullPath;
                var exists = state.Layout.ExpandedCalendarGroups.Any(p => string.Equals(p, uri, StringComparison.OrdinalIgnoreCase));

                if (group.IsExpanded && !exists)
                {
                    state.Layout.ExpandedCalendarGroups.Add(uri);
                    _stateService.Save(state);
                }
                else if (!group.IsExpanded && exists)
                {
                    state.Layout.ExpandedCalendarGroups.RemoveAll(p => string.Equals(p, uri, StringComparison.OrdinalIgnoreCase));
                    _stateService.Save(state);
                }
            }
        }
    }

    private void OnFlatItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ActiveFilePath) && (SelectedNode == null || SelectedNode is GroupItem || (SelectedNode is FileItem f && !string.Equals(NavigationService.NormalizePath(f.FullPath), NavigationService.NormalizePath(ActiveFilePath), StringComparison.OrdinalIgnoreCase))))
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!string.IsNullOrEmpty(ActiveFilePath) && (SelectedNode == null || SelectedNode is GroupItem || (SelectedNode is FileItem currentFile && !string.Equals(NavigationService.NormalizePath(currentFile.FullPath), NavigationService.NormalizePath(ActiveFilePath), StringComparison.OrdinalIgnoreCase))))
                {
                    var match = ActiveFlatItems.OfType<FileItem>().FirstOrDefault(item => string.Equals(NavigationService.NormalizePath(item.FullPath), NavigationService.NormalizePath(ActiveFilePath), StringComparison.OrdinalIgnoreCase));
                    if (match != null && AreAncestorsExpanded(match))
                    {
                        SelectedNode = match;
                    }
                }
            }, DispatcherPriority.Background);
        }
    }

    private void TrySelectActiveFileInGroup(GroupItem group)
    {
        if (string.IsNullOrEmpty(ActiveFilePath) || !File.Exists(ActiveFilePath)) return;

        bool isForCurrentView = ViewMode == SidebarViewMode.Calendar
            ? (group is CalendarGroupItem)
            : (group is FolderItem);

        if (!isForCurrentView) return;

        // 1. Si el archivo ya está en la colección de items del grupo o descendientes
        var found = FindFileRecursive(group, ActiveFilePath);
        if (found != null)
        {
            if (AreAncestorsExpanded(found))
            {
                _uiDispatcher(() =>
                {
                    if (AreAncestorsExpanded(found))
                    {
                        SelectedNode = found;
                    }
                });
            }
            return;
        }

        // 2. Si este grupo es el contenedor directo del archivo activo, inyectarlo de inmediato
        if (IsDirectParentOfActiveFile(group, ActiveFilePath))
        {
            var injected = EnsureFileInActiveTopology(ActiveFilePath);
            if (injected != null && AreAncestorsExpanded(injected))
            {
                _uiDispatcher(() =>
                {
                    if (AreAncestorsExpanded(injected))
                    {
                        SelectedNode = injected;
                    }
                });
            }
        }
    }

    private static FileItem? FindFileRecursive(GroupItem group, string path)
    {
        var normTarget = NavigationService.NormalizePath(path);
        var children = group.ItemsSource.Items.Count > 0 ? (IReadOnlyList<NavigationItem>)group.ItemsSource.Items : group.Items;
        for (int i = 0; i < children.Count; i++)
        {
            var item = children[i];
            if (item is FileItem f && string.Equals(NavigationService.NormalizePath(f.FullPath), normTarget, StringComparison.OrdinalIgnoreCase))
            {
                return f;
            }
            if (item is GroupItem subGroup)
            {
                var found = FindFileRecursive(subGroup, normTarget);
                if (found != null) return found;
            }
        }
        return null;
    }

    private bool IsDirectParentOfActiveFile(GroupItem group, string path)
    {
        if (ViewMode == SidebarViewMode.Tree && group is FolderItem folder)
        {
            var parentDir = Path.GetDirectoryName(path);
            return !string.IsNullOrEmpty(parentDir) && string.Equals(NavigationService.NormalizePath(parentDir), NavigationService.NormalizePath(folder.FullPath), StringComparison.OrdinalIgnoreCase);
        }
        if (ViewMode == SidebarViewMode.Calendar && group is CalendarGroupItem calGroup && calGroup.Kind == GroupKind.Day && calGroup.Date.HasValue)
        {
            var effDate = Qapptia.Core.Services.ImageMetadataService.GetEffectiveDate(path);
            var localDate = effDate.Kind == DateTimeKind.Utc ? effDate.ToLocalTime() : effDate;
            return calGroup.Date.Value.Date == localDate.Date;
        }
        return false;
    }

    public void Dispose()
    {
        ((INotifyCollectionChanged)_treeFlatAdapter.FlatItems).CollectionChanged -= OnFlatItemsCollectionChanged;
        ((INotifyCollectionChanged)_calendarFlatAdapter.FlatItems).CollectionChanged -= OnFlatItemsCollectionChanged;

        foreach (var group in _trackedGroups)
        {
            group.PropertyChanged -= OnGroupExpandedChanged;
            ((INotifyCollectionChanged)group.Items).CollectionChanged -= OnGroupItemsCollectionChanged;
        }
        _trackedGroups.Clear();

        _treeFlatAdapter.Dispose();
        _calendarFlatAdapter.Dispose();
        _navigationService.Dispose();
        GC.SuppressFinalize(this);
    }

    private static bool AreAncestorsExpanded(NavigationItem? item)
    {
        if (item == null) return false;
        for (var current = item.Parent; current != null; current = current.Parent)
        {
            if (!current.IsExpanded) return false;
        }
        return true;
    }

    private void ExpandAncestorsForFile(FileItem file, SidebarViewMode? targetMode = null)
    {
        var mode = targetMode ?? ViewMode;
        GroupItem? parentNode = null;

        if (mode == SidebarViewMode.Tree)
        {
            if (file.Parent is FolderItem folder)
            {
                parentNode = folder;
            }
            else
            {
                var parentPath = Path.GetDirectoryName(file.FullPath);
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var sourceCollection = TreeGroups.Count > 0 ? TreeGroups : SidebarGroups;
                    parentNode = _navigationService.FindNodeByPath(sourceCollection, parentPath) as GroupItem;
                }
            }
        }
        else if (mode == SidebarViewMode.Calendar)
        {
            if (file.Parent is CalendarGroupItem calGroup && calGroup.Kind == GroupKind.Day)
            {
                parentNode = calGroup;
            }
            else
            {
                var rawDate = file.EffectiveDateUtc;
                if (rawDate == DateTime.MinValue && File.Exists(file.FullPath))
                {
                    rawDate = Qapptia.Core.Services.ImageMetadataService.GetEffectiveDate(file.FullPath);
                }
                var localDate = rawDate.Kind == DateTimeKind.Utc ? rawDate.ToLocalTime() : rawDate;
                parentNode = _navigationService.FindCalendarDay(localDate.Date) 
                    ?? FindCalendarDayNode(localDate)
                    ?? _navigationService.GetOrCreateCalendarDay(localDate.Date, CalendarGroups);
            }
        }

        if (parentNode != null)
        {
            var chain = new List<GroupItem>();
            for (var current = parentNode; current != null; current = current.Parent)
            {
                chain.Add(current);
            }
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                chain[i].IsExpanded = true;
            }
        }
    }

    private static List<string> GetAncestorDirectories(string filePath, string rootPath)
    {
        var list = new List<string>();
        var normRoot = NavigationService.NormalizePath(rootPath).TrimEnd('/');
        var dir = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(dir))
        {
            var normDir = NavigationService.NormalizePath(dir).TrimEnd('/');
            list.Add(normDir);
            if (string.Equals(normDir, normRoot, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return list;
    }

    public static List<string> GetCalendarAncestorUris(string filePath)
    {
        var uris = new List<string>();
        if (!File.Exists(filePath)) return uris;

        var rawDate = Qapptia.Core.Services.ImageMetadataService.GetEffectiveDate(filePath);
        var localDate = rawDate.Kind == DateTimeKind.Utc ? rawDate.ToLocalTime() : rawDate;
        int year = localDate.Year;
        int month = localDate.Month;
        int weekNum = System.Globalization.ISOWeek.GetWeekOfYear(localDate.Date);

        uris.Add($"cal://{year}");
        uris.Add($"cal://{year}/{month:D2}");
        uris.Add($"cal://{year}/{month:D2}/w{weekNum:D2}");
        uris.Add($"cal://{year}/{month:D2}/w{weekNum:D2}/{localDate:yyyy-MM-dd}");

        return uris;
    }
}
