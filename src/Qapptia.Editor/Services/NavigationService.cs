using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using DynamicData;
using Qapptia.Editor.Core;
using Qapptia.Editor.Models.Navigation;
using Serilog;

namespace Qapptia.Editor.Services;

/// <summary>
/// Servicio de dominio para exploración, ordenamiento cronológico y monitoreo del árbol de capturas en disco.
/// Refactorizado para Arquitectura Dual-Channel (Fase 1 Topológica).
/// </summary>
public sealed class NavigationService : INavigationService
{
    private static readonly HashSet<string> s_allowedExtensions = new(Qapptia.Core.Constants.SupportedImageExtensions, StringComparer.OrdinalIgnoreCase);

    private readonly ILogger? _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (long Length, DateTime LastWriteUtc, DateTime EffectiveDate)> _effectiveDateCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, GroupItem> _topologicalCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<DateTime, CalendarGroupItem> _calendarDayIndex = new();
    
    private string _calendarWeekLabel = "Semana";
    private FileSystemWatcher? _fileWatcher;
    private CancellationTokenSource? _watcherDebounceCts;
    private Action<string, WatcherChangeTypes>? _onFileEvent;
    private Action? _onFileSystemChanged;

    // FASE 2: DUAL-CHANNEL LIFO BACKGROUND WORKER
    private readonly Channel<string> _standardQueue = Channel.CreateUnbounded<string>();
    
    // Un BoundedChannel de tamaño 1 con DropOldest actúa matemáticamente como una Pila (LIFO)
    // eliminando (cancelando) de forma automática y gratuita las peticiones prioritarias viejas/fantasmas.
    private readonly Channel<string> _priorityLifoStack = Channel.CreateBounded<string>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        
    private readonly SemaphoreSlim _priorityLock = new(1, 1);
    
    private Action<Action>? _uiDispatcher;
    private Action<FileItem>? _onFileDispatched;
        
    private CancellationTokenSource? _indexerCts;
    private Task? _passiveTask;
    private Task? _priorityTask;
    private Task? _enrichmentTask;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _indexedFolders = new(StringComparer.OrdinalIgnoreCase);

    // Detección de cierre de la indexación global para el ciclo de "vacío confirmado"
    private int _activeProcessors;
    private bool _indexSessionActive;
    private readonly object _completionGate = new();

    public NavigationService(ILogger? logger = null)
    {
        _logger = logger;
    }

    public static string NormalizePath(string path) => path.Replace('\\', '/');

    // =========================================================================
    // FASE 1: TOPOLOGÍA INMEDIATA (MS-0)
    // =========================================================================

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
                IsExpanded = expandedFolders.Count == 0 || expandedFolders.Any(p => string.Equals(p, normalizedRoot, StringComparison.OrdinalIgnoreCase) || p.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            };

            if (string.IsNullOrEmpty(root.Name)) root.Name = normalizedRoot;

            _topologicalCache.Clear();
            _topologicalCache[normalizedRoot] = root;
            _indexedFolders.Clear();

            // FASE 1: Solo Topología pura sin leer archivos (O(1) I/O por carpeta)
            PopulateTopologicalFolders(root, dirInfo, expandedFolders);
            
            return root;
        }, ct).ConfigureAwait(false);
    }
    
    private void PopulateTopologicalFolders(FolderItem parentFolder, DirectoryInfo dirInfo, IReadOnlyList<string> expandedFolders)
    {
        var subFolders = new List<FolderItem>();
        try
        {
            var options = new EnumerationOptions { IgnoreInaccessible = true };
            foreach (var subDir in dirInfo.EnumerateDirectories("*", options))
            {
                if ((subDir.Attributes & FileAttributes.Hidden) != 0 || 
                    (subDir.Attributes & FileAttributes.System) != 0 || 
                    subDir.Name.StartsWith(Constants.HiddenPrefixChar))
                {
                    continue;
                }

                var normalizedPath = NormalizePath(subDir.FullName);
                var folderItem = new FolderItem
                {
                    Name = subDir.Name,
                    FullPath = normalizedPath,
                    Parent = parentFolder,
                    IsExpanded = expandedFolders.Any(p => string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase))
                };
                
                _topologicalCache[normalizedPath] = folderItem;

                // Recursividad DFS segura
                PopulateTopologicalFolders(folderItem, subDir, expandedFolders);
                subFolders.Add(folderItem);
            }
            
            foreach (var folder in subFolders.OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                // Soporte O(1) puro para Lazy-Loading Chevron: Asumimos existencia hasta que el Worker pasivo/activo lo valide.
                // Soporte O(1) puro para Lazy-Loading Chevron sin Dummys.
                parentFolder.ItemsSource.Add(folder);
            }
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Fase 1 (Topología): No se pudieron listar directorios de {Path}", dirInfo.FullName);
        }
    }

    public async Task<IReadOnlyList<GroupItem>> BuildCalendarTreeAsync(string rootPath, IReadOnlyList<string> expandedGroups, string? weekLabel = null, DateTime? referenceToday = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return Array.Empty<GroupItem>();

        _calendarWeekLabel = !string.IsNullOrWhiteSpace(weekLabel) ? weekLabel : "Semana";
        return await Task.Run(() =>
        {
            var dirInfo = new DirectoryInfo(rootPath);
            
            // FASE 1 - HEURÍSTICA DE CALENDARIO (Extraer Años desde Topología de Directorios, MS-0)
            var targetYears = ExtractHeuristicYearsFromDirectories(dirInfo);
            
            var culture = new CultureInfo("es-ES");
            var today = (referenceToday ?? DateTime.Today).Date;
            int currentYear = today.Year;
            int currentMonth = today.Month;
            
            targetYears.Add(currentYear);

            foreach (var group in expandedGroups)
            {
                if (group.StartsWith("cal://", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = group[6..].Split('/');
                    if (parts.Length > 0 && int.TryParse(parts[0], out int expYear))
                    {
                        targetYears.Add(expYear);
                    }
                }
            }
                
            var orderedYears = targetYears.Where(y => y <= currentYear).Distinct().OrderByDescending(y => y).ToList();
            var years = new List<GroupItem>();
            _calendarDayIndex.Clear();
            
            // Reconstrucción del Calendario Estructural O(1)
            foreach (int year in orderedYears)
            {
                bool isTodayYear = (year == currentYear);
                var yearGroup = new CalendarGroupItem(GroupKind.Year)
                {
                    Name = year.ToString(CultureInfo.InvariantCulture),
                    FullPath = $"cal://{year}",
                    Year = year,
                    IsToday = isTodayYear,
                    IsScanCompleted = true,
                    IsLoading = false
                };
                yearGroup.IsExpanded = expandedGroups.Any(p => string.Equals(p, yearGroup.FullPath, StringComparison.OrdinalIgnoreCase));

                int startMonth = (year == currentYear ? today.Month : 12);
                for (int month = startMonth; month >= 1; month--)
                {
                    string rawMonthName = culture.DateTimeFormat.GetMonthName(month);
                    string monthName = char.ToUpper(rawMonthName[0], culture) + rawMonthName[1..];
                    bool isTodayMonth = (isTodayYear && month == currentMonth);
                    
                    var monthGroup = new CalendarGroupItem(GroupKind.Month)
                    {
                        Name = monthName,
                        FullPath = $"cal://{year}/{month:D2}",
                        Year = year,
                        Month = month,
                        Parent = yearGroup,
                        IsToday = isTodayMonth,
                        IsScanCompleted = true,
                        IsLoading = false
                    };
                    monthGroup.IsExpanded = expandedGroups.Any(p => string.Equals(p, monthGroup.FullPath, StringComparison.OrdinalIgnoreCase));

                    int daysInMonth = DateTime.DaysInMonth(year, month);
                    var monthDays = Enumerable.Range(1, daysInMonth).Select(dayNum => new DateTime(year, month, dayNum)).ToList();
                    var weekGroups = monthDays.GroupBy(d => ISOWeek.GetWeekOfYear(d)).OrderByDescending(g => g.Key);

                    foreach (var weekGroupData in weekGroups)
                    {
                        int weekNum = weekGroupData.Key;
                        var sampleDay = weekGroupData.First();
                        int diffToMonday = (7 + ((int)sampleDay.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
                        DateTime monday = sampleDay.AddDays(-diffToMonday);
                        DateTime sunday = monday.AddDays(6);

                        // Si la semana inicia después de hoy, es una semana completamente futura: omitir
                        if (monday > today) continue;

                        string startMmm = GetShortMonthName(culture, monday.Month);
                        string endMmm = GetShortMonthName(culture, sunday.Month);
                        string resolvedWeekLabel = !string.IsNullOrWhiteSpace(weekLabel) ? weekLabel : "Semana";
                        bool isTodayWeek = (isTodayYear && isTodayMonth && today >= monday && today <= sunday);
                        
                        var weekGroup = new CalendarGroupItem(GroupKind.Week)
                        {
                            Name = $"{monday:dd} {startMmm} - {sunday:dd} {endMmm} ({resolvedWeekLabel} {weekNum})",
                            FullPath = $"cal://{year}/{month:D2}/w{weekNum:D2}",
                            Year = year,
                            Month = month,
                            WeekNumber = weekNum,
                            Parent = monthGroup,
                            IsToday = isTodayWeek,
                            IsScanCompleted = true,
                            IsLoading = false
                        };
                        weekGroup.IsExpanded = expandedGroups.Any(p => string.Equals(p, weekGroup.FullPath, StringComparison.OrdinalIgnoreCase));

                        for (int dayOffset = 6; dayOffset >= 0; dayOffset--)
                        {
                            DateTime day = monday.AddDays(dayOffset);
                            // Omitir días futuros que sobrepasen la fecha actual
                            if (day > today) continue;

                            string rawDayName = culture.DateTimeFormat.GetDayName(day.DayOfWeek);
                            string dayName = rawDayName.ToLower(culture);
                            string dayMmm = GetShortMonthName(culture, day.Month);
                            bool isTodayDay = (day == today);
                            
                            var dayGroup = new CalendarGroupItem(GroupKind.Day)
                            {
                                Name = $"{day:dd} {dayMmm}, {dayName}",
                                FullPath = $"cal://{year}/{month:D2}/w{weekNum:D2}/{day:yyyy-MM-dd}",
                                Year = year,
                                Month = month,
                                WeekNumber = weekNum,
                                Date = day,
                                Parent = weekGroup,
                                IsToday = isTodayDay
                            };
                            dayGroup.IsExpanded = expandedGroups.Any(p => string.Equals(p, dayGroup.FullPath, StringComparison.OrdinalIgnoreCase));
                            
                            // Fase 1 NO inyecta archivos. Los días nacen vacíos y con chevron esperando el Worker.
                            // Solo seteamos el EffectiveDate base
                            dayGroup.EffectiveDateUtc = day.ToUniversalTime();
                            
                            _calendarDayIndex[day.Date] = dayGroup;
                            weekGroup.ItemsSource.Add(dayGroup);
                        }
                        
                        if (weekGroup.ItemsSource.Items.Count > 0)
                        {
                            weekGroup.EffectiveDateUtc = weekGroup.ItemsSource.Items.Max(i => i.EffectiveDateUtc);
                            monthGroup.ItemsSource.Add(weekGroup);
                        }
                    }
                    
                    if (monthGroup.ItemsSource.Items.Count > 0)
                    {
                        monthGroup.EffectiveDateUtc = monthGroup.ItemsSource.Items.Max(i => i.EffectiveDateUtc);
                        yearGroup.ItemsSource.Add(monthGroup);
                    }
                }
                
                if (yearGroup.ItemsSource.Items.Count > 0)
                {
                    yearGroup.EffectiveDateUtc = yearGroup.ItemsSource.Items.Max(i => i.EffectiveDateUtc);
                    years.Add(yearGroup);
                }
            }

            return years;
        }, ct).ConfigureAwait(false);
    }
    
    private HashSet<int> ExtractHeuristicYearsFromDirectories(DirectoryInfo dirInfo)
    {
        var years = new HashSet<int>();
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false };
        
        try
        {
            // Tomamos una muestra rápida (max 1000 carpetas directas) para inferir años pasados
            foreach(var subDir in dirInfo.EnumerateDirectories("*", options).Take(1000))
            {
                if ((subDir.Attributes & FileAttributes.Hidden) != 0 || (subDir.Attributes & FileAttributes.System) != 0) continue;
                years.Add(subDir.CreationTime.Year);
                if (subDir.Name.Length >= 4 && int.TryParse(subDir.Name[..4], out int nameYear) && nameYear >= 2000 && nameYear <= 2100)
                {
                    years.Add(nameYear);
                }
            }
        }
        catch(Exception ex)
        {
            _logger?.Warning(ex, "Fase 1 (Heurística): No se pudo extraer fechas de directorios para pre-poblar calendario.");
        }
        
        return years;
    }

    public CalendarGroupItem? FindCalendarDay(DateTime targetDate)
    {
        return _calendarDayIndex.TryGetValue(targetDate.Date, out var dayGroup) ? dayGroup : null;
    }

    public NavigationItem? FindNodeByPath(IEnumerable<NavigationItem> nodes, string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var normalizedTarget = NormalizePath(path);

        bool isCalendarContext = nodes.OfType<CalendarGroupItem>().Any();

        // 1. Acceso O(1) si es un día de calendario consultado por su ruta cal://
        if (normalizedTarget.StartsWith("cal://", StringComparison.OrdinalIgnoreCase))
        {
            var segments = normalizedTarget.Split('/');
            if (segments.Length >= 6 && DateTime.TryParse(segments[5], out var parsedDay))
            {
                if (_calendarDayIndex.TryGetValue(parsedDay.Date, out var calDay))
                {
                    return calDay;
                }
            }
        }

        // Si estamos en contexto del Calendario, resolver en O(1) vía _calendarDayIndex
        if (isCalendarContext && File.Exists(path))
        {
            var effDate = GetEffectiveDate(new FileInfo(path));
            if (effDate > DateTime.MinValue)
            {
                var localDate = effDate.Kind == DateTimeKind.Utc ? effDate.ToLocalTime() : effDate;
                if (_calendarDayIndex.TryGetValue(localDate.Date, out var dayGroup))
                {
                    var file = dayGroup.ItemsSource.Items.OfType<FileItem>()
                        .FirstOrDefault(f => string.Equals(f.FullPath, path, StringComparison.OrdinalIgnoreCase)
                                          || string.Equals(NormalizePath(f.FullPath), normalizedTarget, StringComparison.OrdinalIgnoreCase));
                    if (file != null) return file;
                }
            }
        }

        // Si NO estamos en contexto exclusivo de calendario, resolver vía _topologicalCache
        if (!isCalendarContext)
        {
            // 2. Acceso O(1) si es una carpeta ya mapeada en topología
            if (_topologicalCache.TryGetValue(normalizedTarget, out var folderItem))
            {
                return folderItem;
            }

            // 3. Si es un archivo, intentar resolución directa por carpeta contenedora en O(archivos_carpeta)
            var parentDir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parentDir))
            {
                var normalizedParent = NormalizePath(parentDir);
                if (_topologicalCache.TryGetValue(normalizedParent, out var parentFolder))
                {
                    var file = parentFolder.ItemsSource.Items.OfType<FileItem>()
                        .FirstOrDefault(f => string.Equals(f.FullPath, path, StringComparison.OrdinalIgnoreCase)
                                          || string.Equals(NormalizePath(f.FullPath), normalizedTarget, StringComparison.OrdinalIgnoreCase));
                    if (file != null) return file;
                }
            }
        }

        // 4. Fallback recursivo seguro sobre la colección solicitada
        return FindNodeRecursive(nodes, normalizedTarget);
    }

    private static NavigationItem? FindNodeRecursive(IEnumerable<NavigationItem> nodes, string normalizedTarget)
    {
        IReadOnlyList<NavigationItem> list = nodes as IReadOnlyList<NavigationItem> ?? nodes.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            NavigationItem node;
            try
            {
                if (i >= list.Count) break;
                node = list[i];
            }
            catch (ArgumentOutOfRangeException)
            {
                break;
            }

            // Los FileItem almacenan la ruta nativa del SO: se compara en forma normalizada
            if (string.Equals(NormalizePath(node.FullPath), normalizedTarget, StringComparison.OrdinalIgnoreCase))
                return node;

            if (node is GroupItem group)
            {
                var children = group.ItemsSource.Items.Count > 0 ? (IReadOnlyList<NavigationItem>)group.ItemsSource.Items : group.Items;
                if (children.Count > 0)
                {
                    var found = FindNodeRecursive(children, normalizedTarget);
                    if (found != null) return found;
                }
            }
        }
        return null;
    }

    // =========================================================================
    // FASE 2: DOS ETAPAS (TWO-STAGE PIPELINE) - TOPOLOGÍA & ENRIQUECIMIENTO
    // =========================================================================

    private List<GroupItem>? _calendarCache;
    private ObservableCollection<GroupItem>? _calendarCollection;
    private GroupItem? _treeRootCache;
    private string? _rootPath;
    
    // Cola Fase 2.1 (Topológica Diferida)
    private readonly Channel<FileItem> _enrichmentQueue = Channel.CreateUnbounded<FileItem>();

    public void StartIndexer(string rootPath, IEnumerable<GroupItem> calendarYears, GroupItem? treeRoot = null, Action<Action>? uiDispatcher = null, Action<FileItem>? onFileDispatched = null)
    {
        _rootPath = rootPath;
        _calendarCollection = calendarYears as ObservableCollection<GroupItem>;
        _calendarCache = calendarYears.ToList();
        _treeRootCache = treeRoot;
        _uiDispatcher = uiDispatcher ?? (a => a());
        _onFileDispatched = onFileDispatched;

        foreach (var year in _calendarCache)
        {
            IndexCalendarDaysRecursive(year);
        }

        // Cancelar si había algo previo en indexación sin tocar el FileSystemWatcher
        StopIndexer();

        // Armar la nueva sesión de indexación (los restos en cola los consumen los nuevos workers)
        lock (_completionGate)
        {
            _indexSessionActive = true;
        }

        _indexerCts = new CancellationTokenSource();
        _indexedFolders.Clear();
        _passiveTask = Task.Run(() => PassiveWorkerLoop(_indexerCts.Token));
        _priorityTask = Task.Run(() => PriorityWorkerLoop(_indexerCts.Token));
        _enrichmentTask = Task.Run(() => EnrichmentWorkerLoop(_indexerCts.Token));
        
        // Empujar la raíz a la cola lenta (Background)
        _standardQueue.Writer.TryWrite(rootPath);
    }

    public void RequestPriorityFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return;
        var normalized = NormalizePath(folderPath);

        // Inyección Prioritaria Inmediata: Si el usuario expandió una carpeta, inyectar sus archivos directamente
        if (_topologicalCache.TryGetValue(normalized, out var folderItem))
        {
            var dispatcher = _uiDispatcher ?? (a => a());

            if (!folderItem.IsScanCompleted)
            {
                lock (folderItem)
                {
                    if (folderItem.IsScanCompleted)
                    {
                        dispatcher(() => folderItem.IsLoading = false);
                        return;
                    }

                    dispatcher(() => folderItem.IsLoading = true);

                    try
                    {
                        if (Directory.Exists(folderPath))
                        {
                            var dirInfo = new DirectoryInfo(folderPath);
                            var options = new EnumerationOptions { IgnoreInaccessible = true };
                            var existingPaths = folderItem.ItemsSource.Items.Count > 0
                                ? new HashSet<string>(folderItem.ItemsSource.Items.OfType<FileItem>().Select(f => f.FullPath), StringComparer.OrdinalIgnoreCase)
                                : null;
                            var allFiles = new List<FileItem>();

                            foreach (var file in dirInfo.EnumerateFiles("*", options))
                            {
                                if (IsNavigablePath(file.FullName) && (existingPaths == null || existingPaths.Add(file.FullName)))
                                {
                                    var effDate = GetPreliminaryEffectiveDate(file);
                                    var fileItem = new FileItem
                                    {
                                        Name = file.Name,
                                        FullPath = file.FullName,
                                        EffectiveDateUtc = effDate,
                                        Parent = folderItem
                                    };
                                    allFiles.Add(fileItem);
                                    _enrichmentQueue.Writer.TryWrite(fileItem);
                                }
                            }

                            if (allFiles.Count > 0)
                            {
                                dispatcher(() =>
                                {
                                    InsertFilesSorted(folderItem, allFiles);
                                    foreach (var f in allFiles)
                                    {
                                        _onFileDispatched?.Invoke(f);
                                    }
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warning(ex, "Error al cargar carpeta prioritaria {Path}", folderPath);
                    }
                    finally
                    {
                        _indexedFolders[normalized] = 1;
                        dispatcher(() =>
                        {
                            folderItem.IsLoading = false;
                            folderItem.IsScanCompleted = true;
                        });
                    }
                }
                return;
            }
            else
            {
                // Ya escaneada: asegurar apagado de spinner si estuviera encendido
                dispatcher(() => folderItem.IsLoading = false);
                return;
            }
        }

        _priorityLifoStack.Writer.TryWrite(folderPath);
    }

    private async Task PassiveWorkerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var targetPath = await _standardQueue.Reader.ReadAsync(ct);
                await ProcessDirectoryTopologyAsync(targetPath, isPriority: false, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error en el Passive Worker (Topología)");
            }
        }
    }

    private async Task PriorityWorkerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var targetPath = await _priorityLifoStack.Reader.ReadAsync(ct);
                await ProcessDirectoryTopologyAsync(targetPath, isPriority: true, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error en el Priority Worker (Topología)");
            }
        }
    }

    private async Task ProcessDirectoryTopologyAsync(string dirPath, bool isPriority, CancellationToken ct)
    {
        Interlocked.Increment(ref _activeProcessors);
        try
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath)) return;

            var normalized = NormalizePath(dirPath);
            if (!_indexedFolders.TryAdd(normalized, 1))
            {
                _uiDispatcher?.Invoke(() => FinalizeFolderProcessing(dirPath));
                return;
            }

            var options = new EnumerationOptions { IgnoreInaccessible = true };
            var batch = new List<FileItem>(200);

            try
            {
                var dirInfo = new DirectoryInfo(dirPath);

                // Si estamos en background, poblar la cola con subdirectorios (BFS)
                if (!isPriority)
                {
                    foreach (var subDir in dirInfo.EnumerateDirectories("*", options))
                    {
                        if ((subDir.Attributes & FileAttributes.Hidden) == 0 && (subDir.Attributes & FileAttributes.System) == 0)
                            _standardQueue.Writer.TryWrite(subDir.FullName);
                    }
                }

                // ETAPA 1: Topología Ciega (Lectura ultra-rápida sin extraer fechas)
                foreach (var file in dirInfo.EnumerateFiles("*", options))
                {
                    ct.ThrowIfCancellationRequested();

                    if (IsNavigablePath(file.FullName))
                    {
                        var effDate = GetPreliminaryEffectiveDate(file);
                        var fileItem = new FileItem
                        {
                            Name = file.Name,
                            FullPath = file.FullName,
                            EffectiveDateUtc = effDate
                        };
                        batch.Add(fileItem);

                        // Empujar al Worker de Enriquecimiento (Etapa 2)
                        _enrichmentQueue.Writer.TryWrite(fileItem);

                        if (batch.Count >= 200)
                        {
                            var capturedBatch = batch.ToList();
                            _uiDispatcher?.Invoke(() => DispatchTopologyToTreeOnly(capturedBatch));
                            batch.Clear();
                        }
                    }
                }

                if (batch.Count > 0)
                {
                    var capturedBatch = batch.ToList();
                    _uiDispatcher?.Invoke(() => DispatchTopologyToTreeOnly(capturedBatch));
                }
            }
            catch (UnauthorizedAccessException) { /* Ignorar carpetas protegidas */ }
            finally
            {
                _uiDispatcher?.Invoke(() => FinalizeFolderProcessing(dirPath));
            }
            await Task.CompletedTask;
        }
        finally
        {
            Interlocked.Decrement(ref _activeProcessors);
            TrySignalIndexingCompleted();
        }
    }
    
    private async Task EnrichmentWorkerLoop(CancellationToken ct)
    {
        var batch = new List<FileItem>(100);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var fileItem = await _enrichmentQueue.Reader.ReadAsync(ct);
                ProcessEnrichmentItem(fileItem, batch);

                // Drenar elementos inmediatamente disponibles sin demoras artificiales
                while (batch.Count < 100 && _enrichmentQueue.Reader.TryRead(out var nextItem))
                {
                    ProcessEnrichmentItem(nextItem, batch);
                }

                if (batch.Count > 0)
                {
                    var capturedBatch = batch.ToList();
                    batch.Clear();
                    _uiDispatcher?.Invoke(() => DispatchEnrichedToCalendar(capturedBatch));
                }

                TrySignalIndexingCompleted();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error en el Enrichment Worker (Fechas)");
            }
        }
    }

    private void ProcessEnrichmentItem(FileItem fileItem, List<FileItem> batch)
    {
        try
        {
            var fileInfo = new FileInfo(fileItem.FullPath);
            if (fileInfo.Exists)
            {
                var date = GetEffectiveDate(fileInfo);
                if (fileItem.EffectiveDateUtc != date)
                {
                    fileItem.EffectiveDateUtc = date; // Notifica a la UI automáticamente
                    if (fileItem.Parent is GroupItem parentGroup)
                    {
                        _uiDispatcher?.Invoke(() => EnsureFilesSorted(parentGroup));
                    }
                }
                batch.Add(fileItem);
            }
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Error al extraer metadatos para {Path}", fileItem.FullPath);
        }
    }

    private void DispatchTopologyToTreeOnly(List<FileItem> batch)
    {
        if (_treeRootCache == null) return;

        var treeInjections = new Dictionary<GroupItem, List<FileItem>>();
        
        foreach (var file in batch)
        {
            var parentPath = NormalizePath(Path.GetDirectoryName(file.FullPath) ?? string.Empty);
            if (string.IsNullOrEmpty(parentPath)) continue;

            // O(1) Cache Lookup - ¡Erradica el congelamiento del UI Thread!
            if (_topologicalCache.TryGetValue(parentPath, out var folderItem))
            {
                if (!treeInjections.TryGetValue(folderItem, out var list))
                {
                    list = new List<FileItem>();
                    treeInjections[folderItem] = list;
                }
                var existingFile = folderItem.ItemsSource.Items.OfType<FileItem>()
                    .FirstOrDefault(f => string.Equals(NormalizePath(f.FullPath), NormalizePath(file.FullPath), StringComparison.OrdinalIgnoreCase));
                var itemToAdd = existingFile ?? file;
                itemToAdd.Parent = folderItem;
                list.Add(itemToAdd);
            }
        }

        foreach (var kvp in treeInjections)
        {
            InsertFilesSorted(kvp.Key, kvp.Value);
        }

        // Permitimos el Late-Binding de la vista árbol inmediatamente
        foreach (var file in batch)
        {
            _onFileDispatched?.Invoke(file);
        }
    }

    private void FinalizeFolderProcessing(string dirPath)
    {
        var normalized = NormalizePath(dirPath);
        if (_topologicalCache.TryGetValue(normalized, out var folderItem))
        {
            folderItem.IsLoading = false;
            // Carpeta inspeccionada por completo: si está vacía, queda confirmada para ocultar su chevron
            folderItem.IsScanCompleted = true;
        }
    }

    /// <summary>
    /// Señala el cierre de la indexación transversal cuando no queda trabajo en cola
    /// ni procesadores activos, y confirma el estado de escaneo de los días del calendario.
    /// </summary>
    private void TrySignalIndexingCompleted()
    {
        lock (_completionGate)
        {
            if (!_indexSessionActive) return;
            if (Volatile.Read(ref _activeProcessors) != 0) return;
            if (_standardQueue.Reader.Count != 0) return;
            if (_priorityLifoStack.Reader.Count != 0) return;
            if (_enrichmentQueue.Reader.Count != 0) return;
            _indexSessionActive = false;
        }

        var dispatcher = _uiDispatcher ?? (a => a());
        dispatcher(() =>
        {
            if (_calendarCache == null) return;
            int currentYear = DateTime.Today.Year;

            foreach (var year in _calendarCache)
            {
                MarkDayScanCompleted(year);
            }

            var emptyYears = _calendarCache
                .OfType<CalendarGroupItem>()
                .Where(y => y.Year != currentYear && !HasAnyFilesRecursive(y))
                .ToList();

            foreach (var emptyYear in emptyYears)
            {
                _calendarCache.Remove(emptyYear);
                _calendarCollection?.Remove(emptyYear);
                RemoveCalendarDaysRecursive(emptyYear);
            }
        });
    }

    private static void MarkDayScanCompleted(GroupItem group)
    {
        group.IsScanCompleted = true;
        group.IsLoading = false;

        var children = group.ItemsSource.Items.Count > 0 ? (IReadOnlyList<NavigationItem>)group.ItemsSource.Items : group.Items;
        foreach (var item in children)
        {
            if (item is GroupItem child)
            {
                MarkDayScanCompleted(child);
            }
        }
    }

    private static bool HasAnyFilesRecursive(GroupItem group)
    {
        if (group.HasFiles) return true;
        var children = group.ItemsSource.Items.Count > 0 ? (IReadOnlyList<NavigationItem>)group.ItemsSource.Items : group.Items;
        foreach (var child in children)
        {
            if (child is FileItem) return true;
            if (child is GroupItem childGroup && HasAnyFilesRecursive(childGroup)) return true;
        }
        return false;
    }

    private void RemoveCalendarDaysRecursive(GroupItem group)
    {
        if (group is CalendarGroupItem calDay && calDay.Kind == GroupKind.Day && calDay.Date.HasValue)
        {
            _calendarDayIndex.TryRemove(calDay.Date.Value.Date, out _);
        }

        var children = group.ItemsSource.Items.Count > 0 ? (IReadOnlyList<NavigationItem>)group.ItemsSource.Items : group.Items;
        foreach (var child in children)
        {
            if (child is GroupItem childGroup)
            {
                RemoveCalendarDaysRecursive(childGroup);
            }
        }
    }

    public CalendarGroupItem? GetOrCreateCalendarDay(DateTime targetDate, IEnumerable<GroupItem>? roots = null)
    {
        var collection = (roots as ObservableCollection<GroupItem>) ?? _calendarCollection;
        if (collection != null)
        {
            _calendarCollection ??= collection;
            _calendarCache ??= collection.ToList();
        }
        else if (roots != null)
        {
            _calendarCache ??= roots.ToList();
        }

        if (_calendarCache == null) return null;
        var dayDate = targetDate.Date;
        if (_calendarDayIndex.TryGetValue(dayDate, out var existingDay))
        {
            return existingDay;
        }

        var culture = CultureInfo.CurrentCulture;
        int year = dayDate.Year;
        int month = dayDate.Month;
        var today = DateTime.Today;
        int currentYear = today.Year;
        int currentMonth = today.Month;

        // 1. Año
        var yearGroup = _calendarCache.OfType<CalendarGroupItem>().FirstOrDefault(y => y.Year == year);
        if (yearGroup == null)
        {
            bool isTodayYear = (year == currentYear);
            yearGroup = new CalendarGroupItem(GroupKind.Year)
            {
                Name = year.ToString(CultureInfo.InvariantCulture),
                FullPath = $"cal://{year}",
                Year = year,
                IsToday = isTodayYear,
                IsScanCompleted = true,
                IsLoading = false
            };

            int insertIdx = 0;
            while (insertIdx < _calendarCache.Count && (_calendarCache[insertIdx] as CalendarGroupItem)?.Year > year)
            {
                insertIdx++;
            }
            _calendarCache.Insert(insertIdx, yearGroup);
            if (_calendarCollection != null && !_calendarCollection.Contains(yearGroup))
            {
                _calendarCollection.Insert(insertIdx, yearGroup);
            }
            else if (roots is IList<GroupItem> list && !list.Contains(yearGroup))
            {
                list.Insert(insertIdx, yearGroup);
            }
        }

        // 2. Mes
        var monthChildren = yearGroup.ItemsSource.Items.Count > 0 ? (IReadOnlyList<NavigationItem>)yearGroup.ItemsSource.Items : yearGroup.Items;
        var monthGroup = monthChildren.OfType<CalendarGroupItem>().FirstOrDefault(m => m.Month == month);
        if (monthGroup == null)
        {
            string rawMonthName = culture.DateTimeFormat.GetMonthName(month);
            string monthName = char.ToUpper(rawMonthName[0], culture) + rawMonthName[1..];
            bool isTodayMonth = (year == currentYear && month == currentMonth);

            monthGroup = new CalendarGroupItem(GroupKind.Month)
            {
                Name = monthName,
                FullPath = $"cal://{year}/{month:D2}",
                Year = year,
                Month = month,
                Parent = yearGroup,
                IsToday = isTodayMonth,
                IsScanCompleted = true,
                IsLoading = false
            };

            int mIdx = 0;
            while (mIdx < monthChildren.Count && (monthChildren[mIdx] as CalendarGroupItem)?.Month > month)
            {
                mIdx++;
            }
            yearGroup.ItemsSource.Insert(mIdx, monthGroup);
        }

        // 3. Semana
        int weekNum = ISOWeek.GetWeekOfYear(dayDate);
        int diffToMonday = (7 + ((int)dayDate.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
        DateTime monday = dayDate.AddDays(-diffToMonday);
        DateTime sunday = monday.AddDays(6);

        var weekChildren = monthGroup.ItemsSource.Items.Count > 0 ? (IReadOnlyList<NavigationItem>)monthGroup.ItemsSource.Items : monthGroup.Items;
        var weekGroup = weekChildren.OfType<CalendarGroupItem>().FirstOrDefault(w => w.WeekNumber == weekNum);
        if (weekGroup == null)
        {
            string startMmm = GetShortMonthName(culture, monday.Month);
            string endMmm = GetShortMonthName(culture, sunday.Month);
            string resolvedWeekLabel = _calendarWeekLabel;
            bool isTodayWeek = (year == currentYear && month == currentMonth && today >= monday && today <= sunday);

            weekGroup = new CalendarGroupItem(GroupKind.Week)
            {
                Name = $"{monday:dd} {startMmm} - {sunday:dd} {endMmm} ({resolvedWeekLabel} {weekNum})",
                FullPath = $"cal://{year}/{month:D2}/w{weekNum:D2}",
                Year = year,
                Month = month,
                WeekNumber = weekNum,
                Parent = monthGroup,
                IsToday = isTodayWeek,
                IsScanCompleted = true,
                IsLoading = false
            };

            int wIdx = 0;
            while (wIdx < weekChildren.Count && (weekChildren[wIdx] as CalendarGroupItem)?.WeekNumber > weekNum)
            {
                wIdx++;
            }
            monthGroup.ItemsSource.Insert(wIdx, weekGroup);
        }

        // 4. Día
        var dayChildren = weekGroup.ItemsSource.Items.Count > 0 ? (IReadOnlyList<NavigationItem>)weekGroup.ItemsSource.Items : weekGroup.Items;
        var dayGroup = dayChildren.OfType<CalendarGroupItem>().FirstOrDefault(d => d.Date.HasValue && d.Date.Value.Date == dayDate);
        if (dayGroup == null)
        {
            string rawDayName = culture.DateTimeFormat.GetDayName(dayDate.DayOfWeek);
            string dayName = rawDayName.ToLower(culture);
            string dayMmm = GetShortMonthName(culture, dayDate.Month);
            bool isTodayDay = (dayDate == today);

            if (isTodayDay)
            {
                foreach (var d in _calendarDayIndex.Values)
                {
                    if (d.Date.HasValue && d.Date.Value.Date != dayDate && d.IsToday)
                    {
                        d.IsToday = false;
                    }
                }
            }

            dayGroup = new CalendarGroupItem(GroupKind.Day)
            {
                Name = $"{dayDate:dd} {dayMmm}, {dayName}",
                FullPath = $"cal://{year}/{month:D2}/w{weekNum:D2}/{dayDate:yyyy-MM-dd}",
                Year = year,
                Month = month,
                WeekNumber = weekNum,
                Date = dayDate,
                Parent = weekGroup,
                IsToday = isTodayDay,
                EffectiveDateUtc = dayDate.ToUniversalTime(),
                IsScanCompleted = true,
                IsLoading = false
            };

            int dIdx = 0;
            while (dIdx < dayChildren.Count && (dayChildren[dIdx] as CalendarGroupItem)?.Date > dayDate)
            {
                dIdx++;
            }
            weekGroup.ItemsSource.Insert(dIdx, dayGroup);
        }

        _calendarDayIndex[dayDate] = dayGroup;
        return dayGroup;
    }

    private void DispatchEnrichedToCalendar(List<FileItem> batch)
    {
        if ((_calendarCache == null || _calendarCache.Count == 0) && _calendarDayIndex.IsEmpty) return;

        var calendarInjections = new Dictionary<GroupItem, List<FileItem>>();

        foreach (var file in batch)
        {
            if (file.EffectiveDateUtc == default) continue;

            var localDate = file.EffectiveDateUtc.Kind == DateTimeKind.Utc
                ? file.EffectiveDateUtc.ToLocalTime()
                : file.EffectiveDateUtc;

            var dayGroup = FindCalendarDay(localDate.Date) ?? GetOrCreateCalendarDay(localDate.Date);
            if (dayGroup != null)
            {
                if (!calendarInjections.TryGetValue(dayGroup, out var list))
                {
                    list = new List<FileItem>();
                    calendarInjections[dayGroup] = list;
                }

                // Desacoplamos la instancia de FileItem del Calendario respecto a la del Árbol de carpetas.
                // Cada jerarquía tiene su propio Parent y Depth, garantizando que:
                // 1. En Calendario el archivo pertenezca a dayGroup (Depth = 4, sangría 64 px).
                // 2. En Árbol el archivo mantenga su FolderItem (Depth = 1 o 2, sangría 16/32 px).
                // 3. FlatTreeAdapter.CollapseSubtree pueda remover los archivos del árbol sin conflictos de ancestros.
                var existingFile = dayGroup.ItemsSource.Items.OfType<FileItem>()
                    .FirstOrDefault(f => string.Equals(NormalizePath(f.FullPath), NormalizePath(file.FullPath), StringComparison.OrdinalIgnoreCase));

                var calendarFile = existingFile ?? new FileItem
                {
                    Name = file.Name,
                    FullPath = file.FullPath,
                    EffectiveDateUtc = file.EffectiveDateUtc,
                    Parent = dayGroup
                };
                list.Add(calendarFile);
            }
        }

        foreach (var kvp in calendarInjections)
        {
            InsertFilesSorted(kvp.Key, kvp.Value);
            kvp.Key.IsLoading = false;
        }

        // Permitimos el Late-Binding de la vista calendario progresivamente
        foreach (var file in calendarInjections.Values.SelectMany(v => v))
        {
            _onFileDispatched?.Invoke(file);
        }
    }

    public void StartWatching(string rootPath, Action onFileSystemChanged)
    {
        StartWatching(rootPath, null, onFileSystemChanged);
    }

    public void StartWatching(string rootPath, Action<string, WatcherChangeTypes>? onFileEvent, Action onFileSystemChanged)
    {
        StopWatching();
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) return;

        _onFileEvent = onFileEvent;
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

    public void StopIndexer()
    {
        lock (_completionGate)
        {
            _indexSessionActive = false;
        }

        if (_indexerCts != null)
        {
            _indexerCts.Cancel();
            _indexerCts.Dispose();
            _indexerCts = null;
        }
    }

    public void StopWatching()
    {
        StopIndexer();

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
        _calendarDayIndex.Clear();
    }

    private void IndexCalendarDaysRecursive(GroupItem group)
    {
        if (group is CalendarGroupItem { Kind: GroupKind.Day, Date: not null } dayItem)
        {
            _calendarDayIndex[dayItem.Date.Value.Date] = dayItem;
        }

        foreach (var item in group.Items)
        {
            if (item is GroupItem subGroup)
            {
                IndexCalendarDaysRecursive(subGroup);
            }
        }
    }

    private DateTime GetPreliminaryEffectiveDate(FileInfo file)
    {
        if (_effectiveDateCache.TryGetValue(file.FullName, out var cached) &&
            cached.Length == file.Length &&
            cached.LastWriteUtc == file.LastWriteTimeUtc)
        {
            return cached.EffectiveDate;
        }

        return Qapptia.Core.Services.ImageMetadataService.GetFileCreationTimeUtc(file);
    }

    private DateTime GetEffectiveDate(FileInfo file)
    {
        if (_effectiveDateCache.TryGetValue(file.FullName, out var cached) &&
            cached.Length == file.Length &&
            cached.LastWriteUtc == file.LastWriteTimeUtc)
        {
            return cached.EffectiveDate;
        }

        DateTime resolvedDate = Qapptia.Core.Services.ImageMetadataService.GetEffectiveDate(file);
        _effectiveDateCache[file.FullName] = (file.Length, file.LastWriteTimeUtc, resolvedDate);
        return resolvedDate;
    }

    public static bool IsNavigablePath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return false;
        string normalized = fullPath.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
            if (segment.StartsWith(Constants.HiddenPrefixChar)) return false;
            
        string ext = Path.GetExtension(fullPath);
        if (!string.IsNullOrEmpty(ext)) return s_allowedExtensions.Contains(ext);
        return true;
    }

    /// <summary>
    /// Inserta un archivo en la colección del grupo respetando la jerarquía canónica:
    /// 1. Subgrupos (carpetas) siempre primero.
    /// 2. Archivos ordenados de más reciente a más antiguo (EffectiveDateUtc descendente).
    /// </summary>
    public static void InsertFileSorted(GroupItem parent, FileItem file)
    {
        var norm = NormalizePath(file.FullPath);
        lock (parent.ItemsSource)
        {
            if (parent.ItemsSource.Items.OfType<FileItem>().Any(f => string.Equals(NormalizePath(f.FullPath), norm, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            int insertIndex = 0;
            // 1. Subgrupos (carpetas) siempre primero
            while (insertIndex < parent.ItemsSource.Items.Count && parent.ItemsSource.Items[insertIndex] is GroupItem)
            {
                insertIndex++;
            }
            // 2. Archivos ordenados de más reciente a más antiguo (EffectiveDateUtc descendente)
            while (insertIndex < parent.ItemsSource.Items.Count &&
                   parent.ItemsSource.Items[insertIndex] is FileItem existing &&
                   (existing.EffectiveDateUtc > file.EffectiveDateUtc ||
                    (existing.EffectiveDateUtc == file.EffectiveDateUtc && string.Compare(existing.Name, file.Name, StringComparison.OrdinalIgnoreCase) > 0)))
            {
                insertIndex++;
            }
            parent.ItemsSource.Insert(insertIndex, file);
        }
    }

    /// <summary>
    /// Inserta o fusiona un lote de archivos en la colección del grupo asegurando ordenación canónica
    /// de más reciente a más antiguo (EffectiveDateUtc descendente) tras los subgrupos.
    /// </summary>
    public static void InsertFilesSorted(GroupItem parent, IEnumerable<FileItem> files)
    {
        lock (parent.ItemsSource)
        {
            var existingSubGroups = parent.ItemsSource.Items.OfType<GroupItem>().ToList();
            var allFiles = parent.ItemsSource.Items.OfType<FileItem>().Concat(files)
                .GroupBy(f => NormalizePath(f.FullPath), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(f => f.EffectiveDateUtc)
                .ThenByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var combined = new List<NavigationItem>(existingSubGroups.Count + allFiles.Count);
            combined.AddRange(existingSubGroups);
            combined.AddRange(allFiles);

            if (parent.ItemsSource.Items.Count == existingSubGroups.Count)
            {
                parent.ItemsSource.AddRange(allFiles);
            }
            else
            {
                if (parent.ItemsSource.Items.Count == combined.Count)
                {
                    bool isSame = true;
                    for (int i = 0; i < combined.Count; i++)
                    {
                        if (!ReferenceEquals(parent.ItemsSource.Items[i], combined[i]) &&
                            !string.Equals(NormalizePath(parent.ItemsSource.Items[i].FullPath), NormalizePath(combined[i].FullPath), StringComparison.OrdinalIgnoreCase))
                        {
                            isSame = false;
                            break;
                        }
                    }
                    if (isSame) return;
                }

                parent.ItemsSource.Edit(inner =>
                {
                    SynchronizeNavigationList(inner, combined);
                });
            }
        }
    }

    private static void SynchronizeNavigationList(IList<NavigationItem> current, List<NavigationItem> desired)
    {
        static bool AreEqual(NavigationItem a, NavigationItem b) =>
            ReferenceEquals(a, b) || string.Equals(NormalizePath(a.FullPath), NormalizePath(b.FullPath), StringComparison.OrdinalIgnoreCase);

        // 1. Remover elementos que ya no existan en desired o duplicados
        var remainingDesired = new List<NavigationItem>(desired);
        for (int i = current.Count - 1; i >= 0; i--)
        {
            var item = current[i];
            int matchIdx = remainingDesired.FindIndex(d => AreEqual(item, d));
            if (matchIdx >= 0)
            {
                remainingDesired.RemoveAt(matchIdx);
            }
            else
            {
                current.RemoveAt(i);
            }
        }

        // 2. Insertar o mover para igualar desired
        for (int i = 0; i < desired.Count; i++)
        {
            var target = desired[i];
            if (i < current.Count && AreEqual(current[i], target))
            {
                continue;
            }

            int existingIndex = -1;
            for (int j = i + 1; j < current.Count; j++)
            {
                if (AreEqual(current[j], target))
                {
                    existingIndex = j;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                var item = current[existingIndex];
                current.RemoveAt(existingIndex);
                current.Insert(i, item);
            }
            else
            {
                current.Insert(i, target);
            }
        }

        while (current.Count > desired.Count)
        {
            current.RemoveAt(current.Count - 1);
        }
    }

    /// <summary>
    /// Asegura que los archivos de un grupo estén ordenados de más reciente a más antiguo.
    /// Si ya están ordenados, no realiza modificaciones.
    /// </summary>
    public static void EnsureFilesSorted(GroupItem parent)
    {
        var files = parent.ItemsSource.Items.OfType<FileItem>().ToList();
        if (files.Count <= 1) return;

        bool isSorted = true;
        for (int i = 0; i < files.Count - 1; i++)
        {
            if (files[i].EffectiveDateUtc < files[i + 1].EffectiveDateUtc ||
                (files[i].EffectiveDateUtc == files[i + 1].EffectiveDateUtc && string.Compare(files[i].Name, files[i + 1].Name, StringComparison.OrdinalIgnoreCase) < 0))
            {
                isSorted = false;
                break;
            }
        }

        if (!isSorted)
        {
            InsertFilesSorted(parent, Array.Empty<FileItem>());
        }
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        if (!IsNavigablePath(e.FullPath)) return;
        _effectiveDateCache.TryRemove(e.FullPath, out _);

        if (_onFileEvent != null)
        {
            _onFileEvent.Invoke(e.FullPath, e.ChangeType);

            // Solo disparar recarga diferida si es un directorio; los archivos son inyectados reactivamente
            if (string.IsNullOrEmpty(Path.GetExtension(e.FullPath)) || Directory.Exists(e.FullPath))
            {
                TriggerDebouncedChange();
            }
        }
        else
        {
            // Modo general / tests sin manejador reactivo de archivo: notificar debounced para todo cambio
            TriggerDebouncedChange();
        }
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        if (!IsNavigablePath(e.FullPath) && !IsNavigablePath(e.OldFullPath)) return;
        try
        {
            string oldExt = Path.GetExtension(e.OldFullPath);
            string newExt = Path.GetExtension(e.FullPath);

            if (s_allowedExtensions.Contains(oldExt) && s_allowedExtensions.Contains(newExt))
            {
                string parentDir = Path.GetDirectoryName(e.FullPath) ?? string.Empty;
                string oldBaseName = Path.GetFileNameWithoutExtension(e.OldFullPath);
                string newBaseName = Path.GetFileNameWithoutExtension(e.FullPath);

                string annotationDir = Path.Combine(parentDir, Qapptia.Core.Constants.DrawingExtension);
                string oldJsonPath = Path.Combine(annotationDir, $"{oldBaseName}{Qapptia.Core.Constants.JsonFileExtension}");
                string newJsonPath = Path.Combine(annotationDir, $"{newBaseName}{Qapptia.Core.Constants.JsonFileExtension}");

                if (File.Exists(oldJsonPath))
                {
                    File.Move(oldJsonPath, newJsonPath, overwrite: true);
                    _logger?.Information("Sincronización en tiempo real: JSON renombrado {Old} -> {New}", oldJsonPath, newJsonPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Error al sincronizar renombramiento de imagen en tiempo real para {Path}", e.FullPath);
        }

        _effectiveDateCache.TryRemove(e.OldFullPath, out _);
        _effectiveDateCache.TryRemove(e.FullPath, out _);

        if (_onFileEvent != null)
        {
            _onFileEvent.Invoke(e.FullPath, e.ChangeType);
            if (string.IsNullOrEmpty(Path.GetExtension(e.FullPath)) || Directory.Exists(e.FullPath) || Directory.Exists(e.OldFullPath))
            {
                TriggerDebouncedChange();
            }
        }
        else
        {
            TriggerDebouncedChange();
        }
    }

    private void TriggerDebouncedChange()
    {
        _watcherDebounceCts?.Cancel();
        _watcherDebounceCts?.Dispose();
        _watcherDebounceCts = new CancellationTokenSource();
        var token = _watcherDebounceCts.Token;
        Task.Delay(300, token).ContinueWith(t =>
        {
            if (!t.IsCanceled) _onFileSystemChanged?.Invoke();
        }, token);
    }

    private static string GetShortMonthName(CultureInfo culture, int month)
    {
        string raw = culture.DateTimeFormat.GetAbbreviatedMonthName(month).TrimEnd('.');
        if (raw.Equals("sept", StringComparison.OrdinalIgnoreCase) || raw.Equals("set", StringComparison.OrdinalIgnoreCase))
            return "sep";
        if (raw.Length > 3) raw = raw[..3];
        return raw.ToLower(culture);
    }
}
