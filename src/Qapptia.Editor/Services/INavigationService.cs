using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Qapptia.Editor.Models.Navigation;

namespace Qapptia.Editor.Services;

/// <summary>
/// Contrato para el servicio de exploración, construcción de árbol y monitoreo de archivos de captura.
/// </summary>
public interface INavigationService : IDisposable
{
    Task<FolderItem?> BuildTreeAsync(string rootPath, IReadOnlyList<string> expandedFolders, CancellationToken ct = default);
    Task<IReadOnlyList<GroupItem>> BuildCalendarTreeAsync(string rootPath, IReadOnlyList<string> expandedGroups, string? weekLabel = null, DateTime? referenceToday = null, CancellationToken ct = default);
    NavigationItem? FindNodeByPath(IEnumerable<NavigationItem> nodes, string path);
    CalendarGroupItem? FindCalendarDay(DateTime targetDate);
    CalendarGroupItem? GetOrCreateCalendarDay(DateTime targetDate, IEnumerable<GroupItem>? roots = null);
    void StartIndexer(string rootPath, IEnumerable<GroupItem> calendarYears, GroupItem? treeRoot = null, Action<Action>? uiDispatcher = null, Action<FileItem>? onFileDispatched = null);
    void RequestPriorityFolder(string folderPath);
    void StartWatching(string rootPath, Action onFileSystemChanged);
    void StartWatching(string rootPath, Action<string, System.IO.WatcherChangeTypes>? onFileEvent, Action onFileSystemChanged);
    void StopWatching();
    void StopIndexer();
}
