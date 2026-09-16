using System;
using System.Collections.ObjectModel;
using System.Linq;
using DynamicData;
using FluentAssertions;
using Qapptia.Editor.Models.Navigation;
using Qapptia.Editor.Services;
using Xunit;

namespace Qapptia.Editor.Tests.Services;

public sealed class FlatTreeAdapterTests
{
    [Fact]
    public void InitialRootsShouldBeInFlatItemsWhenCollapsed()
    {
        var root = new FolderItem { Name = "Root", IsExpanded = false };
        root.ItemsSource.Add(new FileItem { Name = "file1.png" });

        var roots = new ObservableCollection<GroupItem> { root };
        using var adapter = new FlatTreeAdapter(roots);

        adapter.FlatItems.Should().HaveCount(1);
        adapter.FlatItems[0].Should().Be(root);
    }

    [Fact]
    public void ExpandingRootShouldInsertChildrenInFlatItems()
    {
        var root = new FolderItem { Name = "Root", IsExpanded = false };
        var file1 = new FileItem { Name = "file1.png", Parent = root };
        var file2 = new FileItem { Name = "file2.png", Parent = root };
        root.ItemsSource.AddRange(new[] { file1, file2 });

        var roots = new ObservableCollection<GroupItem> { root };
        using var adapter = new FlatTreeAdapter(roots);

        adapter.FlatItems.Should().HaveCount(1);

        root.IsExpanded = true;

        adapter.FlatItems.Should().HaveCount(3);
        adapter.FlatItems[0].Should().Be(root);
        adapter.FlatItems[1].Should().Be(file1);
        adapter.FlatItems[2].Should().Be(file2);
    }

    [Fact]
    public void CollapsingRootShouldRemoveDescendantsFromFlatItems()
    {
        var root = new FolderItem { Name = "Root", IsExpanded = true };
        var file1 = new FileItem { Name = "file1.png", Parent = root };
        var file2 = new FileItem { Name = "file2.png", Parent = root };
        root.ItemsSource.AddRange(new[] { file1, file2 });

        var roots = new ObservableCollection<GroupItem> { root };
        using var adapter = new FlatTreeAdapter(roots);

        adapter.FlatItems.Should().HaveCount(3);

        root.IsExpanded = false;

        adapter.FlatItems.Should().HaveCount(1);
        adapter.FlatItems[0].Should().Be(root);
    }

    [Fact]
    public void AddingItemsToExpandedGroupShouldReflectAtomicallyInFlatItems()
    {
        var root = new FolderItem { Name = "Root", IsExpanded = true };
        var roots = new ObservableCollection<GroupItem> { root };
        using var adapter = new FlatTreeAdapter(roots);

        adapter.FlatItems.Should().HaveCount(1);

        var files = Enumerable.Range(1, 500)
            .Select(i => new FileItem { Name = $"shot_{i:D3}.png", Parent = root })
            .ToList();

        root.ItemsSource.AddRange(files);

        adapter.FlatItems.Should().HaveCount(501);
        adapter.FlatItems[0].Should().Be(root);
        adapter.FlatItems[1].Name.Should().Be("shot_001.png");
        adapter.FlatItems[500].Name.Should().Be("shot_500.png");
    }

    [Fact]
    public void NestedFolderExpansionShouldMaintainHierarchicalOrder()
    {
        var root = new FolderItem { Name = "Root", IsExpanded = true };
        var subFolder = new FolderItem { Name = "SubFolder", Parent = root, IsExpanded = false };
        var rootFile = new FileItem { Name = "root_file.png", Parent = root };
        var subFile = new FileItem { Name = "sub_file.png", Parent = subFolder };

        root.ItemsSource.AddRange(new NavigationItem[] { subFolder, rootFile });
        subFolder.ItemsSource.Add(subFile);

        var roots = new ObservableCollection<GroupItem> { root };
        using var adapter = new FlatTreeAdapter(roots);

        // root + subFolder + rootFile
        adapter.FlatItems.Should().HaveCount(3);
        adapter.FlatItems[0].Should().Be(root);
        adapter.FlatItems[1].Should().Be(subFolder);
        adapter.FlatItems[2].Should().Be(rootFile);

        // Expand subFolder: sub_file should be inserted between subFolder and rootFile
        subFolder.IsExpanded = true;

        adapter.FlatItems.Should().HaveCount(4);
        adapter.FlatItems[0].Should().Be(root);
        adapter.FlatItems[1].Should().Be(subFolder);
        adapter.FlatItems[2].Should().Be(subFile);
        adapter.FlatItems[3].Should().Be(rootFile);

        // Collapse subFolder: sub_file should be removed
        subFolder.IsExpanded = false;

        adapter.FlatItems.Should().HaveCount(3);
        adapter.FlatItems[0].Should().Be(root);
        adapter.FlatItems[1].Should().Be(subFolder);
        adapter.FlatItems[2].Should().Be(rootFile);
    }

    [Fact]
    public void InsertingItemIntoExpandedGroupShouldInsertIncrementallyWithoutRemovingExistingItems()
    {
        var root = new FolderItem { Name = "Root", IsExpanded = true };
        var file1 = new FileItem { Name = "file1.png", Parent = root };
        var file2 = new FileItem { Name = "file2.png", Parent = root };
        root.ItemsSource.AddRange(new[] { file1, file2 });

        var roots = new ObservableCollection<GroupItem> { root };
        using var adapter = new FlatTreeAdapter(roots);

        adapter.FlatItems.Should().HaveCount(3);
        adapter.FlatItems[0].Should().Be(root);
        adapter.FlatItems[1].Should().Be(file1);
        adapter.FlatItems[2].Should().Be(file2);

        // Registrar cambios de colección en FlatItems para verificar que no haya Remove ni Reset
        var removeCount = 0;
        var resetCount = 0;
        var addCount = 0;
        ((System.Collections.Specialized.INotifyCollectionChanged)adapter.FlatItems).CollectionChanged += (s, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove) removeCount++;
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) resetCount++;
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add) addCount++;
        };

        // Insertar un nuevo archivo al principio de la carpeta
        var fileNew = new FileItem { Name = "fileNew.png", Parent = root };
        root.ItemsSource.Insert(0, fileNew);

        adapter.FlatItems.Should().HaveCount(4);
        adapter.FlatItems[0].Should().Be(root);
        adapter.FlatItems[1].Should().Be(fileNew);
        adapter.FlatItems[2].Should().Be(file1);
        adapter.FlatItems[3].Should().Be(file2);

        // Validar que NINGÚN elemento fue removido o reseteado; solo fue un Add incremental
        removeCount.Should().Be(0, "Una inserción incremental jamás debe remover elementos existentes de FlatItems");
        resetCount.Should().Be(0, "Una inserción incremental jamás debe resetear FlatItems");
        addCount.Should().BeGreaterThan(0, "Debe notificar únicamente la adición");
    }

    [Fact]
    public void RemovingItemFromExpandedGroupShouldOnlyRemoveTargetItemWithoutAffectingSiblings()
    {
        var root = new FolderItem { Name = "Root", IsExpanded = true };
        var file1 = new FileItem { Name = "file1.png", Parent = root };
        var file2 = new FileItem { Name = "file2.png", Parent = root };
        root.ItemsSource.AddRange(new[] { file1, file2 });

        var roots = new ObservableCollection<GroupItem> { root };
        using var adapter = new FlatTreeAdapter(roots);

        adapter.FlatItems.Should().HaveCount(3);

        var removedItems = new System.Collections.Generic.List<NavigationItem>();
        ((System.Collections.Specialized.INotifyCollectionChanged)adapter.FlatItems).CollectionChanged += (s, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<NavigationItem>())
                {
                    removedItems.Add(item);
                }
            }
        };

        // Remover solo file1
        root.ItemsSource.Remove(file1);

        adapter.FlatItems.Should().HaveCount(2);
        adapter.FlatItems[0].Should().Be(root);
        adapter.FlatItems[1].Should().Be(file2);

        removedItems.Should().ContainSingle().Which.Should().Be(file1);
    }
}
