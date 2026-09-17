using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Qapptia.App.Editor.ViewModels;
using Qapptia.Editor.Models.Navigation;
using Qapptia.Editor.Services;
using Xunit;

namespace Qapptia.Editor.Tests.ViewModels;

public sealed class SidebarViewModelTests : IDisposable
{
    private readonly string _testDir;
    private readonly EditorStateService _stateService;
    private readonly NavigationService _navigationService;

    public SidebarViewModelTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Qapptia_SidebarTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _stateService = new EditorStateService(_testDir, "state.json");
        _navigationService = new NavigationService();
    }

    private SidebarViewModel CreateViewModel(INavigationService? nav = null) =>
        new(nav ?? _navigationService, _stateService, _testDir, uiDispatcher: a => a());

    public void Dispose()
    {
        try
        {
            _navigationService.Dispose();
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task LoadSidebarImagesAsyncWithValidPathPopulatesFolders()
    {
        var subDir = Path.Combine(_testDir, "Screenshots");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "shot1.png");
        File.WriteAllBytes(testFile, new byte[] { 1, 2, 3 });

        var state = _stateService.Load();
        state.Layout.SidebarViewMode = "Tree";
        _stateService.Save(state);

        var vm = CreateViewModel();

        await vm.LoadSidebarImagesAsync();

        vm.SidebarGroups.Should().NotBeEmpty();
        var foundNode = vm.FindNodeByPath(subDir);
        foundNode.Should().NotBeNull();
        foundNode.Should().BeOfType<FolderItem>();
    }

    [Fact]
    public void SelectedNodeWhenChangedRaisesFileSelectedEvent()
    {
        var vm = CreateViewModel();
        FileItem? selectedFile = null;
        vm.FileSelected += (s, file) => selectedFile = file;

        var dummyFile = new FileItem { Name = "test.png", FullPath = Path.Combine(_testDir, "test.png") };
        vm.SelectedNode = dummyFile;

        selectedFile.Should().Be(dummyFile);
    }

    [Fact]
    public async Task SetViewModeWhenChangedSwitchesModeAndPersistsState()
    {
        var vm = CreateViewModel();
        vm.ViewMode.Should().Be(SidebarViewMode.Calendar);
        vm.IsCalendarViewActive.Should().BeTrue();
        vm.IsTreeViewActive.Should().BeFalse();

        await vm.SetViewMode(SidebarViewMode.Tree);

        vm.ViewMode.Should().Be(SidebarViewMode.Tree);
        vm.IsTreeViewActive.Should().BeTrue();
        vm.IsCalendarViewActive.Should().BeFalse();

        var state = _stateService.Load();
        state.Layout.SidebarViewMode.Should().Be("Tree");

        // La persistencia es no bloqueante (diferida a segundo plano): validar que el
        // escritor coalescido materializa el snapshot en disco en un margen breve
        var statePath = Path.Combine(_testDir, "state.json");
        var deadline = DateTime.UtcNow.AddSeconds(2);
        string diskJson = string.Empty;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (File.Exists(statePath))
                {
                    diskJson = await File.ReadAllTextAsync(statePath, TestContext.Current.CancellationToken);
                    if (diskJson.Contains("\"sidebar_view_mode\": \"Tree\"", StringComparison.Ordinal)) break;
                }
            }
            catch (IOException) { /* Escritura en curso: reintentar sondeo */ }
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        diskJson.Should().Contain("\"sidebar_view_mode\": \"Tree\"", "La preferencia de vista debe materializarse en disco tras el guardado diferido");
    }


    [Fact]
    public async Task LoadSidebarImagesAsyncShouldRespectPersistedExpandedAndCollapsedFolders()
    {
        var otherDir = Path.Combine(_testDir, "2025-10");
        Directory.CreateDirectory(otherDir);

        var subDir = Path.Combine(_testDir, "2026-09");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "shot.png");
        File.WriteAllBytes(testFile, new byte[] { 1, 2 });

        // Guardar estado con modo Árbol: otherDir está expandida, pero subDir fue colapsada por el usuario (no está en ExpandedFolders)
        var state = _stateService.Load();
        state.Layout.SidebarViewMode = "Tree";
        state.Layout.ExpandedFolders.Clear();
        state.Layout.ExpandedFolders.Add(NavigationService.NormalizePath(otherDir));
        state.Session.LastSelectedFile = NavigationService.NormalizePath(testFile);
        _stateService.Save(state);

        var vm = CreateViewModel();
        await vm.LoadSidebarImagesAsync();

        vm.SidebarGroups.Should().NotBeEmpty();
        var root = vm.SidebarGroups[0];
        root.IsExpanded.Should().BeTrue("El nodo raíz debe expandirse");

        // 1. La carpeta colapsada por el usuario debe permanecer colapsada al inicio
        var subFolder = root.Items.OfType<GroupItem>().FirstOrDefault(f => f.Name == "2026-09");
        subFolder.Should().NotBeNull();
        subFolder!.IsExpanded.Should().BeFalse("La carpeta colapsada por el usuario debe permanecer colapsada");

        // 2. Al expandir el nodo después, el archivo debe auto-seleccionarse
        subFolder.IsExpanded = true;
        var selected = vm.SelectedNode as FileItem;
        selected.Should().NotBeNull("Al expandir el nodo contenedor, el archivo activo debe seleccionarse automáticamente");
        NavigationService.NormalizePath(selected!.FullPath).Should().Be(NavigationService.NormalizePath(testFile));

        // 3. La otra carpeta previamente expandida por el usuario debe permanecer expandida
        var otherFolder = root.Items.OfType<GroupItem>().FirstOrDefault(f => f.Name == "2025-10");
        otherFolder.Should().NotBeNull();
        otherFolder!.IsExpanded.Should().BeTrue("Las carpetas previamente expandidas por el usuario deben mantenerse expandidas");
    }

    [Fact]
    public async Task LoadSidebarImagesAsyncInCalendarModeShouldRespectPersistedExpandedCalendarGroups()
    {
        var subDir = Path.Combine(_testDir, "2026-09");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "shot.png");
        File.WriteAllBytes(testFile, new byte[] { 1, 2 });
        var localDate = new FileInfo(testFile).LastWriteTime;

        // Guardar estado con modo Calendario y solo cal://2025 expandido
        var state = _stateService.Load();
        state.Layout.SidebarViewMode = "Calendar";
        state.Layout.ExpandedCalendarGroups.Clear();
        state.Layout.ExpandedCalendarGroups.Add("cal://2025");
        state.Session.LastSelectedFile = NavigationService.NormalizePath(testFile);
        _stateService.Save(state);

        var vm = CreateViewModel();
        await vm.LoadSidebarImagesAsync();

        var yearGroup2025 = vm.SidebarGroups.OfType<CalendarGroupItem>().FirstOrDefault(y => y.Year == 2025);
        if (yearGroup2025 != null)
        {
            yearGroup2025.IsExpanded.Should().BeTrue("El grupo persistido en ExpandedCalendarGroups debe mantenerse expandido");
        }
    }

    [Fact]
    public async Task LoadSidebarImagesAsyncWithPersistedSelectionBindsLateNode()
    {
        var mockNav = new Mock<INavigationService>();
        var state = _stateService.Load();
        state.Layout.SidebarViewMode = "Tree";
        state.Session.LastSelectedFile = "C:/fake/path.png";
        _stateService.Save(state);

        using var sut = CreateViewModel(mockNav.Object);
        
        Action<FileItem>? capturedCallback = null;

        mockNav.Setup(m => m.BuildTreeAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new FolderItem { Name = "Root" });

        mockNav.Setup(m => m.StartIndexer(It.IsAny<string>(), It.IsAny<IEnumerable<GroupItem>>(), It.IsAny<GroupItem>(), It.IsAny<Action<Action>>(), It.IsAny<Action<FileItem>>()))
               .Callback<string, IEnumerable<GroupItem>, GroupItem?, Action<Action>?, Action<FileItem>?>((_, _, _, _, cb) => capturedCallback = cb);

        await sut.LoadSidebarImagesCoreAsync(expandAncestorsForSelected: true);

        capturedCallback.Should().NotBeNull("El callback de late binding debe haber sido inyectado");

        var incomingFile = new FileItem { FullPath = "C:/fake/path.png" };
        capturedCallback!(incomingFile);

        sut.SelectedNode.Should().Be(incomingFile, "El late binding debe haber atrapado el archivo entrante");
    }

    [Fact]
    public async Task SetViewModeWhenSwappingViewsShouldRetainSelectedFile()
    {
        var subDir = Path.Combine(_testDir, "2026-09");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "capture_swap.png");
        File.WriteAllBytes(testFile, new byte[] { 1, 2, 3 });

        var state = _stateService.Load();
        state.Layout.SidebarViewMode = "Calendar";
        state.Session.LastSelectedFile = NavigationService.NormalizePath(testFile);
        _stateService.Save(state);

        var vm = CreateViewModel();
        await vm.LoadSidebarImagesAsync();

        // 1. Inicialmente en modo Calendario el archivo debe estar seleccionado
        vm.ViewMode.Should().Be(SidebarViewMode.Calendar);
        vm.SelectedNode.Should().NotBeNull("Debe haber un archivo seleccionado en la vista calendario");
        NavigationService.NormalizePath(vm.SelectedNode!.FullPath).Should().Be(NavigationService.NormalizePath(testFile));

        // 2. Act: Cambiar a la vista Árbol
        await vm.SetViewMode(SidebarViewMode.Tree);

        // 3. Assert: El mismo archivo debe permanecer seleccionado en la vista Árbol y sus ancestros de carpeta expandidos
        vm.ViewMode.Should().Be(SidebarViewMode.Tree);
        vm.SelectedNode.Should().NotBeNull("El archivo seleccionado debe mantenerse al cambiar a la vista árbol");
        NavigationService.NormalizePath(vm.SelectedNode!.FullPath).Should().Be(NavigationService.NormalizePath(testFile));

        var root = vm.SidebarGroups.FirstOrDefault();
        root.Should().NotBeNull();
        root!.IsExpanded.Should().BeTrue("El nodo raíz debe estar expandido en la vista árbol");
        var folder = root.Items.OfType<FolderItem>().FirstOrDefault(f => f.Name == "2026-09");
        folder.Should().NotBeNull();
        folder!.IsExpanded.Should().BeTrue("La carpeta contenedora debe estar auto-expandida en la vista árbol");

        // 4. Act: Cambiar nuevamente de regreso a la vista Calendario
        await vm.SetViewMode(SidebarViewMode.Calendar);

        // 5. Assert: El archivo debe continuar seleccionado en la vista Calendario
        vm.ViewMode.Should().Be(SidebarViewMode.Calendar);
        vm.SelectedNode.Should().NotBeNull("El archivo seleccionado debe mantenerse al regresar a la vista calendario");
        NavigationService.NormalizePath(vm.SelectedNode!.FullPath).Should().Be(NavigationService.NormalizePath(testFile));
    }

    [Fact]
    public void GroupNodeWhenExpandedAndScanNotCompletedShouldShowLoadingSpinner()
    {
        var vm = CreateViewModel();

        // 1. Nodo de tipo Folder
        var folder = new FolderItem { Name = "UnscannedFolder", FullPath = Path.Combine(_testDir, "UnscannedFolder"), IsScanCompleted = false };
        vm.SidebarGroups.Add(folder);

        folder.IsExpanded = true;
        folder.IsLoading.Should().BeTrue("Cualquier carpeta expandida con escaneo pendiente debe mostrar spinner");

        folder.IsExpanded = false;
        folder.IsLoading.Should().BeFalse("Al colapsar el nodo, el spinner debe apagarse");

        // 2. Nodo de tipo Calendario (Día)
        var dayGroup = new CalendarGroupItem(GroupKind.Day) { Name = "UnscannedDay", FullPath = "cal://2025/12/w50/2025-12-10", IsScanCompleted = false };
        vm.SidebarGroups.Add(dayGroup);

        dayGroup.IsExpanded = true;
        dayGroup.IsLoading.Should().BeTrue("Cualquier nodo de calendario expandido con escaneo pendiente debe mostrar spinner");

        dayGroup.IsExpanded = false;
        dayGroup.IsLoading.Should().BeFalse("Al colapsar el nodo de calendario, el spinner debe apagarse");
    }

    [Fact]
    public async Task InjectCreatedFileWhenFolderIsExpandedAddsImmediatelyToFlatTreeItems()
    {
        var subDir = Path.Combine(_testDir, "Screenshots");
        Directory.CreateDirectory(subDir);

        var state = _stateService.Load();
        state.Layout.SidebarViewMode = "Tree";
        state.Layout.ExpandedFolders.Add(NavigationService.NormalizePath(_testDir));
        state.Layout.ExpandedFolders.Add(NavigationService.NormalizePath(subDir));
        _stateService.Save(state);

        using var vm = CreateViewModel();
        await vm.LoadSidebarImagesAsync();

        var subFolderNode = vm.FindNodeByPath(subDir) as FolderItem;
        subFolderNode.Should().NotBeNull();
        subFolderNode!.IsExpanded.Should().BeTrue();

        int initialFlatCount = vm.FlatTreeItems.Count;

        var newCapture = Path.Combine(subDir, "capture_realtime.png");
        await File.WriteAllBytesAsync(newCapture, new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);

        vm.InjectCreatedFile(newCapture);

        vm.FlatTreeItems.Count.Should().Be(initialFlatCount + 1, "La nueva captura debe proyectarse inmediatamente en la lista plana si la carpeta está expandida");
        vm.FlatTreeItems.OfType<FileItem>().Should().Contain(f => f.FullPath == newCapture);
    }

    [Fact]
    public async Task InjectCreatedFileWhenFolderIsCollapsedDoesNotShowInFlatTreeUntilExpanded()
    {
        var subDir = Path.Combine(_testDir, "Screenshots");
        Directory.CreateDirectory(subDir);

        var state = _stateService.Load();
        state.Layout.SidebarViewMode = "Tree";
        state.Layout.ExpandedFolders.Add(NavigationService.NormalizePath(_testDir));
        _stateService.Save(state);

        using var vm = CreateViewModel();
        await vm.LoadSidebarImagesAsync();

        var subFolderNode = vm.FindNodeByPath(subDir) as FolderItem;
        subFolderNode.Should().NotBeNull();
        subFolderNode!.IsExpanded.Should().BeFalse();

        int initialFlatCount = vm.FlatTreeItems.Count;

        var newCapture = Path.Combine(subDir, "capture_hidden.png");
        await File.WriteAllBytesAsync(newCapture, new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);

        vm.InjectCreatedFile(newCapture);

        vm.FlatTreeItems.Count.Should().Be(initialFlatCount, "La captura en una carpeta colapsada no debe proyectarse en la lista plana todavía");
        subFolderNode.ItemsSource.Items.OfType<FileItem>().Should().Contain(f => f.FullPath == newCapture);

        subFolderNode.IsExpanded = true;

        vm.FlatTreeItems.OfType<FileItem>().Should().Contain(f => f.FullPath == newCapture);
    }

    [Fact]
    public async Task InjectCreatedFileWhenMultipleFoldersExistShouldNotTriggerSpinnersOnOtherFolders()
    {
        var folderA = Path.Combine(_testDir, "FolderA");
        var folderB = Path.Combine(_testDir, "FolderB");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);

        var state = _stateService.Load();
        state.Layout.SidebarViewMode = "Tree";
        state.Layout.ExpandedFolders.Add(NavigationService.NormalizePath(_testDir));
        state.Layout.ExpandedFolders.Add(NavigationService.NormalizePath(folderA));
        state.Layout.ExpandedFolders.Add(NavigationService.NormalizePath(folderB));
        _stateService.Save(state);

        using var vm = CreateViewModel();
        await vm.LoadSidebarImagesAsync();

        var nodeA = vm.FindNodeByPath(folderA) as FolderItem;
        var nodeB = vm.FindNodeByPath(folderB) as FolderItem;
        nodeA.Should().NotBeNull();
        nodeB.Should().NotBeNull();

        nodeA!.IsLoading = false;
        nodeB!.IsLoading = false;

        var newCapture = Path.Combine(folderA, "capture_isolated.png");
        await File.WriteAllBytesAsync(newCapture, new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);

        bool handled = vm.InjectCreatedFile(newCapture);
        handled.Should().BeTrue();

        nodeB.IsLoading.Should().BeFalse("Las carpetas hermanas no deben activar spinners al crearse un archivo en otra carpeta");
        nodeA.IsLoading.Should().BeFalse("La carpeta receptora no debe quedar con spinner bloqueado");
        nodeA.ItemsSource.Items.OfType<FileItem>().Should().Contain(f => f.FullPath == newCapture);
    }

    [Fact]
    public async Task FilesInFolderNodeShouldBeOrderedFromMostRecentToOldest()
    {
        var subDir = Path.Combine(_testDir, "Screenshots");
        Directory.CreateDirectory(subDir);

        var fileOld = Path.Combine(subDir, "old_file.png");
        var fileMid = Path.Combine(subDir, "mid_file.png");
        var fileNew = Path.Combine(subDir, "new_file.png");

        await File.WriteAllBytesAsync(fileOld, new byte[] { 1 }, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(fileMid, new byte[] { 2 }, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(fileNew, new byte[] { 3 }, TestContext.Current.CancellationToken);

        File.SetCreationTimeUtc(fileOld, new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc));
        File.SetCreationTimeUtc(fileMid, new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc));
        File.SetCreationTimeUtc(fileNew, new DateTime(2026, 9, 10, 18, 0, 0, DateTimeKind.Utc));

        var state = _stateService.Load();
        state.Layout.SidebarViewMode = "Tree";
        state.Layout.ExpandedFolders.Add(NavigationService.NormalizePath(_testDir));
        state.Layout.ExpandedFolders.Add(NavigationService.NormalizePath(subDir));
        _stateService.Save(state);

        using var vm = CreateViewModel();
        await vm.LoadSidebarImagesAsync();

        var subFolderNode = vm.FindNodeByPath(subDir) as FolderItem;
        subFolderNode.Should().NotBeNull();

        // Inyectar en orden desordenado (antiguo, nuevo, intermedio)
        vm.InjectCreatedFile(fileOld);
        vm.InjectCreatedFile(fileNew);
        vm.InjectCreatedFile(fileMid);

        var files = subFolderNode!.ItemsSource.Items.OfType<FileItem>().ToList();
        files.Should().HaveCount(3);
        files[0].FullPath.Should().Be(fileNew, "El archivo más reciente (18:00) debe ser el primero");
        files[1].FullPath.Should().Be(fileMid, "El archivo intermedio (12:00) debe ser el segundo");
        files[2].FullPath.Should().Be(fileOld, "El archivo más antiguo (08:00) debe ser el último");

        var flatFiles = vm.FlatTreeItems.OfType<FileItem>().ToList();
        flatFiles.Should().HaveCount(3);
        flatFiles[0].FullPath.Should().Be(fileNew);
        flatFiles[1].FullPath.Should().Be(fileMid);
        flatFiles[2].FullPath.Should().Be(fileOld);

        // Inyectar un archivo aún más nuevo (20:00)
        var fileNewest = Path.Combine(subDir, "newest_file.png");
        await File.WriteAllBytesAsync(fileNewest, new byte[] { 4 }, TestContext.Current.CancellationToken);
        File.SetCreationTimeUtc(fileNewest, new DateTime(2026, 9, 10, 20, 0, 0, DateTimeKind.Utc));

        vm.InjectCreatedFile(fileNewest);

        var updatedFiles = subFolderNode.ItemsSource.Items.OfType<FileItem>().ToList();
        updatedFiles.Should().HaveCount(4);
        updatedFiles[0].FullPath.Should().Be(fileNewest, "La nueva captura (20:00) debe insertarse en la primera posición");
        updatedFiles[1].FullPath.Should().Be(fileNew);
        updatedFiles[2].FullPath.Should().Be(fileMid);
        updatedFiles[3].FullPath.Should().Be(fileOld);
    }

    [Fact]
    public async Task FilesInCalendarDayNodeShouldBeOrderedFromMostRecentToOldest()
    {
        var targetDate = new DateTime(2026, 9, 15);
        var fileMorning = Path.Combine(_testDir, "morning.png");
        var fileNoon = Path.Combine(_testDir, "noon.png");
        var fileNight = Path.Combine(_testDir, "night.png");

        await File.WriteAllBytesAsync(fileMorning, new byte[] { 1 }, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(fileNoon, new byte[] { 2 }, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(fileNight, new byte[] { 3 }, TestContext.Current.CancellationToken);

        File.SetCreationTimeUtc(fileMorning, new DateTime(2026, 9, 15, 9, 0, 0, DateTimeKind.Utc));
        File.SetCreationTimeUtc(fileNoon, new DateTime(2026, 9, 15, 13, 0, 0, DateTimeKind.Utc));
        File.SetCreationTimeUtc(fileNight, new DateTime(2026, 9, 15, 21, 0, 0, DateTimeKind.Utc));

        var state = _stateService.Load();
        state.Layout.SidebarViewMode = "Calendar";
        _stateService.Save(state);

        using var vm = CreateViewModel();
        await vm.LoadSidebarImagesAsync();

        var dayNode = vm.CalendarGroups.SelectMany(y => y.ItemsSource.Items)
            .OfType<CalendarGroupItem>()
            .SelectMany(m => m.ItemsSource.Items)
            .OfType<CalendarGroupItem>()
            .SelectMany(w => w.ItemsSource.Items)
            .OfType<CalendarGroupItem>()
            .FirstOrDefault(d => d.Date.HasValue && d.Date.Value.Date == targetDate.Date);

        dayNode.Should().NotBeNull();

        // Inyectar en orden desordenado y validar ordenación descendente automática
        vm.InjectCreatedFile(fileMorning);
        vm.InjectCreatedFile(fileNight);
        vm.InjectCreatedFile(fileNoon);

        var dayFiles = dayNode!.ItemsSource.Items.OfType<FileItem>().ToList();
        dayFiles.Should().HaveCount(3);
        dayFiles[0].FullPath.Should().Be(fileNight, "El archivo nocturno (21:00) debe estar primero");
        dayFiles[1].FullPath.Should().Be(fileNoon, "El archivo del mediodía (13:00) debe estar segundo");
        dayFiles[2].FullPath.Should().Be(fileMorning, "El archivo de la mañana (09:00) debe estar al final");
    }

    [Fact]
    public async Task InjectCreatedFileWhenFileIsAlreadySelectedShouldPreserveSelection()
    {
        var subDir = Path.Combine(_testDir, "Screenshots");
        Directory.CreateDirectory(subDir);
        var existingFile = Path.Combine(subDir, "existing.png");
        await File.WriteAllBytesAsync(existingFile, new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);

        var state = _stateService.Load();
        state.Layout.SidebarViewMode = "Tree";
        state.Session.LastSelectedFile = existingFile;
        state.Layout.ExpandedFolders.Add(NavigationService.NormalizePath(_testDir));
        state.Layout.ExpandedFolders.Add(NavigationService.NormalizePath(subDir));
        _stateService.Save(state);

        using var vm = CreateViewModel();
        await vm.LoadSidebarImagesAsync();

        // Localizar el archivo existente y confirmar que está seleccionado al arrancar
        var folder = vm.FindNodeByPath(subDir) as FolderItem;
        folder.Should().NotBeNull();
        folder!.IsExpanded = true;

        var existingFileNode = folder.ItemsSource.Items.OfType<FileItem>().FirstOrDefault(f => string.Equals(f.FullPath, existingFile, StringComparison.OrdinalIgnoreCase));
        existingFileNode.Should().NotBeNull();

        vm.SelectedNode.Should().Be(existingFileNode);
        vm.ActiveFilePath.Should().Be(existingFile);

        // Crear una nueva captura en la misma carpeta
        var newCapture = Path.Combine(subDir, "new_capture.png");
        await File.WriteAllBytesAsync(newCapture, new byte[] { 4, 5, 6 }, TestContext.Current.CancellationToken);

        // Inyectar el nuevo archivo mediante el mecanismo reactivo
        bool injected = vm.InjectCreatedFile(newCapture);
        injected.Should().BeTrue();

        // Validar que el archivo previamente seleccionado SIGUE seleccionado y no se deseleccionó
        vm.SelectedNode.Should().NotBeNull("La inyección de un nuevo archivo no debe limpiar la selección");
        vm.SelectedNode.Should().Be(existingFileNode, "El archivo seleccionado previamente debe mantenerse seleccionado");
        vm.ActiveFilePath.Should().Be(existingFile);
    }

    [Fact]
    public async Task InitialLoadInCalendarViewWithPersistedAncestorsShouldSelectFile()
    {
        var subDir = Path.Combine(_testDir, "Screenshots");
        Directory.CreateDirectory(subDir);
        var existingFile = Path.Combine(subDir, "existing.png");
        await File.WriteAllBytesAsync(existingFile, new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);

        var effDate = Qapptia.Core.Services.ImageMetadataService.GetEffectiveDate(existingFile);
        var localDate = effDate.Kind == DateTimeKind.Utc ? effDate.ToLocalTime() : effDate;

        var state = _stateService.Load();
        state.Layout.SidebarViewMode = "Calendar";
        state.Session.LastSelectedFile = existingFile;
        foreach (var uri in SidebarViewModel.GetCalendarAncestorUris(existingFile))
        {
            state.Layout.ExpandedCalendarGroups.Add(uri);
        }
        _stateService.Save(state);

        using var vm = CreateViewModel();
        await vm.LoadSidebarImagesAsync();

        vm.SelectedNode.Should().NotBeNull("El archivo de sesión debe estar seleccionado en la vista Calendario cuando sus ancestros están expandidos");
        vm.SelectedNode.Should().BeOfType<FileItem>();
        ((FileItem)vm.SelectedNode!).FullPath.Should().Be(existingFile);
        vm.ActiveFlatItems.Should().Contain(item => item is FileItem && ((FileItem)item).FullPath == existingFile);
        vm.ActiveFlatItems.Any(item => ReferenceEquals(item, vm.SelectedNode)).Should().BeTrue("El nodo seleccionado debe ser exactamente el elemento proyectado en ActiveFlatItems");
    }

    [Fact]
    public async Task TreeModeWhenFolderIsCollapsedAndSwappingViewsShouldNeverAutoExpandFolder()
    {
        var otherDir = Path.Combine(_testDir, "2025-10");
        Directory.CreateDirectory(otherDir);
        var subDir = Path.Combine(_testDir, "2026-09");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "shot.png");
        await File.WriteAllBytesAsync(testFile, new byte[] { 1, 2 }, TestContext.Current.CancellationToken);

        var state = _stateService.Load();
        state.Layout.SidebarViewMode = "Tree";
        state.Layout.ExpandedFolders.Clear();
        state.Layout.ExpandedFolders.Add(NavigationService.NormalizePath(otherDir));
        state.Session.LastSelectedFile = NavigationService.NormalizePath(testFile);
        _stateService.Save(state);

        using var vm = CreateViewModel();
        await vm.LoadSidebarImagesAsync();

        var root = vm.SidebarGroups.FirstOrDefault();
        var subFolder = root?.Items.OfType<GroupItem>().FirstOrDefault(f => f.Name == "2026-09");
        subFolder.Should().NotBeNull();
        subFolder!.IsExpanded.Should().BeFalse("El nodo colapsado debe iniciar colapsado");
        vm.SelectedNode.Should().BeNull("No debe haber nodo seleccionado en el árbol si su carpeta está colapsada");

        // Alternar a Calendario y regresar a Árbol
        await vm.SetViewMode(SidebarViewMode.Calendar);
        await vm.SetViewMode(SidebarViewMode.Tree);

        subFolder.IsExpanded.Should().BeFalse("El nodo NO debe auto-expandirse tras alternar entre vistas");
        vm.SelectedNode.Should().BeNull("No debe auto-seleccionarse si el nodo permanece colapsado");

        // Al expandir manualmente, debe auto-seleccionarse
        subFolder.IsExpanded = true;
        vm.SelectedNode.Should().NotBeNull("Al expandir el nodo contenedor, el archivo activo debe seleccionarse automáticamente");
        NavigationService.NormalizePath(((FileItem)vm.SelectedNode!).FullPath).Should().Be(NavigationService.NormalizePath(testFile));
    }

    [Fact]
    public async Task CalendarModeWhenGroupIsCollapsedAndSwappingViewsShouldNeverAutoExpandGroup()
    {
        var subDir = Path.Combine(_testDir, "2026-09");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "shot.png");
        await File.WriteAllBytesAsync(testFile, new byte[] { 1, 2 }, TestContext.Current.CancellationToken);
        var localDate = new FileInfo(testFile).LastWriteTime;

        var state = _stateService.Load();
        state.Layout.SidebarViewMode = "Calendar";
        state.Layout.ExpandedCalendarGroups.Clear();
        state.Layout.ExpandedCalendarGroups.Add("cal://2025");
        state.Session.LastSelectedFile = NavigationService.NormalizePath(testFile);
        _stateService.Save(state);

        using var vm = CreateViewModel();
        await vm.LoadSidebarImagesAsync();

        var yearGroup2026 = vm.CalendarGroups.OfType<CalendarGroupItem>().FirstOrDefault(y => y.Year == localDate.Year);
        yearGroup2026.Should().NotBeNull();
        yearGroup2026!.IsExpanded.Should().BeFalse("El año que no estaba en ExpandedCalendarGroups debe iniciar colapsado");
        vm.SelectedNode.Should().BeNull("No debe haber nodo seleccionado si el año está colapsado");

        // Alternar a Árbol y regresar a Calendario
        await vm.SetViewMode(SidebarViewMode.Tree);
        await vm.SetViewMode(SidebarViewMode.Calendar);

        yearGroup2026.IsExpanded.Should().BeFalse("El año NO debe auto-expandirse tras alternar entre vistas");
        vm.SelectedNode.Should().BeNull("No debe auto-seleccionarse si el grupo permanece colapsado");

        // Al expandir el año y mes manualmente, se debe auto-seleccionar
        yearGroup2026.IsExpanded = true;
        var monthGroup = yearGroup2026.ItemsSource.Items.OfType<CalendarGroupItem>().FirstOrDefault(m => m.Month == localDate.Month);
        monthGroup.Should().NotBeNull();
        monthGroup!.IsExpanded = true;

        CalendarGroupItem? targetWeek = null;
        CalendarGroupItem? targetDay = null;
        foreach (var w in monthGroup.ItemsSource.Items.OfType<CalendarGroupItem>())
        {
            var d = w.ItemsSource.Items.OfType<CalendarGroupItem>().FirstOrDefault(x => x.Date.HasValue && x.Date.Value.Date == localDate.Date);
            if (d != null)
            {
                targetWeek = w;
                targetDay = d;
                break;
            }
        }

        targetWeek.Should().NotBeNull();
        targetDay.Should().NotBeNull();
        targetWeek!.IsExpanded = true;
        targetDay!.IsExpanded = true;

        vm.SelectedNode.Should().NotBeNull("Al expandir los grupos contenedores, el archivo activo debe seleccionarse automáticamente");
        NavigationService.NormalizePath(((FileItem)vm.SelectedNode!).FullPath).Should().Be(NavigationService.NormalizePath(testFile));
    }

    [Fact]
    public async Task InjectCreatedFileInCalendarViewWhenDayNodeDoesNotExistShouldDynamicallyCreateDayNodeAndInsertFile()
    {
        using var vm = CreateViewModel();
        await vm.LoadSidebarImagesAsync();

        vm.ViewMode.Should().Be(SidebarViewMode.Calendar);

        // Crear un archivo en un subdirectorio físico nuevo (que el árbol no conoce)
        var newSubDir = Path.Combine(_testDir, "UnscannedSubDir");
        Directory.CreateDirectory(newSubDir);
        var testFile = Path.Combine(newSubDir, "capture_new_day.png");
        await File.WriteAllBytesAsync(testFile, new byte[] { 42, 43 }, TestContext.Current.CancellationToken);

        var fileDate = DateTime.Today;
        File.SetLastWriteTime(testFile, fileDate.AddHours(10));

        // Inyectar el archivo como si el FileWatcher lo hubiera detectado
        bool handled = vm.InjectCreatedFile(testFile);

        handled.Should().BeTrue("En vista calendario, la inyección debe ser exitosa y crear dinámicamente el nodo día sin recargar");

        // Buscar el nodo día creado
        var dayNode = _navigationService.FindCalendarDay(fileDate.Date);
        dayNode.Should().NotBeNull();
        dayNode!.Kind.Should().Be(GroupKind.Day);

        var containedFile = dayNode.ItemsSource.Items.OfType<FileItem>()
            .FirstOrDefault(f => string.Equals(NavigationService.NormalizePath(f.FullPath), NavigationService.NormalizePath(testFile), StringComparison.OrdinalIgnoreCase));
        containedFile.Should().NotBeNull();
        containedFile!.Name.Should().Be("capture_new_day.png");
    }

    [Fact]
    public async Task InjectCreatedFileOnNewDayShouldDynamicallyCreateDayNodeWithoutReloading()
    {
        using var vm = CreateViewModel();
        await vm.LoadSidebarImagesAsync();

        var targetDate = DateTime.Today.AddDays(-10); // Día que no existía si la carpeta estaba vacía
        var newSubDir = Path.Combine(_testDir, "PastDayCapture");
        Directory.CreateDirectory(newSubDir);
        var testFile = Path.Combine(newSubDir, "past_capture.png");
        await File.WriteAllBytesAsync(testFile, new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        File.SetCreationTime(testFile, targetDate);
        File.SetLastWriteTime(testFile, targetDate);
        Qapptia.Core.Services.ImageMetadataService.InvalidateEffectiveDateCache(testFile);

        bool handled = vm.InjectCreatedFile(testFile);
        handled.Should().BeTrue();

        var dayNode = _navigationService.FindCalendarDay(targetDate.Date);
        dayNode.Should().NotBeNull();
        dayNode!.ItemsSource.Items.OfType<FileItem>().Should().ContainSingle(f => f.Name == "past_capture.png");
    }

    [Fact]
    public async Task RequestPriorityFolderOnDenseFolderPopulatesImmediately()
    {
        var denseDir = Path.Combine(_testDir, "DenseFolderTest");
        Directory.CreateDirectory(denseDir);
        for (int i = 0; i < 50; i++)
        {
            var p = Path.Combine(denseDir, $"dense_img_{i:D3}.png");
            File.WriteAllBytes(p, new byte[] { 1, 2, 3 });
        }

        var state = _stateService.Load();
        state.Layout.SidebarViewMode = "Tree";
        _stateService.Save(state);

        using var vm = CreateViewModel();
        await vm.LoadSidebarImagesAsync();

        var folder = vm.FindNodeByPath(denseDir) as FolderItem;
        folder.Should().NotBeNull();

        folder!.IsExpanded = true;
        _navigationService.RequestPriorityFolder(denseDir);

        folder.IsScanCompleted.Should().BeTrue();
        folder.IsLoading.Should().BeFalse();
        folder.ItemsSource.Items.OfType<FileItem>().Should().HaveCount(50);
    }
}


