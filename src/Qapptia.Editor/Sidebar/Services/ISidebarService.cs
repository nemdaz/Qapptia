using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Qapptia.Editor.Sidebar.Models;

namespace Qapptia.Editor.Sidebar.Services;

public interface ISidebarService : IDisposable
{
    Task<SidebarFolder?> BuildTreeAsync(string rootPath, IReadOnlyList<string> expandedFolders, CancellationToken ct = default);
    SidebarItem? FindNodeByPath(IEnumerable<SidebarItem> nodes, string path);
    void StartWatching(string rootPath, Action onFileSystemChanged);
    void StopWatching();
}
