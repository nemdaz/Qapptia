using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using FluentAssertions;
using Qapptia.Editor.Models.Navigation;
using Qapptia.Editor.Services;
using Xunit;

namespace Qapptia.Editor.Tests;

public sealed class NavigationServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly NavigationService _sut;

    private static readonly byte[] s_minimalPng = new byte[]
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89,
        0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    };

    public NavigationServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Qapptia_NavigationTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _sut = new NavigationService();
    }

    public void Dispose()
    {
        _sut.Dispose();
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task BuildTreeAsyncWithValidFoldersOrdersDescendingByName()
    {
        var folder2025 = Path.Combine(_testDir, "2025-12");
        var folder202608 = Path.Combine(_testDir, "2026-08");
        var folder202609 = Path.Combine(_testDir, "2026-09");
        Directory.CreateDirectory(folder2025);
        Directory.CreateDirectory(folder202608);
        Directory.CreateDirectory(folder202609);

        var tree = await _sut.BuildTreeAsync(_testDir, Array.Empty<string>(), TestContext.Current.CancellationToken);

        tree.Should().NotBeNull();
        var folders = tree!.ItemsSource.Items.OfType<FolderItem>().ToList();
        folders.Should().HaveCount(3);
        folders[0].Name.Should().Be("2026-09");
        folders[1].Name.Should().Be("2026-08");
        folders[2].Name.Should().Be("2025-12");
    }

    [Fact]
    public async Task BuildTreeAsyncWithHiddenFoldersIgnoresFolders()
    {
        var hiddenDir = Path.Combine(_testDir, ".annotations");
        Directory.CreateDirectory(hiddenDir);

        var visibleDir = Path.Combine(_testDir, "2026-08");
        Directory.CreateDirectory(visibleDir);

        var tree = await _sut.BuildTreeAsync(_testDir, Array.Empty<string>(), TestContext.Current.CancellationToken);

        tree.Should().NotBeNull();
        tree!.ItemsSource.Items.OfType<FolderItem>().Should().ContainSingle(f => f.Name == "2026-08");
        tree.ItemsSource.Items.OfType<FolderItem>().Should().NotContain(f => f.Name == ".annotations");
    }

    [Fact]
    public async Task BuildCalendarTreeAsyncWithValidDatesGeneratesExhaustiveStructure()
    {
        // MS-0: El motor lee años desde la fecha del directorio
        var fileDate = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Local);
        var dirPath = Path.Combine(_testDir, "Photos");
        Directory.CreateDirectory(dirPath);
        Directory.SetCreationTimeUtc(dirPath, fileDate.ToUniversalTime());
        Directory.SetLastWriteTimeUtc(dirPath, fileDate.ToUniversalTime());

        var calendar = await _sut.BuildCalendarTreeAsync(_testDir, Array.Empty<string>(), referenceToday: new DateTime(2027, 1, 1), ct: TestContext.Current.CancellationToken);

        calendar.Should().NotBeEmpty();
        var year2026 = calendar.FirstOrDefault(y => y.Name == "2026");
        year2026.Should().NotBeNull();
        year2026!.Kind.Should().Be(GroupKind.Year);

        var sepMonth = year2026.ItemsSource.Items.OfType<CalendarGroupItem>().FirstOrDefault(m => m.Month == 9);
        sepMonth.Should().NotBeNull();
        sepMonth!.Name.Should().Be("Septiembre");
        sepMonth.ItemsSource.Items.Should().NotBeEmpty();

        var week37 = sepMonth.ItemsSource.Items.OfType<CalendarGroupItem>().FirstOrDefault(w => w.WeekNumber == 37);
        week37.Should().NotBeNull();
        week37!.Kind.Should().Be(GroupKind.Week);
        week37.Name.Should().Be("07 sep - 13 sep (Semana 37)");

        // Toda semana generada debe contener rigurosamente sus 7 días completos
        week37.ItemsSource.Items.Should().HaveCount(7);
        
        var day7 = week37.ItemsSource.Items.OfType<CalendarGroupItem>().FirstOrDefault(d => d.Date?.Day == 7);
        day7.Should().NotBeNull();
        day7!.Name.Should().Be("07 sep, lunes");
        // Las colecciones nacen vacías esperando al Dual Channel Worker
        day7.ItemsSource.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildTreeAndCalendarWithHiddenPrefixIgnoresFolders()
    {
        var drawingDir = Path.Combine(_testDir, $"{Qapptia.Editor.Core.Constants.HiddenPrefixChar}dibujo");
        Directory.CreateDirectory(drawingDir);

        var normalDir = Path.Combine(_testDir, "capturas");
        Directory.CreateDirectory(normalDir);

        var tree = await _sut.BuildTreeAsync(_testDir, Array.Empty<string>(), TestContext.Current.CancellationToken);
        var calendar = await _sut.BuildCalendarTreeAsync(_testDir, Array.Empty<string>(), ct: TestContext.Current.CancellationToken);

        tree.Should().NotBeNull();
        tree!.ItemsSource.Items.OfType<FolderItem>().Should().ContainSingle(f => f.Name == "capturas");
        tree.ItemsSource.Items.OfType<FolderItem>().Should().NotContain(f => f.Name.StartsWith(Qapptia.Editor.Core.Constants.HiddenPrefixChar));
    }

    [Fact]
    public async Task BuildCalendarTreeAsyncWithTodayDateMarksTodayCorrectly()
    {
        var referenceToday = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Local);

        // Crear una carpeta en la fecha actual y otra en una fecha pasada
        var todayDir = Path.Combine(_testDir, "TodayFolder");
        Directory.CreateDirectory(todayDir);
        Directory.SetCreationTimeUtc(todayDir, referenceToday.ToUniversalTime());

        var pastDir = Path.Combine(_testDir, "PastFolder");
        Directory.CreateDirectory(pastDir);
        Directory.SetCreationTimeUtc(pastDir, new DateTime(2025, 5, 15, 10, 0, 0, DateTimeKind.Utc));

        var calendar = await _sut.BuildCalendarTreeAsync(_testDir, Array.Empty<string>(), weekLabel: null, referenceToday: referenceToday, ct: TestContext.Current.CancellationToken);

        var year2026 = calendar.OfType<CalendarGroupItem>().FirstOrDefault(y => y.Year == 2026);
        var year2025 = calendar.OfType<CalendarGroupItem>().FirstOrDefault(y => y.Year == 2025);
        year2026.Should().NotBeNull();
        year2026!.IsToday.Should().BeTrue();
        year2025.Should().NotBeNull();
        year2025!.IsToday.Should().BeFalse();

        var sepMonth = year2026.ItemsSource.Items.OfType<CalendarGroupItem>().FirstOrDefault(m => m.Month == 9);
        sepMonth.Should().NotBeNull();
        sepMonth!.IsToday.Should().BeTrue();

        var todayWeek = sepMonth.ItemsSource.Items.OfType<CalendarGroupItem>().FirstOrDefault(w => w.IsToday);
        todayWeek.Should().NotBeNull();
        todayWeek!.WeekNumber.Should().Be(System.Globalization.ISOWeek.GetWeekOfYear(referenceToday));

        var todayDay = todayWeek.ItemsSource.Items.OfType<CalendarGroupItem>().FirstOrDefault(d => d.IsToday);
        todayDay.Should().NotBeNull();
        todayDay!.Date?.Date.Should().Be(referenceToday.Date);
    }

    [Fact]
    public async Task StartIndexerWithValidRootsDispatchesFilesToBothViews()
    {
        // MS-0 y Fase 2 Integration Test
        var fileDate = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Local);
        var dirPath = Path.Combine(_testDir, "2026-09");
        Directory.CreateDirectory(dirPath);
        var filePath = Path.Combine(dirPath, "test.png");
        await File.WriteAllBytesAsync(filePath, s_minimalPng, TestContext.Current.CancellationToken);
        File.SetCreationTimeUtc(filePath, fileDate.ToUniversalTime());
        File.SetLastWriteTimeUtc(filePath, fileDate.ToUniversalTime());

        var treeRoot = await _sut.BuildTreeAsync(_testDir, Array.Empty<string>(), TestContext.Current.CancellationToken);
        var calendar = await _sut.BuildCalendarTreeAsync(_testDir, Array.Empty<string>(), referenceToday: new DateTime(2027, 1, 1), ct: TestContext.Current.CancellationToken);

        // Act - Inyectar despachador sincrono y esperar al worker LIFO/BFS
        _sut.StartIndexer(_testDir, calendar, treeRoot, a => a());
        
        // Esperamos a que el Background Worker consuma la cola
        await Task.Delay(800, TestContext.Current.CancellationToken);
        _sut.StopWatching();

        // Assert - Vista de Árbol (Tree)
        var treeFolder = treeRoot!.ItemsSource.Items.OfType<FolderItem>().First(f => f.Name == "2026-09");
        var treeFile = treeFolder.ItemsSource.Items.OfType<FileItem>().FirstOrDefault(f => f.Name == "test.png");
        treeFile.Should().NotBeNull("El despachador debió inyectar el archivo real en el FolderItem del árbol");

        // Assert - Vista de Calendario (Calendar)
        var calYear = calendar.OfType<CalendarGroupItem>().First(y => y.Year == 2026);
        var calMonth = calYear.ItemsSource.Items.OfType<CalendarGroupItem>().First(m => m.Month == 9);
        var calWeek = calMonth.ItemsSource.Items.OfType<CalendarGroupItem>().First(w => w.WeekNumber == 37);
        var calDay = calWeek.ItemsSource.Items.OfType<CalendarGroupItem>().First(d => d.Date?.Day == 7);
        var calFile = calDay.ItemsSource.Items.OfType<FileItem>().FirstOrDefault(f => f.Name == "test.png");
        calFile.Should().NotBeNull("El despachador debió inyectar el mismo archivo en el CalendarGroupItem del día");
    }

    [Fact]
    public async Task StartIndexerConfirmsEmptyDaysWhenGlobalIndexingCompletes()
    {
        // Directorio sin capturas: todos los días deben terminar confirmados como vacíos
        var referenceToday = new DateTime(2027, 1, 5, 12, 0, 0, DateTimeKind.Local);

        var treeRoot = await _sut.BuildTreeAsync(_testDir, Array.Empty<string>(), TestContext.Current.CancellationToken);
        var calendar = await _sut.BuildCalendarTreeAsync(_testDir, Array.Empty<string>(), referenceToday: referenceToday, ct: TestContext.Current.CancellationToken);

        var targetDay = calendar.OfType<CalendarGroupItem>()
            .SelectMany(y => y.Items).Cast<CalendarGroupItem>()
            .SelectMany(m => m.Items).Cast<CalendarGroupItem>()
            .SelectMany(w => w.Items).Cast<CalendarGroupItem>()
            .First(d => d.Date?.Date == referenceToday.Date);

        targetDay.IsEmptyConfirmed.Should().BeFalse("antes del cierre del indexado el día conserva su chevron");

        _sut.StartIndexer(_testDir, calendar, treeRoot, a => a());

        var retries = 40;
        while (!targetDay.IsScanCompleted && retries-- > 0)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
        _sut.StopWatching();

        targetDay.IsScanCompleted.Should().BeTrue("el cierre de la indexación global debe confirmar el estado del día");
        targetDay.IsEmptyConfirmed.Should().BeTrue("el día sin archivos queda confirmado como vacío");
        targetDay.IsDimmed.Should().BeTrue("el día vacío confirmado se atenúa");
        targetDay.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task StartIndexerWithMassiveFilesUsesO1CacheAndInjectsSuccessfully()
    {
        // Arrange
        var massiveDir = Path.Combine(_testDir, "MassiveFolder");
        Directory.CreateDirectory(massiveDir);
        
        // Creamos 1000 archivos vacíos rápidamente para forzar el batching del indexador
        for (int i = 0; i < 1000; i++)
        {
            await File.WriteAllBytesAsync(Path.Combine(massiveDir, $"img_{i}.png"), s_minimalPng, TestContext.Current.CancellationToken);
        }

        // MS-0: Construcción topológica que llena el _topologicalCache en O(1)
        var treeRoot = await _sut.BuildTreeAsync(_testDir, Array.Empty<string>(), TestContext.Current.CancellationToken);
        var massiveFolderItem = treeRoot!.ItemsSource.Items.OfType<FolderItem>().FirstOrDefault(f => f.Name == "MassiveFolder");
        massiveFolderItem!.Should().NotBeNull();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // Act - Inyectar y despachar
        _sut.StartIndexer(_testDir, Array.Empty<GroupItem>(), treeRoot, a => a());

        // Esperamos a que los lotes de inyección topológica reemplacen el Dummy node
        // Con el Cache O(1) esto debería ser < 500ms incluso para 1000 elementos
        int maxRetries = 20;
        while (massiveFolderItem!.ItemsSource.Items.Count < 1000 && maxRetries > 0)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            maxRetries--;
        }
        
        sw.Stop();
        _sut.StopWatching();

        // Assert
        massiveFolderItem.ItemsSource.Items.Should().HaveCount(1000, "El despachador O(1) debió inyectar los 1000 archivos exactos mediante el DispatchTopologyToTreeOnly");
        
        // El tiempo total debe ser saludable, demostrando ausencia de latencia cuadrática O(N^2)
        sw.ElapsedMilliseconds.Should().BeLessThan(2000, "El motor despachador O(1) no debe trabarse resolviendo 1000 inyecciones");
    }

    [Fact]
    public async Task BuildCalendarTreeAsyncWithDirectoryNameYearHeuristicsShouldIncludeExtractedYear()
    {
        // Directorio con nombre de año previo pero timestamps de creación actuales
        var dirPath = Path.Combine(_testDir, "2025-12");
        Directory.CreateDirectory(dirPath);
        Directory.SetCreationTimeUtc(dirPath, new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc));
        Directory.SetLastWriteTimeUtc(dirPath, new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc));

        var calendar = await _sut.BuildCalendarTreeAsync(_testDir, Array.Empty<string>(), referenceToday: new DateTime(2026, 9, 14), ct: TestContext.Current.CancellationToken);

        calendar.Should().NotBeEmpty();
        calendar.Should().Contain(y => y.Name == "2025", "La heurística debe inferir el año a partir del nombre del directorio");
    }

    [Fact]
    public void GetEffectiveDateWithCanonicalXmpMetadataShouldPreferEmbeddedDate()
    {
        var filePath = Path.Combine(_testDir, "xmp_test.png");
        var canonicalDate = new DateTime(2025, 12, 15, 10, 30, 0, DateTimeKind.Utc);
        
        var enrichedBytes = Qapptia.Core.Services.ImageMetadataService.InjectMetadata(s_minimalPng, Guid.NewGuid().ToString(), "image/png", canonicalDate);
        File.WriteAllBytes(filePath, enrichedBytes);
        
        // Asignar timestamps del sistema de archivos deliberadamente distintos
        File.SetCreationTimeUtc(filePath, new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(filePath, new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));

        var resolvedDate = Qapptia.Core.Services.ImageMetadataService.GetEffectiveDate(new FileInfo(filePath));

        resolvedDate.Date.Should().Be(canonicalDate.Date, "El metadato canónico XMP tiene máxima prioridad sobre el filesystem");
    }

    [Fact]
    public async Task StartWatchingFollowedByStartIndexerShouldNotDisposeWatcherAndTriggerOnChange()
    {
        var tcs = new TaskCompletionSource<bool>();
        _sut.StartWatching(_testDir, () => tcs.TrySetResult(true));

        var treeRoot = await _sut.BuildTreeAsync(_testDir, Array.Empty<string>(), TestContext.Current.CancellationToken);
        _sut.StartIndexer(_testDir, Array.Empty<GroupItem>(), treeRoot, a => a());

        // Crear un nuevo archivo en el directorio observado
        var newFile = Path.Combine(_testDir, "captured_test.png");
        await File.WriteAllBytesAsync(newFile, s_minimalPng, TestContext.Current.CancellationToken);

        // Esperar la notificación debounced del watcher (esperamos hasta 2500ms con timeout)
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(2500, TestContext.Current.CancellationToken));
        completedTask.Should().Be(tcs.Task, "El FileSystemWatcher debe permanecer activo tras StartIndexer y disparar la notificación");
    }

    [Fact]
    public void InsertFilesSortedShouldMaintainSubgroupsFirstAndFilesDescendingByEffectiveDate()
    {
        var parent = new FolderItem { Name = "Parent", FullPath = "d:/test/parent" };
        var subFolder = new FolderItem { Name = "SubFolder", FullPath = "d:/test/parent/sub", Parent = parent };
        parent.ItemsSource.AddRange(new[] { subFolder });

        var fileOld = new FileItem { Name = "old.png", FullPath = "d:/test/parent/old.png", EffectiveDateUtc = new DateTime(2026, 9, 1), Parent = parent };
        var fileMid = new FileItem { Name = "mid.png", FullPath = "d:/test/parent/mid.png", EffectiveDateUtc = new DateTime(2026, 9, 5), Parent = parent };
        var fileNew = new FileItem { Name = "new.png", FullPath = "d:/test/parent/new.png", EffectiveDateUtc = new DateTime(2026, 9, 10), Parent = parent };

        NavigationService.InsertFilesSorted(parent, new[] { fileOld, fileNew, fileMid });

        parent.ItemsSource.Items.Should().HaveCount(4);
        parent.ItemsSource.Items[0].Should().Be(subFolder, "Las subcarpetas siempre deben estar al inicio");
        parent.ItemsSource.Items[1].Should().Be(fileNew, "El archivo del 10 de sep debe ser el primero de los archivos");
        parent.ItemsSource.Items[2].Should().Be(fileMid, "El archivo del 5 de sep debe ser el segundo");
        parent.ItemsSource.Items[3].Should().Be(fileOld, "El archivo del 1 de sep debe ser el último");
    }
}
