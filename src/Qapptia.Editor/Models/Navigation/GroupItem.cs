using System;
using System.Collections.ObjectModel;
using System.Linq;
using DynamicData;

namespace Qapptia.Editor.Models.Navigation;

/// <summary>
/// Abstracción base para cualquier nodo contenedor de elementos en el árbol de navegación.
/// Modificado para usar DynamicData y prevenir UI Starvation.
/// </summary>
public class GroupItem : NavigationItem, IDisposable
{
    private readonly IDisposable _cleanup;
    
    // La fuente de verdad masiva (SourceList O(1))
    public SourceList<NavigationItem> ItemsSource { get; } = new();

    // La colección ligera expuesta al VirtualizingStackPanel de Avalonia
    private readonly ReadOnlyObservableCollection<NavigationItem> _items = null!;
    public ReadOnlyObservableCollection<NavigationItem> Items => _items;

    public GroupKind Kind { get; set; } = GroupKind.Folder;

    public string IconKey { get; set; } = "IconFolder";

    private bool _hasFiles;
    public bool HasFiles
    {
        get => _hasFiles;
        set => SetProperty(ref _hasFiles, value);
    }

    public bool IsDimmed => IsEmptyConfirmed;
    public bool IsToday { get; set; }

    private bool _isScanCompleted;

    /// <summary>
    /// Indica que el indexador completó la inspección del contenido del nodo:
    /// la finalización propia en carpetas o el cierre de la indexación global en días del calendario.
    /// </summary>
    private bool _lastEmptyConfirmed;

    public bool IsScanCompleted
    {
        get => _isScanCompleted;
        set
        {
            if (SetProperty(ref _isScanCompleted, value))
            {
                if (value)
                {
                    IsLoading = false;
                }
                _lastEmptyConfirmed = IsEmptyConfirmed;
                OnPropertyChanged(nameof(IsEmptyConfirmed));
                OnPropertyChanged(nameof(IsDimmed));
            }
        }
    }

    // Todo nodo nace con chevron explorable; solo se confirma vacío tras la inspección del indexador
    public override bool IsEmptyConfirmed => IsScanCompleted && Items.Count == 0;

    private int _recursiveFileCount;
    public int RecursiveFileCount
    {
        get => _recursiveFileCount;
        private set
        {
            if (SetProperty(ref _recursiveFileCount, value))
            {
                OnPropertyChanged(nameof(FileCountDisplay));
            }
        }
    }

    public string FileCountDisplay => $"({RecursiveFileCount})";

    /// <summary>
    /// Asigna directamente el conteo recursivo del nodo (usado durante la construcción inicial del árbol).
    /// </summary>
    public void SetRecursiveFileCount(int count)
    {
        RecursiveFileCount = Math.Max(0, count);
    }

    /// <summary>
    /// Aplica un delta al conteo recursivo de archivos del nodo y lo propaga en cascada ascendente hacia sus ancestros.
    /// </summary>
    public void ApplyFileCountDelta(int delta)
    {
        if (delta == 0) return;
        RecursiveFileCount = Math.Max(0, RecursiveFileCount + delta);
        Parent?.ApplyFileCountDelta(delta);
    }

    private bool _isLoading;
    public bool IsLoading 
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public GroupItem()
    {
        // El empalme atómico y batching automático que elimina el UI Starvation (Plan Fase 3 / 4.1)
        _cleanup = ItemsSource.Connect()
            .Bind(out _items)
            .Subscribe(_ => 
            {
                foreach (var item in _items)
                {
                    if (item.Parent == null)
                    {
                        item.Parent = this;
                        if (item is GroupItem childGroup && childGroup.RecursiveFileCount > 0)
                        {
                            ApplyFileCountDelta(childGroup.RecursiveFileCount);
                        }
                    }
                }
                HasFiles = _items.Any(i => i is FileItem || (i is GroupItem g && g.HasFiles));
                
                bool currentEmptyConfirmed = IsEmptyConfirmed;
                if (_lastEmptyConfirmed != currentEmptyConfirmed)
                {
                    _lastEmptyConfirmed = currentEmptyConfirmed;
                    OnPropertyChanged(nameof(IsEmptyConfirmed));
                    OnPropertyChanged(nameof(IsDimmed));
                }
            });
    }

    public void Dispose()
    {
        _cleanup.Dispose();
        ItemsSource.Dispose();
        GC.SuppressFinalize(this);
    }
}
