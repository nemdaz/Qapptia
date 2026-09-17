using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using DynamicData;
using Qapptia.Editor.Models.Navigation;

namespace Qapptia.Editor.Services;

/// <summary>
/// Adaptador reactivo que proyecta una estructura jerárquica de GroupItem/FileItem
/// en una lista plana observable optimizada para VirtualizingStackPanel (ListBox).
/// Realiza inserciones y remociones en lote atómico O(1), eliminando el lag
/// de layout en colecciones masivas de archivos.
/// </summary>
public sealed class FlatTreeAdapter : IDisposable
{
    private readonly IEnumerable<GroupItem> _roots;
    private readonly Action<Action> _uiDispatcher;
    private readonly SourceList<NavigationItem> _sourceList = new();
    private readonly ReadOnlyObservableCollection<NavigationItem> _flatItems;
    private readonly IDisposable _bindingCleanup;
    private readonly HashSet<GroupItem> _trackedGroups = new();

    public ReadOnlyObservableCollection<NavigationItem> FlatItems => _flatItems;

    public FlatTreeAdapter(IEnumerable<GroupItem> roots, Action<Action>? uiDispatcher = null)
    {
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _uiDispatcher = uiDispatcher ?? (action => action());

        _bindingCleanup = _sourceList.Connect()
            .Bind(out _flatItems)
            .Subscribe();

        if (_roots is INotifyCollectionChanged incc)
        {
            incc.CollectionChanged += OnRootsCollectionChanged;
        }

        Rebuild();
    }

    public void Rebuild()
    {
        RunOnUi(() =>
        {
            UntrackAll();
            var all = new List<NavigationItem>();
            foreach (var root in _roots.ToList())
            {
                AddNodeAndDescendants(root, all);
            }

            _sourceList.Edit(inner =>
            {
                SynchronizeSubtree(inner, 0, inner.Count, all);
            });
        });
    }

    public static void ToggleExpand(GroupItem group)
    {
        if (group == null) return;
        group.IsExpanded = !group.IsExpanded;
    }

    public static void ExpandNode(GroupItem group)
    {
        if (group == null) return;
        group.IsExpanded = true;
    }

    public static void CollapseNode(GroupItem group)
    {
        if (group == null) return;
        group.IsExpanded = false;
    }

    private void AddNodeAndDescendants(GroupItem group, List<NavigationItem> result)
    {
        result.Add(group);
        TrackGroup(group);

        if (group.IsExpanded)
        {
            foreach (var child in group.Items.ToList())
            {
                if (child is GroupItem subGroup)
                {
                    AddNodeAndDescendants(subGroup, result);
                }
                else
                {
                    result.Add(child);
                }
            }
        }
    }

    private void TrackGroup(GroupItem group)
    {
        lock (_trackedGroups)
        {
            if (!_trackedGroups.Add(group)) return;
        }

        group.PropertyChanged += OnGroupPropertyChanged;
        ((INotifyCollectionChanged)group.Items).CollectionChanged += OnGroupItemsCollectionChanged;

        foreach (var child in group.Items.OfType<GroupItem>().ToList())
        {
            TrackGroup(child);
        }
    }

    private void UntrackGroup(GroupItem group)
    {
        lock (_trackedGroups)
        {
            if (!_trackedGroups.Remove(group)) return;
        }

        group.PropertyChanged -= OnGroupPropertyChanged;
        ((INotifyCollectionChanged)group.Items).CollectionChanged -= OnGroupItemsCollectionChanged;

        foreach (var child in group.Items.OfType<GroupItem>().ToList())
        {
            UntrackGroup(child);
        }
    }

    private void UntrackAll()
    {
        List<GroupItem> snapshot;
        lock (_trackedGroups)
        {
            snapshot = _trackedGroups.ToList();
            _trackedGroups.Clear();
        }

        foreach (var group in snapshot)
        {
            group.PropertyChanged -= OnGroupPropertyChanged;
            ((INotifyCollectionChanged)group.Items).CollectionChanged -= OnGroupItemsCollectionChanged;
        }
    }

    private void OnGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavigationItem.IsExpanded) && sender is GroupItem group)
        {
            RunOnUi(() =>
            {
                if (group.IsExpanded)
                {
                    ExpandSubtree(group);
                }
                else
                {
                    CollapseSubtree(group);
                }
            });
        }
    }

    private void ExpandSubtree(GroupItem group)
    {
        int index = _sourceList.Items.IndexOf(group);
        if (index < 0) return;

        var toInsert = new List<NavigationItem>();
        CollectVisibleDescendants(group, toInsert);

        if (toInsert.Count > 0)
        {
            _sourceList.Edit(inner => inner.InsertRange(toInsert, index + 1));
        }
    }

    private void CollapseSubtree(GroupItem group)
    {
        int index = _sourceList.Items.IndexOf(group);
        if (index < 0) return;

        var subtreeSet = GetSubtreeItemSet(group);
        int removeCount = 0;
        for (int i = index + 1; i < _sourceList.Count; i++)
        {
            var item = _sourceList.Items[i];
            if (subtreeSet.Contains(item) || IsDescendantOf(item, group))
            {
                removeCount++;
            }
            else
            {
                break;
            }
        }

        if (removeCount > 0)
        {
            _sourceList.Edit(inner => inner.RemoveRange(index + 1, removeCount));
        }
    }

    private static HashSet<NavigationItem> GetSubtreeItemSet(GroupItem group)
    {
        var set = new HashSet<NavigationItem>();
        void Collect(GroupItem g)
        {
            foreach (var child in g.Items)
            {
                set.Add(child);
                if (child is GroupItem sub)
                {
                    Collect(sub);
                }
            }
        }
        Collect(group);
        return set;
    }

    private void CollectVisibleDescendants(GroupItem parent, List<NavigationItem> result)
    {
        foreach (var child in parent.Items)
        {
            result.Add(child);
            if (child is GroupItem subGroup)
            {
                TrackGroup(subGroup);
                if (subGroup.IsExpanded)
                {
                    CollectVisibleDescendants(subGroup, result);
                }
            }
        }
    }

    private void RefreshGroupSubtree(GroupItem group)
    {
        int index = _sourceList.Items.IndexOf(group);
        if (index < 0) return;

        var subtreeSet = GetSubtreeItemSet(group);
        int removeCount = 0;
        for (int i = index + 1; i < _sourceList.Count; i++)
        {
            var item = _sourceList.Items[i];
            if (subtreeSet.Contains(item) || IsDescendantOf(item, group))
            {
                removeCount++;
            }
            else
            {
                break;
            }
        }

        var toInsert = new List<NavigationItem>();
        CollectVisibleDescendants(group, toInsert);

        foreach (var item in toInsert.OfType<GroupItem>())
        {
            TrackGroup(item);
        }

        _sourceList.Edit(inner =>
        {
            SynchronizeSubtree(inner, index + 1, removeCount, toInsert);
        });
    }

    private static void SynchronizeSubtree(IList<NavigationItem> inner, int startIndex, int currentCount, List<NavigationItem> desired)
    {
        static bool AreEqual(NavigationItem a, NavigationItem b) =>
            ReferenceEquals(a, b) || string.Equals(a.FullPath, b.FullPath, StringComparison.OrdinalIgnoreCase);

        // Si la lista ya es idéntica en longitud y elementos en el mismo orden, evitar ciclo de deselección
        if (currentCount == desired.Count)
        {
            bool identical = true;
            for (int i = 0; i < currentCount; i++)
            {
                if (!AreEqual(inner[startIndex + i], desired[i]))
                {
                    identical = false;
                    break;
                }
            }
            if (identical) return;
        }

        // 1. Remover elementos que ya no existan en desired
        int remainingCurrent = currentCount;
        for (int i = currentCount - 1; i >= 0; i--)
        {
            var item = inner[startIndex + i];
            if (!desired.Any(d => AreEqual(item, d)))
            {
                inner.RemoveAt(startIndex + i);
                remainingCurrent--;
            }
        }

        // 2. Insertar o mover para igualar desired
        for (int i = 0; i < desired.Count; i++)
        {
            var target = desired[i];
            int currentPos = startIndex + i;
            if (i < remainingCurrent && AreEqual(inner[currentPos], target))
            {
                continue;
            }

            int existingIndex = -1;
            for (int j = i + 1; j < remainingCurrent; j++)
            {
                if (AreEqual(inner[startIndex + j], target))
                {
                    existingIndex = startIndex + j;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                var item = inner[existingIndex];
                inner.RemoveAt(existingIndex);
                inner.Insert(currentPos, item);
            }
            else
            {
                inner.Insert(currentPos, target);
                remainingCurrent++;
            }
        }
    }

    private void OnGroupItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        GroupItem? group = null;
        lock (_trackedGroups)
        {
            foreach (var g in _trackedGroups)
            {
                if (ReferenceEquals(g.Items, sender))
                {
                    group = g;
                    break;
                }
            }
        }

        if (group == null || !group.IsExpanded) return;

        RunOnUi(() =>
        {
            HandleGroupItemsChanged(group, e);
        });
    }

    private void HandleGroupItemsChanged(GroupItem group, NotifyCollectionChangedEventArgs e)
    {
        int groupIndex = _sourceList.Items.IndexOf(group);
        if (groupIndex < 0) return;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems != null:
                HandleGroupItemsAdded(group, groupIndex, e);
                break;

            case NotifyCollectionChangedAction.Remove when e.OldItems != null:
                HandleGroupItemsRemoved(group, e);
                break;

            default:
                RefreshGroupSubtree(group);
                break;
        }
    }

    private void HandleGroupItemsAdded(GroupItem group, int groupIndex, NotifyCollectionChangedEventArgs e)
    {
        var newItems = e.NewItems?.OfType<NavigationItem>().ToList();
        if (newItems == null || newItems.Count == 0) return;

        int startIndex = e.NewStartingIndex >= 0 ? e.NewStartingIndex : group.Items.IndexOf(newItems[0]);
        int targetIndex;

        if (startIndex <= 0)
        {
            targetIndex = groupIndex + 1;
        }
        else
        {
            var prevSibling = group.Items[startIndex - 1];
            int prevIndex = _sourceList.Items.IndexOf(prevSibling);
            if (prevIndex < 0)
            {
                RefreshGroupSubtree(group);
                return;
            }

            targetIndex = prevIndex + 1;
            if (prevSibling is GroupItem prevGroup)
            {
                var prevSubtreeSet = GetSubtreeItemSet(prevGroup);
                while (targetIndex < _sourceList.Count)
                {
                    var candidate = _sourceList.Items[targetIndex];
                    if (prevSubtreeSet.Contains(candidate) || IsDescendantOf(candidate, prevGroup))
                    {
                        targetIndex++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        var toInsert = new List<NavigationItem>();
        foreach (var item in newItems)
        {
            if (item is GroupItem subGroup)
            {
                AddNodeAndDescendants(subGroup, toInsert);
            }
            else
            {
                toInsert.Add(item);
            }
        }

        if (toInsert.Count > 0)
        {
            _sourceList.Edit(inner => inner.InsertRange(toInsert, targetIndex));
        }
    }

    private void HandleGroupItemsRemoved(GroupItem group, NotifyCollectionChangedEventArgs e)
    {
        var oldItems = e.OldItems?.OfType<NavigationItem>().ToList();
        if (oldItems == null || oldItems.Count == 0) return;

        foreach (var item in oldItems)
        {
            int itemIndex = _sourceList.Items.IndexOf(item);
            if (itemIndex < 0) continue;

            if (item is GroupItem oldGroup)
            {
                UntrackGroup(oldGroup);
                var oldSubtreeSet = GetSubtreeItemSet(oldGroup);
                int removeCount = 1;
                while (itemIndex + removeCount < _sourceList.Count)
                {
                    var candidate = _sourceList.Items[itemIndex + removeCount];
                    if (oldSubtreeSet.Contains(candidate) || IsDescendantOf(candidate, oldGroup))
                    {
                        removeCount++;
                    }
                    else
                    {
                        break;
                    }
                }

                _sourceList.Edit(inner => inner.RemoveRange(itemIndex, removeCount));
            }
            else
            {
                _sourceList.Edit(inner => inner.RemoveAt(itemIndex));
            }
        }
    }

    private void OnRootsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Rebuild();
    }

    private static bool IsDescendantOf(NavigationItem item, GroupItem ancestor)
    {
        for (var current = item.Parent; current != null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }
        return false;
    }

    private void RunOnUi(Action action)
    {
        _uiDispatcher(action);
    }

    public void Dispose()
    {
        UntrackAll();
        if (_roots is INotifyCollectionChanged incc)
        {
            incc.CollectionChanged -= OnRootsCollectionChanged;
        }
        _bindingCleanup.Dispose();
        _sourceList.Dispose();
        GC.SuppressFinalize(this);
    }
}
