using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Qapptia.Editor.Models.Navigation;
using Serilog;

namespace Qapptia.Editor.Services;

/// <summary>
/// Servicio de dominio para exploración, ordenamiento cronológico y monitoreo del árbol de capturas en disco.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private static readonly HashSet<string> s_allowedExtensions = new(Qapptia.Core.Constants.SupportedImageExtensions, StringComparer.OrdinalIgnoreCase);

    private readonly ILogger? _logger;
    private FileSystemWatcher? _fileWatcher;
    private CancellationTokenSource? _watcherDebounceCts;
    private Action? _onFileSystemChanged;

    public NavigationService(ILogger? logger = null)
    {
        _logger = logger;
    }

    public static string NormalizePath(string path) => path.Replace('\\', '/');

    public async Task<FolderItem?> BuildTreeAsync(string rootPath, IReadOnlyList<string> expandedFolders, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) return null;

        return await Task.Run(() =>
        {
            var dirInfo = new DirectoryInfo(rootPath);
            var normalizedRoot = NormalizePath(rootPath);

            var root = new FolderItem
            {
                Name = dirInfo.Name,
                FullPath = normalizedRoot,
                IsExpanded = expandedFolders.Any(p => string.Equals(p, normalizedRoot, StringComparison.OrdinalIgnoreCase)) || expandedFolders.Count == 0
            };

            if (string.IsNullOrEmpty(root.Name)) root.Name = normalizedRoot;

            PopulateFolder(root, dirInfo, expandedFolders);
            return root;
        }, ct).ConfigureAwait(false);
    }

    public NavigationItem? FindNodeByPath(IEnumerable<NavigationItem> nodes, string path)
    {
        var normalizedTarget = NormalizePath(path);
        foreach (var node in nodes)
        {
            if (string.Equals(NormalizePath(node.FullPath), normalizedTarget, StringComparison.OrdinalIgnoreCase))
                return node;

            if (node is FolderItem folder)
            {
                var found = FindNodeByPath(folder.Items, normalizedTarget);
                if (found != null) return found;
            }
        }
        return null;
    }

    public void StartWatching(string rootPath, Action onFileSystemChanged)
    {
        StopWatching();

        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) return;

        _onFileSystemChanged = onFileSystemChanged;

        try
        {
            _fileWatcher = new FileSystemWatcher(rootPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };

            _fileWatcher.Created += OnFileSystemEvent;
            _fileWatcher.Deleted += OnFileSystemEvent;
            _fileWatcher.Renamed += OnFileSystemRenamed;
            _fileWatcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Error al iniciar FileSystemWatcher en {RootPath}", rootPath);
        }
    }

    public void StopWatching()
    {
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Created -= OnFileSystemEvent;
            _fileWatcher.Deleted -= OnFileSystemEvent;
            _fileWatcher.Renamed -= OnFileSystemRenamed;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }

        _watcherDebounceCts?.Cancel();
        _watcherDebounceCts?.Dispose();
        _watcherDebounceCts = null;
    }

    public void Dispose()
    {
        StopWatching();
    }

    private void PopulateFolder(FolderItem parentFolder, DirectoryInfo dirInfo, IReadOnlyList<string> expandedFolders)
    {
        var subFolders = new List<FolderItem>();
        try
        {
            foreach (var subDir in dirInfo.EnumerateDirectories())
            {
                if ((subDir.Attributes & FileAttributes.Hidden) != 0 || (subDir.Attributes & FileAttributes.System) != 0 || subDir.Name.StartsWith('.'))
                    continue;

                var normalizedPath = NormalizePath(subDir.FullName);
                var folderItem = new FolderItem
                {
                    Name = subDir.Name,
                    FullPath = normalizedPath,
                    IsExpanded = expandedFolders.Any(p => string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase))
                };

                PopulateFolder(folderItem, subDir, expandedFolders);

                if (folderItem.Items.Count > 0)
                {
                    folderItem.EffectiveDateUtc = folderItem.Items.Max(i => i.EffectiveDateUtc);
                    subFolders.Add(folderItem);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "No se pudieron listar los subdirectorios de {Path}", dirInfo.FullName);
        }

        var files = new List<FileItem>();
        try
        {
            foreach (var file in dirInfo.EnumerateFiles())
            {
                if (!s_allowedExtensions.Contains(file.Extension)) continue;

                DateTime effectiveDate = GetEffectiveDate(file);
                files.Add(new FileItem
                {
                    Name = file.Name,
                    FullPath = NormalizePath(file.FullName),
                    EffectiveDateUtc = effectiveDate
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "No se pudieron listar los archivos de {Path}", dirInfo.FullName);
        }

        var sortedFolders = subFolders.OrderByDescending(f => f.EffectiveDateUtc);
        var sortedFiles = files.OrderByDescending(f => f.EffectiveDateUtc);

        foreach (var folder in sortedFolders)
        {
            parentFolder.Items.Add(folder);
        }

        foreach (var file in sortedFiles)
        {
            parentFolder.Items.Add(file);
        }

        if (parentFolder.Items.Count > 0)
        {
            parentFolder.EffectiveDateUtc = parentFolder.Items.Max(i => i.EffectiveDateUtc);
        }
        else
        {
            parentFolder.EffectiveDateUtc = dirInfo.CreationTimeUtc;
        }
    }

    private static DateTime GetEffectiveDate(FileInfo file)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(file.Name);
        var parts = nameWithoutExt.Split('_');
        if (parts.Length >= 2 && parts[^2].Length == 8 && parts[^1].Length == 6)
        {
            var dateStr = parts[^2] + parts[^1];
            if (DateTime.TryParseExact(dateStr, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsedDate))
            {
                return parsedDate.ToUniversalTime();
            }
        }

        return file.CreationTimeUtc;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e) => TriggerDebouncedChange();
    private void OnFileSystemRenamed(object sender, RenamedEventArgs e) => TriggerDebouncedChange();

    private void TriggerDebouncedChange()
    {
        _watcherDebounceCts?.Cancel();
        _watcherDebounceCts?.Dispose();
        _watcherDebounceCts = new CancellationTokenSource();

        var token = _watcherDebounceCts.Token;
        Task.Delay(300, token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                _onFileSystemChanged?.Invoke();
            }
        }, token);
    }
}
