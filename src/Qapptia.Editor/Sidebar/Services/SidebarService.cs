using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Qapptia.Editor.Core;
using Qapptia.Editor.Sidebar.Models;
using Serilog;

namespace Qapptia.Editor.Sidebar.Services;

public sealed class SidebarService : ISidebarService
{
    private static readonly HashSet<string> s_allowedExtensions = new(Qapptia.Core.Constants.SupportedImageExtensions, StringComparer.OrdinalIgnoreCase);

    private readonly ILogger? _logger;
    private FileSystemWatcher? _fileWatcher;
    private CancellationTokenSource? _watcherDebounceCts;
    private Action? _onFileSystemChanged;

    public SidebarService(ILogger? logger = null)
    {
        _logger = logger;
    }

    public static string NormalizePath(string path) => path.Replace('\\', '/');

    public async Task<SidebarFolder?> BuildTreeAsync(string rootPath, IReadOnlyList<string> expandedFolders, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return null;

        return await Task.Run(() =>
        {
            var dirInfo = new DirectoryInfo(rootPath);
            var normalizedRoot = NormalizePath(rootPath);

            var root = new SidebarFolder
            {
                Name = dirInfo.Name,
                FullPath = normalizedRoot,
                IsExpanded = expandedFolders.Any(p => string.Equals(p, normalizedRoot, StringComparison.OrdinalIgnoreCase)) || expandedFolders.Count == 0
            };

            if (string.IsNullOrEmpty(root.Name))
                root.Name = normalizedRoot;

            PopulateFolder(root, dirInfo, expandedFolders);
            return root;
        }, ct).ConfigureAwait(false);
    }

    public SidebarItem? FindNodeByPath(IEnumerable<SidebarItem> nodes, string path)
    {
        var normalizedTarget = NormalizePath(path);
        foreach (var node in nodes)
        {
            if (string.Equals(NormalizePath(node.FullPath), normalizedTarget, StringComparison.OrdinalIgnoreCase))
                return node;

            if (node is SidebarFolder folder)
            {
                var found = FindNodeByPath(folder.Items, normalizedTarget);
                if (found != null)
                    return found;
            }
        }
        return null;
    }

    public void StartWatching(string rootPath, Action onFileSystemChanged)
    {
        StopWatching();

        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return;

        _onFileSystemChanged = onFileSystemChanged;

        try
        {
            _fileWatcher = new FileSystemWatcher(rootPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                EnableRaisingEvents = true
            };

            _fileWatcher.Created += HandleFileSystemEvent;
            _fileWatcher.Deleted += HandleFileSystemEvent;
            _fileWatcher.Renamed += HandleFileSystemEvent;
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "No se pudo iniciar FileSystemWatcher en {Path}", rootPath);
        }
    }

    public void StopWatching()
    {
        _watcherDebounceCts?.Cancel();
        _watcherDebounceCts?.Dispose();
        _watcherDebounceCts = null;

        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Created -= HandleFileSystemEvent;
            _fileWatcher.Deleted -= HandleFileSystemEvent;
            _fileWatcher.Renamed -= HandleFileSystemEvent;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }
    }

    private void HandleFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        var fileName = Path.GetFileName(e.FullPath);
        if (fileName.StartsWith('.')) return;

        var ext = Path.GetExtension(e.FullPath);
        var isImageOrDir = s_allowedExtensions.Contains(ext) || string.IsNullOrEmpty(ext);
        if (!isImageOrDir) return;

        _watcherDebounceCts?.Cancel();
        _watcherDebounceCts = new CancellationTokenSource();
        var token = _watcherDebounceCts.Token;

        Task.Delay(250, token).ContinueWith(t =>
        {
            if (t.IsCanceled) return;
            _onFileSystemChanged?.Invoke();
        }, TaskScheduler.Default);
    }

    private static void PopulateFolder(SidebarFolder folderNode, DirectoryInfo dirInfo, IReadOnlyList<string> expandedFolders)
    {
        try
        {
            var subFoldersList = new List<SidebarFolder>();
            var filesList = new List<SidebarFile>();

            // 1. Obtener subdirectorios, omitiendo ocultos (ej. ".annotations")
            foreach (var subDir in dirInfo.EnumerateDirectories())
            {
                if (subDir.Name.StartsWith('.'))
                    continue;

                var normalizedD = NormalizePath(subDir.FullName);
                var subFolder = new SidebarFolder
                {
                    Name = subDir.Name,
                    FullPath = normalizedD,
                    IsExpanded = expandedFolders.Any(p => string.Equals(p, normalizedD, StringComparison.OrdinalIgnoreCase))
                };

                PopulateFolder(subFolder, subDir, expandedFolders);

                if (subFolder.Items.Count > 0)
                {
                    subFoldersList.Add(subFolder);
                }
            }

            // 2. Obtener imágenes (png, jpg, jpeg) leyendo timestamps directamente del sistema de archivos
            foreach (var fileInfo in dirInfo.EnumerateFiles())
            {
                if (!s_allowedExtensions.Contains(fileInfo.Extension))
                    continue;

                var fileDate = fileInfo.LastWriteTimeUtc > fileInfo.CreationTimeUtc
                    ? fileInfo.LastWriteTimeUtc
                    : fileInfo.CreationTimeUtc;

                filesList.Add(new SidebarFile
                {
                    Name = fileInfo.Name,
                    FullPath = NormalizePath(fileInfo.FullName),
                    EffectiveDateUtc = fileDate
                });
            }

            // 3. Ordenar subcarpetas y archivos de más reciente a más antiguo por metadata de fecha
            var sortedSubFolders = subFoldersList.OrderByDescending(f => f.EffectiveDateUtc);
            var sortedFiles = filesList.OrderByDescending(f => f.EffectiveDateUtc);

            foreach (var sf in sortedSubFolders)
            {
                folderNode.Items.Add(sf);
            }

            foreach (var f in sortedFiles)
            {
                folderNode.Items.Add(f);
            }

            // 4. Calcular fecha efectiva de esta carpeta a partir de sus elementos
            if (folderNode.Items.Count > 0)
            {
                folderNode.EffectiveDateUtc = folderNode.Items.Max(i => i.EffectiveDateUtc);
            }
            else
            {
                folderNode.EffectiveDateUtc = dirInfo.LastWriteTimeUtc > dirInfo.CreationTimeUtc
                    ? dirInfo.LastWriteTimeUtc
                    : dirInfo.CreationTimeUtc;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Ignorar carpetas sin permisos
        }
        catch (Exception)
        {
            // Fallar de forma segura ante errores de I/O
        }
    }

    public void Dispose()
    {
        StopWatching();
    }
}
