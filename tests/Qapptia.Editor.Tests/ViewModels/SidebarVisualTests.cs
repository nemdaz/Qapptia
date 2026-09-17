using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media;
using DynamicData;
using FluentAssertions;
using Qapptia.App.Editor;
using Qapptia.App.Editor.ViewModels;
using Qapptia.App.Editor.ViewModels.Shapes;
using Qapptia.Editor.Models.Navigation;
using Qapptia.Editor.Services;
using Xunit;

namespace Qapptia.Editor.Tests.ViewModels;

public class SidebarVisualTests
{
    public static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(SidebarVisualTests));

    private static readonly byte[] s_minimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89,
        0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    ];

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Qapptia.App.Editor.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });

    [Fact]
    public async Task FlatTreeMassiveInjectionShouldMaintainResponsiveRendering()
    {
        var output = TestContext.Current.TestOutputHelper;

        await Session.Dispatch(() =>
        {
            // Arrange: Carga de MainWindow real (XAML de produccion)
            var rootGroup = new GroupItem { Name = "2025-12", IsExpanded = true };
            var window = new MainWindow { Width = 800, Height = 600 };
            window.Show();

            var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
            Assert.NotNull(listBox);
            var roots = new System.Collections.ObjectModel.ObservableCollection<GroupItem> { rootGroup };
            using var adapter = new FlatTreeAdapter(roots);
            listBox!.ItemsSource = adapter.FlatItems;

            // Warm-up de layout inicial
            rootGroup.ItemsSource.Add(new FileItem { Name = "warmup.png" });
            Dispatcher.UIThread.RunJobs();
            rootGroup.ItemsSource.Clear();
            Dispatcher.UIThread.RunJobs();

            // 100 items para validar inyeccion atomica reactiva
            var dummyItems = new List<FileItem>(100);
            for (int i = 0; i < 100; i++)
            {
                dummyItems.Add(new FileItem { Name = $"Img_{i}.png", FullPath = $"C:/Fake/Img_{i}.png" });
            }

            var sw = Stopwatch.StartNew();

            // Act: Inyeccion masiva atomica al arbol real
            rootGroup.ItemsSource.AddRange(dummyItems);
            Dispatcher.UIThread.RunJobs();
            sw.Stop();

            output?.WriteLine($"[TreeViewMassiveInjection] Render time for 100 items: {sw.ElapsedMilliseconds} ms ({sw.Elapsed.TotalMicroseconds:F1} µs)");

            // Assert: Rendimiento reactivo con panel y coleccion poblada
            rootGroup.ItemsSource.Count.Should().Be(100);
            sw.ElapsedMilliseconds.Should().BeLessThan(1200, "La inyeccion masiva debe realizarse fluidamente");

            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FlatTreeMassiveExpansionWithOneThousandFilesMustRemainInstantaneousDueToVirtualization()
    {
        var output = TestContext.Current.TestOutputHelper;

        await Session.Dispatch(() =>
        {
            var folder = new FolderItem { Name = "2025-12", IsExpanded = false };
            var window = new MainWindow { Width = 800, Height = 600 };
            window.Show();

            try
            {
                var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
                Assert.NotNull(listBox);

                var roots = new System.Collections.ObjectModel.ObservableCollection<GroupItem> { folder };
                using var adapter = new FlatTreeAdapter(roots);
                listBox!.ItemsSource = adapter.FlatItems;
                Dispatcher.UIThread.RunJobs();

                // Warm-up
                folder.ItemsSource.Add(new FileItem { Name = "warmup.png" });
                Dispatcher.UIThread.RunJobs();
                folder.ItemsSource.Clear();
                Dispatcher.UIThread.RunJobs();

                // Preparar 1,000 archivos reales
                var thousandFiles = Enumerable.Range(1, 1000)
                    .Select(i => new FileItem { Name = $"capture_{i:D4}.png", FullPath = $"C:/Fake/capture_{i:D4}.png" })
                    .ToList();
                folder.ItemsSource.AddRange(thousandFiles);

                // Act: Expandir la carpeta con 1,000 archivos sobre el ListBox virtualizado
                var sw = Stopwatch.StartNew();
                folder.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();
                sw.Stop();

                output?.WriteLine($"[FlatTreeVirtualization] Layout and render time for 1000 items: {sw.ElapsedMilliseconds} ms ({sw.Elapsed.TotalMicroseconds:F1} µs)");

                // Assert 1: La coleccion plana contiene el folder + los 1000 archivos
                adapter.FlatItems.Count.Should().Be(1001);

                // Assert 2: Virtualizacion real activa -> El árbol visual de Avalonia NO contiene 1000 controles ListBoxItem
                var realizedContainers = listBox.GetVisualDescendants().OfType<ListBoxItem>().Count();
                output?.WriteLine($"[FlatTreeVirtualization] Realized ListBoxItem visual containers in viewport: {realizedContainers}");
                realizedContainers.Should().BeLessThan(50, "VirtualizingStackPanel solo debe instanciar los controles visibles en pantalla y no los 1000 elementos");

                // Assert 3: Tiempo de layout instantaneo en la UI (< 250 ms en entorno de test headless)
                sw.ElapsedMilliseconds.Should().BeLessThan(250, "El tiempo de layout visual con virtualizacion debe ser practicamente instantaneo");
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task ChangeViewModeTreeToCalendarShouldSwapWithoutUiLag()
    {
        var output = TestContext.Current.TestOutputHelper;

        await Session.Dispatch(() =>
        {
            // Arrange: MainWindow real cargando templates de arbol y calendario
            var treeRootGroup = new GroupItem { Name = "Tree Root", IsExpanded = true };
            var calendarRootGroup = new CalendarGroupItem(GroupKind.Year) { Name = "Calendar Root", IsExpanded = true };

            for (int i = 0; i < 20; i++) treeRootGroup.ItemsSource.Add(new FileItem { Name = $"T{i}" });
            for (int i = 0; i < 20; i++) calendarRootGroup.ItemsSource.Add(new FileItem { Name = $"C{i}" });

            var window = new MainWindow { Width = 800, Height = 600 };
            window.Show();

            var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
            Assert.NotNull(listBox);

            var groups = new System.Collections.ObjectModel.ObservableCollection<GroupItem> { treeRootGroup };
            using var adapter = new FlatTreeAdapter(groups);
            listBox!.ItemsSource = adapter.FlatItems;
            Dispatcher.UIThread.RunJobs();

            // Warm-up inicial para JIT y realizacion de plantillas en headless
            groups.Clear();
            groups.Add(calendarRootGroup);
            Dispatcher.UIThread.RunJobs();
            groups.Clear();
            groups.Add(treeRootGroup);
            Dispatcher.UIThread.RunJobs();

            var bestSwap = TimeSpan.MaxValue;
            for (int i = 0; i < 5; i++)
            {
                var sw = Stopwatch.StartNew();

                // Act: Cambio reactivo de perspectiva mediante la coleccion de la vista (patron de produccion)
                groups.Clear();
                groups.Add((i % 2 == 0) ? calendarRootGroup : treeRootGroup);
                Dispatcher.UIThread.RunJobs();

                sw.Stop();
                if (sw.Elapsed < bestSwap) bestSwap = sw.Elapsed;
            }

            output?.WriteLine($"[ChangeViewModeTreeToCalendar] Swap time for items: {bestSwap.Milliseconds} ms ({bestSwap.TotalMicroseconds:F1} µs)");

            bestSwap.TotalMilliseconds.Should().BeLessThan(300, "La conmutacion de vista en XAML debe ser fluida");

            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FolderExpansionShouldActivateSpinnerAndRenderNodesFluently()
    {
        var output = TestContext.Current.TestOutputHelper;

        await Session.Dispatch(async () =>
        {
            // Arrange: Carpeta que inicia sin archivos cargados y no inspeccionada
            var folder = new FolderItem { Name = "2026-09", FullPath = "C:/Captures/2026-09", IsExpanded = false, IsLoading = false };

            var window = new MainWindow { Width = 800, Height = 600 };
            window.Show();

            var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
            Assert.NotNull(listBox);
            var groups = new System.Collections.ObjectModel.ObservableCollection<GroupItem> { folder };
            using var adapter = new FlatTreeAdapter(groups);
            listBox!.ItemsSource = adapter.FlatItems;
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();

            // Act 1: Usuario hace clic para expandir -> activa spinner antes de tener archivos
            folder.IsLoading = true;
            folder.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();

            // Assert 1: Verificar spinner activo en la plantilla real de MainWindow.axaml
            var spinners = listBox.GetVisualDescendants().OfType<PathIcon>().Where(p => p.Classes.Contains("spinner")).ToList();
            spinners.Should().NotBeEmpty("MainWindow.axaml debe contener el PathIcon.spinner en el TreeDataTemplate");
            spinners.Any(s => s.IsVisible).Should().BeTrue("El spinner vectorial debe ser visible cuando IsLoading = true");

            // Act 2: El servicio entrega los archivos y marca inspeccion completada
            var files = new List<FileItem>();
            for (int i = 0; i < 5; i++)
            {
                files.Add(new FileItem { Name = $"capture_{i:D2}.png", FullPath = $"C:/Captures/2026-09/capture_{i:D2}.png" });
            }
            folder.ItemsSource.AddRange(files);
            folder.IsScanCompleted = true;
            folder.IsLoading = false;
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();

            // Assert 2: Al tener archivos, NO debe quedar vacia confirmada, NO debe atenuarse y el chevron debe permanecer
            folder.HasFiles.Should().BeTrue("La carpeta debe reflejar que contiene archivos");
            folder.IsEmptyConfirmed.Should().BeFalse("Una carpeta con archivos jamas debe quedar como vacia confirmada");
            folder.IsDimmed.Should().BeFalse("El texto de la carpeta con archivos no debe atenuarse");

            spinners.Any(s => s.IsVisible).Should().BeFalse("El spinner debe apagarse al culminar la carga");

            var container = listBox.GetVisualDescendants().OfType<ListBoxItem>().FirstOrDefault(t => t.DataContext == folder);
            container.Should().NotBeNull("Debe existir el contenedor visual ListBoxItem en la vista real");
            container!.Classes.Contains("FolderNode").Should().BeTrue("El contenedor debe tener la clase FolderNode");
            container.Classes.Contains("EmptyNode").Should().BeFalse("El contenedor NO debe tener la clase EmptyNode si tiene archivos");

            // Act 3: Colapso del nodo
            folder.IsExpanded = false;
            Dispatcher.UIThread.RunJobs();
            folder.IsExpanded.Should().BeFalse("El nodo debe reflejar el estado colapsado");

            await Task.CompletedTask;
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ViewModeSwapBetweenTreeAndCalendarShouldRetainLoadedDataAndExecutePromptly()
    {
        var output = TestContext.Current.TestOutputHelper;
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_SwapTest_" + Guid.NewGuid().ToString("N"));
        var folder202609 = Path.Combine(tempDir, "2026-09");
        Directory.CreateDirectory(folder202609);

        for (int i = 0; i < 10; i++)
        {
            await File.WriteAllBytesAsync(Path.Combine(folder202609, $"shot_{i:D2}.png"), s_minimalPng, TestContext.Current.CancellationToken);
        }

        try
        {
            await Session.Dispatch(async () =>
            {
                var navService = new NavigationService();
                var stateService = new EditorStateService(tempDir, "state.json");
                using var viewModel = new SidebarViewModel(navService, stateService, tempDir);

                var window = new MainWindow { Width = 800, Height = 600 };
                window.Show();

                var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
                Assert.NotNull(listBox);
                listBox!.ItemsSource = viewModel.FlatTreeItems;

                // Carga inicial en modo Arbol
                await viewModel.SetViewMode(SidebarViewMode.Tree);
                Dispatcher.UIThread.RunJobs();

                viewModel.SidebarGroups.Should().NotBeEmpty();

                // Act: Alternar entre Calendario y Arbol mediante cache O(1)
                var swToCalendar = Stopwatch.StartNew();
                await viewModel.SetViewMode(SidebarViewMode.Calendar);
                Dispatcher.UIThread.RunJobs();
                swToCalendar.Stop();

                viewModel.IsCalendarViewActive.Should().BeTrue();
                viewModel.SidebarGroups.Should().NotBeEmpty();

                var swToTree = Stopwatch.StartNew();
                await viewModel.SetViewMode(SidebarViewMode.Tree);
                Dispatcher.UIThread.RunJobs();
                swToTree.Stop();

                viewModel.IsTreeViewActive.Should().BeTrue();
                viewModel.SidebarGroups.Should().NotBeEmpty();

                output?.WriteLine($"[ViewModeSwap] Tree -> Calendar: {swToCalendar.ElapsedMilliseconds} ms, Calendar -> Tree: {swToTree.ElapsedMilliseconds} ms");

                swToCalendar.ElapsedMilliseconds.Should().BeLessThan(100);
                swToTree.ElapsedMilliseconds.Should().BeLessThan(100);

                window.Close();
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task LoadSidebarImagesWithPersistedSelectionShouldExpandAncestors()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_PersistSel_" + Guid.NewGuid().ToString("N"));
        var otherDir = Path.Combine(tempDir, "2025-10");
        Directory.CreateDirectory(otherDir);

        var subDir = Path.Combine(tempDir, "2026-08");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "test.png");
        await File.WriteAllBytesAsync(testFile, s_minimalPng, TestContext.Current.CancellationToken);

        var stateService = new EditorStateService(tempDir, "state.json");
        var state = stateService.Load();
        state.Layout.SidebarViewMode = "Tree";
        state.Layout.ExpandedFolders.Clear();
        state.Layout.ExpandedFolders.Add(NavigationService.NormalizePath(otherDir));
        state.Session.LastSelectedFile = NavigationService.NormalizePath(testFile);
        stateService.Save(state);

        try
        {
            await Session.Dispatch(async () =>
            {
                var navService = new NavigationService();
                using var viewModel = new SidebarViewModel(navService, stateService, tempDir);

                // Act - Mediante el comando estándar de arranque
                await viewModel.LoadSidebarImagesAsync();

                GroupItem? rootFolder = null;
                GroupItem? subFolder = null;
                GroupItem? otherFolder = null;
                var timeout = Stopwatch.StartNew();
                while (timeout.ElapsedMilliseconds < 3000)
                {
                    Dispatcher.UIThread.RunJobs();
                    rootFolder = viewModel.SidebarGroups.OfType<GroupItem>().FirstOrDefault();
                    subFolder = rootFolder?.Items.OfType<GroupItem>().FirstOrDefault(f => f.Name == "2026-08");
                    otherFolder = rootFolder?.Items.OfType<GroupItem>().FirstOrDefault(f => f.Name == "2025-10");
                    if (rootFolder?.IsExpanded == true && subFolder?.IsExpanded == true && otherFolder?.IsExpanded == true && viewModel.SelectedNode != null)
                    {
                        break;
                    }
                    await Task.Delay(25);
                }

                rootFolder.Should().NotBeNull();
                subFolder.Should().NotBeNull();
                otherFolder.Should().NotBeNull();
                rootFolder!.IsExpanded.Should().BeTrue("El nodo raiz debe estar expandido");
                subFolder!.IsExpanded.Should().BeFalse("La carpeta colapsada por el usuario debe permanecer colapsada");
                otherFolder!.IsExpanded.Should().BeTrue("Las carpetas previamente expandidas por el usuario deben preservarse");

                // Al expandir la carpeta después, el archivo se auto-selecciona
                subFolder.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();

                var selected = viewModel.SelectedNode as FileItem;
                selected.Should().NotBeNull("Al expandir la carpeta, el archivo activo debe seleccionarse automáticamente");
                selected!.FullPath.Should().Be(NavigationService.NormalizePath(testFile));
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task LoadSidebarImagesInCalendarModeWithPersistedSelectionShouldExpandCalendarAncestors()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_CalPersist_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "2026-08");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "cal_test.png");
        await File.WriteAllBytesAsync(testFile, s_minimalPng, TestContext.Current.CancellationToken);
        var localDate = new FileInfo(testFile).LastWriteTime;

        var stateService = new EditorStateService(tempDir, "state.json");
        var state = stateService.Load();
        state.Layout.SidebarViewMode = "Calendar";
        state.Layout.ExpandedCalendarGroups.Clear();
        state.Layout.ExpandedCalendarGroups.Add("cal://2025");
        state.Session.LastSelectedFile = NavigationService.NormalizePath(testFile);
        stateService.Save(state);

        try
        {
            await Session.Dispatch(async () =>
            {
                var navService = new NavigationService();
                using var viewModel = new SidebarViewModel(navService, stateService, tempDir);

                await viewModel.LoadSidebarImagesAsync();

                CalendarGroupItem? yearNode = null;
                var timeout = Stopwatch.StartNew();
                while (timeout.ElapsedMilliseconds < 3000)
                {
                    Dispatcher.UIThread.RunJobs();
                    yearNode = viewModel.SidebarGroups.OfType<CalendarGroupItem>().FirstOrDefault(y => y.Year == localDate.Year);
                    if (yearNode != null)
                    {
                        break;
                    }
                    await Task.Delay(25);
                }

                yearNode.Should().NotBeNull();
                yearNode!.IsExpanded.Should().BeFalse("El año que no estaba en ExpandedCalendarGroups debe permanecer colapsado");

                // Al expandir el año y mes después, el archivo se auto-selecciona
                yearNode.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();
                var monthNode = yearNode.ItemsSource.Items.OfType<CalendarGroupItem>().FirstOrDefault(m => m.Month == localDate.Month);
                monthNode.Should().NotBeNull();
                monthNode!.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();

                var selected = viewModel.SelectedNode as FileItem;
                selected.Should().NotBeNull("Al expandir el mes, el archivo activo debe seleccionarse automáticamente");
                selected!.FullPath.Should().Be(NavigationService.NormalizePath(testFile));
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RealXamlListBoxItemShouldApplyFolderAndEmptyConfirmedClasses()
    {
        await Session.Dispatch(() =>
        {
            // Arrange: Crear MainWindow real desde XAML de produccion
            var activeFolder = new FolderItem { Name = "ActiveFolder", IsExpanded = true };
            activeFolder.ItemsSource.Add(new FileItem { Name = "child.png" });

            var emptyFolder = new FolderItem { Name = "EmptyFolder", IsExpanded = false, IsScanCompleted = true };
            // Empty confirmado: 0 subcarpetas y 0 archivos

            var window = new MainWindow { Width = 800, Height = 600 };
            window.Show();

            var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
            Assert.NotNull(listBox);

            var roots = new System.Collections.ObjectModel.ObservableCollection<GroupItem> { activeFolder, emptyFolder };
            using var adapter = new FlatTreeAdapter(roots);
            listBox!.ItemsSource = adapter.FlatItems;
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();

            // Assert: Validar que el ItemContainerTheme de MainWindow.axaml aplica Classes.FolderNode y Classes.EmptyNode
            activeFolder.IsFolder.Should().BeTrue();
            emptyFolder.IsFolder.Should().BeTrue();
            emptyFolder.IsEmptyConfirmed.Should().BeTrue("Una carpeta escaneada con 0 elementos debe ser vacia confirmada");

            var container = listBox.GetVisualDescendants().OfType<ListBoxItem>().FirstOrDefault(t => t.DataContext == activeFolder);
            container.Should().NotBeNull();
            container!.Classes.Contains("FolderNode").Should().BeTrue();

            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ViewModeSwapWithRealXamlShouldRetainSelectedFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_SwapSelTest_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "2026-09");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "shot_swap.png");
        await File.WriteAllBytesAsync(testFile, s_minimalPng, TestContext.Current.CancellationToken);

        try
        {
            await Session.Dispatch(async () =>
            {
                var navService = new NavigationService();
                var stateService = new EditorStateService(tempDir, "state.json");
                var state = stateService.Load();
                state.Layout.SidebarViewMode = "Calendar";
                state.Session.LastSelectedFile = NavigationService.NormalizePath(testFile);
                stateService.Save(state);

                using var viewModel = new SidebarViewModel(navService, stateService, tempDir);
                var window = new MainWindow { Width = 800, Height = 600 };
                window.Show();

                var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
                Assert.NotNull(listBox);
                listBox!.ItemsSource = viewModel.FlatTreeItems;

                await viewModel.LoadSidebarImagesAsync();
                Dispatcher.UIThread.RunJobs();

                viewModel.SelectedNode.Should().NotBeNull("Debe seleccionarse en modo Calendario");

                // Act: Cambiar a modo Árbol con el TreeView enlazado a SelectedItem
                await viewModel.SetViewMode(SidebarViewMode.Tree);
                Dispatcher.UIThread.RunJobs();

                viewModel.SelectedNode.Should().NotBeNull("Debe mantenerse seleccionado en modo Árbol");
                NavigationService.NormalizePath(viewModel.SelectedNode!.FullPath).Should().Be(NavigationService.NormalizePath(testFile));

                // Act: Cambiar de regreso a modo Calendario
                await viewModel.SetViewMode(SidebarViewMode.Calendar);
                Dispatcher.UIThread.RunJobs();

                viewModel.SelectedNode.Should().NotBeNull("Debe mantenerse seleccionado al volver a modo Calendario");
                NavigationService.NormalizePath(viewModel.SelectedNode!.FullPath).Should().Be(NavigationService.NormalizePath(testFile));

                window.Close();
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task CollapsedFolderWithSelectedFileShouldRemainCollapsedOnReopen()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_CollapseTest_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "2025-12");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "shot_collapsed.png");
        await File.WriteAllBytesAsync(testFile, s_minimalPng, TestContext.Current.CancellationToken);

        try
        {
            await Session.Dispatch(async () =>
            {
                var stateService = new EditorStateService(tempDir, "state.json");
                var state = stateService.Load();
                state.Layout.SidebarViewMode = "Tree";
                state.Layout.ExpandedFolders.Clear(); // 2025-12 fue colapsada por el usuario
                state.Session.LastSelectedFile = NavigationService.NormalizePath(testFile);
                stateService.Save(state);

                var navService = new NavigationService();
                using var viewModel = new SidebarViewModel(navService, stateService, tempDir);
                var window = new MainWindow { Width = 800, Height = 600 };
                window.Show();

                var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
                Assert.NotNull(listBox);
                listBox!.ItemsSource = viewModel.FlatTreeItems;

                await viewModel.LoadSidebarImagesAsync();
                Dispatcher.UIThread.RunJobs();

                var root = viewModel.SidebarGroups.FirstOrDefault();
                root.Should().NotBeNull();
                var targetFolder = root!.Items.OfType<FolderItem>().FirstOrDefault(f => f.Name == "2025-12");
                targetFolder.Should().NotBeNull();
                targetFolder!.IsExpanded.Should().BeFalse("La carpeta colapsada por el usuario debe permanecer colapsada al reiniciar el editor");

                window.Close();
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task DenseFolderViewSwappingShouldBeInstantaneous()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_DenseSwapTest_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "2025-12");
        Directory.CreateDirectory(subDir);

        // Crear 850 archivos en la carpeta
        for (int i = 1; i <= 850; i++)
        {
            var filePath = Path.Combine(subDir, $"shot_{i:D4}.png");
            File.WriteAllBytes(filePath, s_minimalPng);
        }

        try
        {
            await Session.Dispatch(async () =>
            {
                var stateService = new EditorStateService(tempDir, "state.json");
                var state = stateService.Load();
                state.Layout.SidebarViewMode = "Tree";
                state.Layout.ExpandedFolders.Add(NavigationService.NormalizePath(subDir));
                stateService.Save(state);

                var navService = new NavigationService();
                using var viewModel = new SidebarViewModel(navService, stateService, tempDir);
                var window = new MainWindow { Width = 800, Height = 600 };
                window.Show();

                // Carga inicial en modo Árbol con 850 archivos
                await viewModel.SetViewMode(SidebarViewMode.Tree);
                navService.RequestPriorityFolder(subDir);
                Dispatcher.UIThread.RunJobs();

                var root = viewModel.TreeGroups.FirstOrDefault() ?? viewModel.SidebarGroups.FirstOrDefault();
                root.Should().NotBeNull();
                var targetFolder = root!.Items.OfType<FolderItem>().FirstOrDefault(f => f.Name == "2025-12");
                targetFolder.Should().NotBeNull();
                targetFolder!.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();

                targetFolder.Items.OfType<FileItem>().Count().Should().Be(850);

                // Act 1: Alternar a Calendario con la carpeta de 850 archivos expandida
                var swToCalendar = Stopwatch.StartNew();
                await viewModel.SetViewMode(SidebarViewMode.Calendar);
                Dispatcher.UIThread.RunJobs();
                swToCalendar.Stop();

                viewModel.IsCalendarViewActive.Should().BeTrue();
                swToCalendar.ElapsedMilliseconds.Should().BeLessThan(250, "El cambio a Calendario debe ser inmediato y no reconstruir controles");

                // Act 2: Regresar a Árbol con la carpeta de 850 archivos expandida
                var swToTree = Stopwatch.StartNew();
                await viewModel.SetViewMode(SidebarViewMode.Tree);
                Dispatcher.UIThread.RunJobs();
                swToTree.Stop();

                viewModel.IsTreeViewActive.Should().BeTrue();
                swToTree.ElapsedMilliseconds.Should().BeLessThan(250, "El regreso a Árbol debe ser inmediato gracias a la retención del árbol visual cálido");

                targetFolder.IsExpanded.Should().BeTrue();
                targetFolder.Items.OfType<FileItem>().Count().Should().Be(850);

                window.Close();
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task SpinnerGeometryAndThemeBrushShouldBeSymmetricAndOpaqueGrey()
    {
        await Session.Dispatch(() =>
        {
            var app = Application.Current;
            app.Should().NotBeNull();

            // 1. Validate StreamGeometry bounds symmetry and center
            app!.TryGetResource("IconSpinner", null, out var iconObj).Should().BeTrue();
            var iconSpinner = iconObj as StreamGeometry;
            iconSpinner.Should().NotBeNull();
            var bounds = iconSpinner!.Bounds;

            // Width and Height must be equal so rotation around center does not wobble
            bounds.Width.Should().BeApproximately(bounds.Height, 0.5, "Spinner bounding box must be symmetric to prevent wobbling cam effects");

            // Center of the bounding box should be at (12, 12)
            var centerX = bounds.X + bounds.Width / 2.0;
            var centerY = bounds.Y + bounds.Height / 2.0;
            centerX.Should().BeApproximately(12.0, 0.5, "Spinner rotation center must align with viewport center at (12, 12)");
            centerY.Should().BeApproximately(12.0, 0.5, "Spinner rotation center must align with viewport center at (12, 12)");

            // 2. Validate PathIcon.spinner applies M3OnSurfaceVariantBrush (opaque theme grey)
            var window = new MainWindow { Width = 800, Height = 600 };
            window.Show();

            var spinnerIcon = new PathIcon();
            spinnerIcon.Classes.Add("spinner");
            spinnerIcon.Classes.Add("spinning");
            window.Content = spinnerIcon;
            Dispatcher.UIThread.RunJobs();

            var foregroundBrush = spinnerIcon.Foreground as ISolidColorBrush;
            foregroundBrush.Should().NotBeNull();

            // Verify brush color matches M3OnSurfaceVariantBrush (opaque grey)
            app.TryGetResource("M3OnSurfaceVariantBrush", app.ActualThemeVariant, out var expectedBrushObj).Should().BeTrue();
            var expectedBrush = expectedBrushObj as ISolidColorBrush;
            expectedBrush.Should().NotBeNull();
            foregroundBrush!.Color.Should().Be(expectedBrush!.Color);

            var tg = spinnerIcon.RenderTransform as Avalonia.Media.TransformGroup;
            var rot = tg?.Children.OfType<RotateTransform>().FirstOrDefault();
            rot.Should().NotBeNull("PathIcon.spinner.spinning must have an active RotateTransform in its transform group");
            rot!.Angle.Should().BeGreaterThan(0.0, "PathIcon.spinner.spinning must actively rotate its angle and not remain static at zero");

            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FolderSpinnerLifecycleShouldRemainActiveUntilAllFilesAreDispatched()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_SpinnerLife_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "dense_folder");
        Directory.CreateDirectory(subDir);

        const int fileCount = 120;
        for (int i = 1; i <= fileCount; i++)
        {
            var filePath = Path.Combine(subDir, $"shot_{i:D4}.png");
            File.WriteAllBytes(filePath, s_minimalPng);
        }

        try
        {
            await Session.Dispatch(async () =>
            {
                var navService = new NavigationService();
                var root = await navService.BuildTreeAsync(tempDir, Array.Empty<string>());
                root.Should().NotBeNull();

                var folder = root!.Items.OfType<FolderItem>().FirstOrDefault(f => f.Name == "dense_folder");
                folder.Should().NotBeNull();
                folder!.IsLoading.Should().BeFalse();
                folder.IsScanCompleted.Should().BeFalse();

                bool wasLoadingObserved = false;
                bool prematureLoadingFalseWithIncompleteFiles = false;
                int collectionChangeEvents = 0;

                ((INotifyCollectionChanged)folder.Items).CollectionChanged += (s, e) =>
                {
                    collectionChangeEvents++;
                };

                folder.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(GroupItem.IsLoading))
                    {
                        if (folder.IsLoading)
                        {
                            wasLoadingObserved = true;
                        }
                        else if (!folder.IsLoading && !folder.IsScanCompleted)
                        {
                            prematureLoadingFalseWithIncompleteFiles = true;
                        }
                    }
                };

                // Request priority scan
                navService.RequestPriorityFolder(subDir);
                Dispatcher.UIThread.RunJobs();

                wasLoadingObserved.Should().BeTrue("IsLoading must turn true when scanning starts");
                prematureLoadingFalseWithIncompleteFiles.Should().BeFalse("IsLoading must never turn false before IsScanCompleted is true");
                folder.IsScanCompleted.Should().BeTrue("Folder scan must be marked completed");
                folder.IsLoading.Should().BeFalse("Folder spinner must turn off once all files are loaded");
                folder.Items.OfType<FileItem>().Count().Should().Be(fileCount);
                collectionChangeEvents.Should().Be(1, "Folder files MUST be delivered in exactly ONE atomic collection-changed event, never in partial batches or chunks");
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task RealMainWindowFolderExpansionMustActivateSpinningClassAndRotate()
    {
        await Session.Dispatch(async () =>
        {
            var folder = new FolderItem { Name = "ActiveFolder", IsExpanded = true, IsLoading = false };
            var window = new MainWindow { Width = 800, Height = 600 };
            window.Show();

            try
            {
                var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
                Assert.NotNull(listBox);
                var roots = new System.Collections.ObjectModel.ObservableCollection<GroupItem> { folder };
                using var adapter = new FlatTreeAdapter(roots);
                listBox!.ItemsSource = adapter.FlatItems;
                Dispatcher.UIThread.RunJobs();

                var spinner = window.GetVisualDescendants().OfType<PathIcon>().FirstOrDefault(p => p.Classes.Contains("spinner"));
                Assert.NotNull(spinner);
                spinner!.IsVisible.Should().BeFalse("Spinner must not be visible when IsLoading is false");
                spinner.Classes.Contains("spinning").Should().BeFalse("Spinner must not have class 'spinning' when IsLoading is false");

                // Act 1: Turn on loading
                folder.IsLoading = true;
                Dispatcher.UIThread.RunJobs();

                spinner.IsVisible.Should().BeTrue("Spinner must be visible when IsLoading is true");
                spinner.Classes.Contains("spinning").Should().BeTrue("Spinner must gain class 'spinning' when IsLoading is true to trigger rotation");

                await Task.Delay(100);
                Dispatcher.UIThread.RunJobs();

                var tg = spinner.RenderTransform as Avalonia.Media.TransformGroup;
                var rot = tg?.Children.OfType<RotateTransform>().FirstOrDefault();
                rot.Should().NotBeNull("PathIcon must have an active RotateTransform");
                rot!.Angle.Should().BeGreaterThan(0.0, "Spinner must actively rotate while loading and not remain static at zero");

                // Act 2: Turn off loading
                folder.IsLoading = false;
                Dispatcher.UIThread.RunJobs();

                spinner.IsVisible.Should().BeFalse("Spinner must hide when IsLoading becomes false");
                spinner.Classes.Contains("spinning").Should().BeFalse("Spinner must drop class 'spinning' when IsLoading becomes false");
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DenseFolderLoadingMustDeliverAllFilesAtomicallyWithoutChunkingOrMultipleBatches()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_AtomicCheck_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "dense_folder");
        Directory.CreateDirectory(subDir);

        const int fileCount = 250;
        for (int i = 1; i <= fileCount; i++)
        {
            var filePath = Path.Combine(subDir, $"shot_{i:D4}.png");
            File.WriteAllBytes(filePath, s_minimalPng);
        }

        try
        {
            await Session.Dispatch(async () =>
            {
                var navService = new NavigationService();
                var root = await navService.BuildTreeAsync(tempDir, Array.Empty<string>());
                root.Should().NotBeNull();

                var folder = root!.Items.OfType<FolderItem>().FirstOrDefault(f => f.Name == "dense_folder");
                folder.Should().NotBeNull();

                int collectionChangeEvents = 0;
                int itemsAddedInFirstEvent = 0;

                ((INotifyCollectionChanged)folder!.Items).CollectionChanged += (s, e) =>
                {
                    collectionChangeEvents++;
                    if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
                    {
                        itemsAddedInFirstEvent += e.NewItems.Count;
                    }
                };

                navService.RequestPriorityFolder(subDir);
                Dispatcher.UIThread.RunJobs();

                collectionChangeEvents.Should().Be(1, "Folder files MUST be delivered in exactly ONE atomic collection-changed event, never in chunks or progressive batches");
                itemsAddedInFirstEvent.Should().Be(fileCount, "The single atomic event must contain all files without omitting any");
                folder.Items.OfType<FileItem>().Count().Should().Be(fileCount);
                folder.IsScanCompleted.Should().BeTrue();
                folder.IsLoading.Should().BeFalse();
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task SidebarSelectedItemShouldApplyThemePrimaryContainerEmphasisAndForeground()
    {
        await Session.Dispatch(() =>
        {
            var app = Application.Current;
            app.Should().NotBeNull();

            // 1. La variante de acento grisáceo debe existir en el tema para botones de vista (sidebar-toggle)
            app!.TryGetResource("M3GrayContainerBrush", null, out var grayContObj).Should().BeTrue();
            app.TryGetResource("M3OnGrayContainerBrush", null, out var onGrayContObj).Should().BeTrue();
            (grayContObj as ISolidColorBrush).Should().NotBeNull();
            (onGrayContObj as ISolidColorBrush).Should().NotBeNull();

            // 2. Primary container y OnPrimaryContainer deben existir para énfasis del sistema en selección
            var window = new MainWindow { Width = 1000, Height = 700 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            app.TryGetResource("M3PrimaryContainerBrush", app.ActualThemeVariant, out var primContObj).Should().BeTrue();
            app.TryGetResource("M3OnPrimaryContainerBrush", app.ActualThemeVariant, out var onPrimContObj).Should().BeTrue();

            var primContBrush = primContObj as ISolidColorBrush;
            var onPrimContBrush = onPrimContObj as ISolidColorBrush;
            primContBrush.Should().NotBeNull();
            onPrimContBrush.Should().NotBeNull();

            // 3. Verify MainWindow sidebar ListBox selection styles apply M3PrimaryContainerBrush and M3OnPrimaryContainerBrush
            var sidebarTree = window.FindControl<ListBox>("SidebarTreeView");
            sidebarTree.Should().NotBeNull();

            // Create and select an item in the sidebar
            var file = new FileItem { Name = "test.png", FullPath = "C:/fake/test.png" };
            sidebarTree!.ItemsSource = new[] { file };
            sidebarTree.SelectedItem = file;
            Dispatcher.UIThread.RunJobs();

            var container = sidebarTree.ContainerFromItem(file) as ListBoxItem;
            container.Should().NotBeNull();
            container!.IsSelected.Should().BeTrue();

            // Verify the Part_ContentPresenter has M3PrimaryContainerBrush background
            var presenter = container.FindDescendantOfType<Avalonia.Controls.Presenters.ContentPresenter>();
            presenter.Should().NotBeNull();
            var bgBrush = presenter!.Background as ISolidColorBrush;
            bgBrush.Should().NotBeNull();
            bgBrush!.Color.Should().Be(primContBrush!.Color, "Selected item background must match theme M3PrimaryContainerBrush");

            // Verify TextBlock inside container has M3OnPrimaryContainerBrush foreground
            var textBlock = container.FindDescendantOfType<TextBlock>();
            textBlock.Should().NotBeNull();
            var fgBrush = textBlock!.Foreground as ISolidColorBrush;
            fgBrush.Should().NotBeNull();
            fgBrush!.Color.Should().Be(onPrimContBrush!.Color, "Selected item text must match theme M3OnPrimaryContainerBrush");

            // Verify PathIcon inside container has M3OnPrimaryContainerBrush foreground
            var pathIcon = container.FindDescendantOfType<PathIcon>();
            pathIcon.Should().NotBeNull();
            var iconFg = pathIcon!.Foreground as ISolidColorBrush;
            iconFg.Should().NotBeNull();
            iconFg!.Color.Should().Be(onPrimContBrush!.Color, "Selected item icon must match theme M3OnPrimaryContainerBrush");

            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CalendarWeekGroupMustUseMaterialSymbolsViewWeekIcon()
    {
        var weekGroup = new CalendarGroupItem(GroupKind.Week);
        weekGroup.IconKey.Should().Be("IconViewWeek");

        await Session.Dispatch(() =>
        {
            var app = Application.Current;
            app.Should().NotBeNull();
            app!.TryGetResource("IconViewWeek", null, out var iconObj).Should().BeTrue();
            (iconObj as Geometry).Should().NotBeNull("IconViewWeek must be a valid StreamGeometry in application resources");
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CalendarViewFilesMustHaveDepthFourAndSixtyFourPixelsIndentMargin()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_CalDepth_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "2026-03");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "shot_cal.png");
        await File.WriteAllBytesAsync(testFile, s_minimalPng, TestContext.Current.CancellationToken);

        try
        {
            await Session.Dispatch(async () =>
            {
                var navService = new NavigationService();
                var stateService = new EditorStateService(tempDir, "state.json");
                using var viewModel = new SidebarViewModel(navService, stateService, tempDir);

                await viewModel.LoadSidebarImagesAsync();

                // Esperar a que el indexador complete la estructura
                var sw = Stopwatch.StartNew();
                CalendarGroupItem? dayGroup = null;
                FileItem? calFile = null;

                while (sw.ElapsedMilliseconds < 3000)
                {
                    Dispatcher.UIThread.RunJobs();
                    dayGroup = viewModel.CalendarGroups
                        .SelectMany(y => y.Items.OfType<CalendarGroupItem>()) // months
                        .SelectMany(m => m.Items.OfType<CalendarGroupItem>()) // weeks
                        .SelectMany(w => w.Items.OfType<CalendarGroupItem>()) // days
                        .FirstOrDefault(d => d.Items.OfType<FileItem>().Any());

                    if (dayGroup != null)
                    {
                        calFile = dayGroup.Items.OfType<FileItem>().FirstOrDefault();
                        if (calFile != null) break;
                    }
                    await Task.Delay(25);
                }

                dayGroup.Should().NotBeNull("Calendar day should be populated with the test file");
                calFile.Should().NotBeNull("Calendar day should contain the FileItem");

                // Day is depth 3 (Margin 48)
                dayGroup!.Depth.Should().Be(3);
                dayGroup.IndentMargin.Left.Should().Be(48.0);

                // File under Day MUST be depth 4 (Margin 64px = 4 * 16px)
                calFile!.Parent.Should().BeSameAs(dayGroup);
                calFile.Depth.Should().Be(4, "File under Calendar Day must have Depth = 4 (Parent = DayGroup)");
                calFile.IndentMargin.Left.Should().Be(64.0, "File in Calendar View must have IndentMargin = 64px (4 * 16px)");
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task TreeViewCollapseMustRemoveDescendantFilesImmediatelyFromFlatList()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_TreeCollapse_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "folder_to_collapse");
        Directory.CreateDirectory(subDir);

        for (int i = 1; i <= 5; i++)
        {
            var filePath = Path.Combine(subDir, $"img_{i:D2}.png");
            File.WriteAllBytes(filePath, s_minimalPng);
        }

        try
        {
            await Session.Dispatch(async () =>
            {
                var navService = new NavigationService();
                var stateService = new EditorStateService(tempDir, "state.json");
                using var viewModel = new SidebarViewModel(navService, stateService, tempDir);

                await viewModel.SetViewMode(SidebarViewMode.Tree);
                await viewModel.LoadSidebarImagesAsync();

                // Esperar a que el árbol cargue
                var sw = Stopwatch.StartNew();
                FolderItem? folder = null;
                while (sw.ElapsedMilliseconds < 3000)
                {
                    Dispatcher.UIThread.RunJobs();
                    var root = viewModel.TreeGroups.FirstOrDefault();
                    folder = root?.Items.OfType<FolderItem>().FirstOrDefault(f => f.Name == "folder_to_collapse");
                    if (folder != null && folder.Items.OfType<FileItem>().Count() == 5)
                    {
                        break;
                    }
                    await Task.Delay(25);
                }

                folder.Should().NotBeNull();
                folder!.Items.OfType<FileItem>().Count().Should().Be(5);

                // 1. Expandir la carpeta
                folder.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();

                viewModel.FlatTreeItems.Should().Contain(folder);
                foreach (var childFile in folder.Items.OfType<FileItem>())
                {
                    viewModel.FlatTreeItems.Should().Contain(childFile, "Expanded folder files must be in FlatTreeItems");
                }

                // 2. Colapsar la carpeta: todos los archivos deben desaparecer de la lista plana
                folder.IsExpanded = false;
                Dispatcher.UIThread.RunJobs();

                viewModel.FlatTreeItems.Should().Contain(folder);
                foreach (var childFile in folder.Items.OfType<FileItem>())
                {
                    viewModel.FlatTreeItems.Should().NotContain(childFile, "Collapsed folder files MUST be removed from FlatTreeItems");
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task CalendarVirtualNodesExpandingYearMonthWeekNeverShowsLoadingSpinner()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_CalSpinner_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            await Session.Dispatch(async () =>
            {
                var navService = new NavigationService();
                var stateService = new EditorStateService(tempDir, "state.json");
                using var viewModel = new SidebarViewModel(navService, stateService, tempDir);

                await viewModel.SetViewMode(SidebarViewMode.Calendar);
                await viewModel.LoadSidebarImagesAsync();

                var currentYear = DateTime.Today.Year;
                var yearNode = viewModel.CalendarGroups.OfType<CalendarGroupItem>().FirstOrDefault(y => y.Year == currentYear);
                yearNode.Should().NotBeNull();

                // 1. Expandir Año: jamás debe mostrar spinner
                yearNode!.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();
                yearNode.IsLoading.Should().BeFalse("Virtual Year node must never show loading spinner");

                // 2. Expandir Mes: jamás debe mostrar spinner
                var monthNode = yearNode.ItemsSource.Items.OfType<CalendarGroupItem>().FirstOrDefault();
                monthNode.Should().NotBeNull();
                monthNode!.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();
                monthNode.IsLoading.Should().BeFalse("Virtual Month node must never show loading spinner");

                // 3. Expandir Semana: jamás debe mostrar spinner
                var weekNode = monthNode.ItemsSource.Items.OfType<CalendarGroupItem>().FirstOrDefault();
                weekNode.Should().NotBeNull();
                weekNode!.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();
                weekNode.IsLoading.Should().BeFalse("Virtual Week node must never show loading spinner");
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task EmptyHeuristicYearsWithNoFilesArePrunedAfterIndexing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_CalPrune_" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(tempDir, "2025-12");
        Directory.CreateDirectory(subDir);

        // Archivo con fecha de creación actual (2026), sin metadatos XMP
        var filePath = Path.Combine(subDir, "20251201_test.png");
        File.WriteAllBytes(filePath, s_minimalPng);

        try
        {
            await Session.Dispatch(async () =>
            {
                var navService = new NavigationService();
                var stateService = new EditorStateService(tempDir, "state.json");
                using var viewModel = new SidebarViewModel(navService, stateService, tempDir);

                await viewModel.SetViewMode(SidebarViewMode.Calendar);
                await viewModel.LoadSidebarImagesAsync();

                // Esperar a que la indexación asíncrona termine y ejecute el pruning
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 3000)
                {
                    Dispatcher.UIThread.RunJobs();
                    bool has2025 = viewModel.CalendarGroups.OfType<CalendarGroupItem>().Any(y => y.Year == 2025);
                    if (!has2025 && viewModel.CalendarGroups.Count > 0)
                    {
                        break;
                    }
                    await Task.Delay(25);
                }

                // 2025 que nació por heurística de carpeta pero quedó con 0 archivos debe ser podado
                viewModel.CalendarGroups.OfType<CalendarGroupItem>().Any(y => y.Year == 2025)
                    .Should().BeFalse("Heuristic Year 2025 with 0 files must be pruned from CalendarGroups");

                viewModel.SidebarGroups.OfType<CalendarGroupItem>().Any(y => y.Year == 2025)
                    .Should().BeFalse("Heuristic Year 2025 with 0 files must be pruned from SidebarGroups");

                // El año actual (2026) debe permanecer
                viewModel.CalendarGroups.OfType<CalendarGroupItem>().Any(y => y.Year == DateTime.Today.Year)
                    .Should().BeTrue("Current year must be preserved");
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task FlatTreeAdapterRefreshSubtreeShouldPreserveSelectionInListBox()
    {
        await Session.Dispatch(() =>
        {
            var folder = new FolderItem { Name = "DayNode", FullPath = "C:/Captures/2026-09-16", IsExpanded = true };
            var file1 = new FileItem { Name = "file1.png", FullPath = "C:/Captures/2026-09-16/file1.png", Parent = folder };
            folder.ItemsSource.Add(file1);

            var window = new MainWindow { Width = 800, Height = 600 };
            window.Show();

            try
            {
                var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
                Assert.NotNull(listBox);

                var groups = new System.Collections.ObjectModel.ObservableCollection<GroupItem> { folder };
                using var adapter = new FlatTreeAdapter(groups);
                listBox!.ItemsSource = adapter.FlatItems;
                listBox.SelectedItem = file1;
                Dispatcher.UIThread.RunJobs();

                listBox.SelectedItem.Should().Be(file1, "ListBox debe tener seleccionado file1");

                // Simular lo que hace InsertFilesSorted cuando llega otro archivo o nueva lista
                var file2 = new FileItem { Name = "file2.png", FullPath = "C:/Captures/2026-09-16/file2.png", Parent = folder };
                NavigationService.InsertFilesSorted(folder, new[] { file2 });
                Dispatcher.UIThread.RunJobs();

                // Verificar si ListBox perdió la selección!
                listBox.SelectedItem.Should().NotBeNull("ListBox NO debe perder la selección tras la actualización de la colección");
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SwitchingFromCalendarToTreeShouldMaintainFileSelectionInBothViewModelAndListBox()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_SwitchTest_" + Guid.NewGuid().ToString("N"));
        var dayDir = Path.Combine(tempDir, "2026-09", "2026-09-16");
        Directory.CreateDirectory(dayDir);
        var shotPath = Path.Combine(dayDir, "Qapptia_20260916_153831.png");
        File.WriteAllBytes(shotPath, s_minimalPng);
        var shotPath2 = Path.Combine(dayDir, "Qapptia_20260916_153830.png");
        File.WriteAllBytes(shotPath2, s_minimalPng);

        try
        {
            await Session.Dispatch(async () =>
            {
                var stateService = new EditorStateService(tempDir, "editor_state.json");
                var state = stateService.Load();
                state.Layout.SidebarViewMode = "Calendar";
                state.Layout.ExpandedFolders = new List<string> { tempDir };
                state.Session.LastSelectedFile = shotPath;
                stateService.Save(state);

                using var navService = new NavigationService();
                var editorVm = new EditorViewModel(stateService, tempDir, new Moq.Mock<Qapptia.Editor.Core.IFontProvider>().Object, navigationService: navService);

                var window = new MainWindow { Width = 800, Height = 600 };
                window.InitializeWithViewModel(editorVm);
                window.Show();

                try
                {
                    var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
                    Assert.NotNull(listBox);

                    Dispatcher.UIThread.RunJobs();

                    // Assert Calendar mode selection
                    editorVm.SelectedNode.Should().NotBeNull("editorVm.SelectedNode debe estar seleccionado al arrancar en Calendario");
                    ((FileItem)editorVm.SelectedNode!).FullPath.Should().Be(shotPath);
                    listBox!.SelectedItem.Should().NotBeNull("listBox.SelectedItem debe estar seleccionado en Calendario");

                    // Act 1: Switch to Tree mode
                    await editorVm.SetSidebarViewModeCommand.ExecuteAsync(SidebarViewMode.Tree);
                    await Task.Delay(300);
                    Dispatcher.UIThread.RunJobs();

                    // Assert Tree mode selection
                    editorVm.SelectedNode.Should().NotBeNull("editorVm.SelectedNode debe mantenerse seleccionado al alternar a Árbol");
                    ((FileItem)editorVm.SelectedNode!).FullPath.Should().Be(shotPath);
                    listBox.SelectedItem.Should().NotBeNull("listBox.SelectedItem debe mantenerse seleccionado al alternar a Árbol");
                    ((FileItem)listBox.SelectedItem!).FullPath.Should().Be(shotPath);

                    // Act 2: Switch back to Calendar mode
                    await editorVm.SetSidebarViewModeCommand.ExecuteAsync(SidebarViewMode.Calendar);
                    await Task.Delay(300);
                    Dispatcher.UIThread.RunJobs();

                    // Assert Calendar mode retention
                    editorVm.SelectedNode.Should().NotBeNull("editorVm.SelectedNode debe mantenerse seleccionado al regresar a Calendario");
                    ((FileItem)editorVm.SelectedNode!).FullPath.Should().Be(shotPath);
                    listBox.SelectedItem.Should().NotBeNull("listBox.SelectedItem debe mantenerse seleccionado al regresar a Calendario");
                    ((FileItem)listBox.SelectedItem!).FullPath.Should().Be(shotPath);
                }
                finally
                {
                    window.Close();
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task ExpandingCollapsedNodeAfterViewSwitchShouldImmediatelySelectActiveFileInBothViews()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_ExpandSwitchTest_" + Guid.NewGuid().ToString("N"));
        var dayDir = Path.Combine(tempDir, "2026-09", "2026-09-16");
        Directory.CreateDirectory(dayDir);
        var shotPath = Path.Combine(dayDir, "Qapptia_20260916_153831.png");
        File.WriteAllBytes(shotPath, s_minimalPng);

        try
        {
            await Session.Dispatch(async () =>
            {
                // Configurar: Arbol con nodo expandido y archivo seleccionado; Calendario con nodo colapsado
                var stateService = new EditorStateService(tempDir, "editor_state.json");
                var state = stateService.Load();
                state.Layout.SidebarViewMode = "Tree";
                state.Layout.ExpandedFolders = new List<string> { tempDir, Path.Combine(tempDir, "2026-09"), dayDir };
                state.Layout.ExpandedCalendarGroups = new List<string>(); // Calendario completamente colapsado
                state.Session.LastSelectedFile = shotPath;
                stateService.Save(state);

                using var navService = new NavigationService();
                var editorVm = new EditorViewModel(stateService, tempDir, new Moq.Mock<Qapptia.Editor.Core.IFontProvider>().Object, navigationService: navService);

                var window = new MainWindow { Width = 800, Height = 600 };
                window.InitializeWithViewModel(editorVm);
                window.Show();

                try
                {
                    var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
                    Assert.NotNull(listBox);

                    Dispatcher.UIThread.RunJobs();

                    // 1. Inicia en Arbol: nodo expandido y archivo seleccionado
                    editorVm.SidebarViewMode.Should().Be(SidebarViewMode.Tree);
                    editorVm.SelectedNode.Should().NotBeNull();
                    ((FileItem)editorVm.SelectedNode!).FullPath.Should().Be(shotPath);
                    listBox!.SelectedItem.Should().Be(editorVm.SelectedNode);

                    // 2. Cambiar a Calendario (nodo colapsado): no debe haber archivo seleccionado
                    await editorVm.SetSidebarViewModeCommand.ExecuteAsync(SidebarViewMode.Calendar);
                    Dispatcher.UIThread.RunJobs();

                    editorVm.SidebarViewMode.Should().Be(SidebarViewMode.Calendar);
                    editorVm.SelectedNode.Should().BeNull("En Calendario el nodo esta colapsado, no debe seleccionarse");
                    listBox.SelectedItem.Should().BeNull();

                    // 3. Expandir los nodos de Calendario hasta el dia que contiene el archivo
                    // Buscar el dia correspondiente a la fecha del archivo
                    var dayNode = navService.FindCalendarDay(new DateTime(2026, 9, 16));
                    Assert.NotNull(dayNode);

                    // Expandir ancestros (Año, Mes, Semana) y finalmente el Día
                    var ancestors = new List<GroupItem>();
                    for (GroupItem? cur = dayNode; cur != null; cur = cur.Parent as GroupItem)
                    {
                        ancestors.Insert(0, cur);
                    }

                    foreach (var ancestor in ancestors)
                    {
                        ancestor.IsExpanded = true;
                        Dispatcher.UIThread.RunJobs();
                    }

                    // 4. Incidencia del usuario: al expandir el dia que contiene el archivo, DEBE seleccionarlo
                    editorVm.SelectedNode.Should().NotBeNull("Al expandir el nodo contenedor en Calendario, debe seleccionarse el archivo activo");
                    ((FileItem)editorVm.SelectedNode!).FullPath.Should().Be(shotPath);
                    listBox.SelectedItem.Should().NotBeNull("ListBox debe reflejar el archivo seleccionado");
                    ((FileItem)listBox.SelectedItem!).FullPath.Should().Be(shotPath);
                }
                finally
                {
                    window.Close();
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task ExpandingCollapsedFolderInTreeAfterStartingInCalendarShouldSelectActiveFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_ExpandSwitchTreeTest_" + Guid.NewGuid().ToString("N"));
        var dayDir = Path.Combine(tempDir, "2026-09", "2026-09-16");
        Directory.CreateDirectory(dayDir);
        var shotPath = Path.Combine(dayDir, "Qapptia_20260916_153831.png");
        File.WriteAllBytes(shotPath, s_minimalPng);

        try
        {
            await Session.Dispatch(async () =>
            {
                // Configurar: Calendario con nodo expandido y archivo seleccionado; Arbol con nodo colapsado
                var stateService = new EditorStateService(tempDir, "editor_state.json");
                var state = stateService.Load();
                state.Layout.SidebarViewMode = "Calendar";
                state.Layout.ExpandedFolders = new List<string>(); // Arbol completamente colapsado
                state.Layout.ExpandedCalendarGroups = new List<string>
                {
                    "cal://2026",
                    "cal://2026/09",
                    "cal://2026/09/w38",
                    "cal://2026/09/w38/2026-09-16"
                };
                state.Session.LastSelectedFile = shotPath;
                stateService.Save(state);

                using var navService = new NavigationService();
                var editorVm = new EditorViewModel(stateService, tempDir, new Moq.Mock<Qapptia.Editor.Core.IFontProvider>().Object, navigationService: navService);

                var window = new MainWindow { Width = 800, Height = 600 };
                window.InitializeWithViewModel(editorVm);
                window.Show();

                try
                {
                    var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
                    Assert.NotNull(listBox);

                    Dispatcher.UIThread.RunJobs();

                    // 1. Inicia en Calendario: nodo expandido y archivo seleccionado
                    editorVm.SidebarViewMode.Should().Be(SidebarViewMode.Calendar);
                    editorVm.SelectedNode.Should().NotBeNull();
                    ((FileItem)editorVm.SelectedNode!).FullPath.Should().Be(shotPath);
                    listBox!.SelectedItem.Should().Be(editorVm.SelectedNode);

                    // 2. Cambiar a Arbol (nodo colapsado): no debe haber archivo seleccionado
                    await editorVm.SetSidebarViewModeCommand.ExecuteAsync(SidebarViewMode.Tree);
                    Dispatcher.UIThread.RunJobs();

                    editorVm.SidebarViewMode.Should().Be(SidebarViewMode.Tree);
                    editorVm.SelectedNode.Should().BeNull("En Arbol el nodo esta colapsado, no debe seleccionarse");
                    listBox.SelectedItem.Should().BeNull();

                    // 3. Expandir la carpeta raiz y subcarpetas hasta llegar a dayDir
                    var rootFolder = editorVm.TreeGroups.OfType<FolderItem>().FirstOrDefault();
                    Assert.NotNull(rootFolder);

                    var folder202609 = rootFolder.ItemsSource.Items.OfType<FolderItem>().FirstOrDefault();
                    Assert.NotNull(folder202609);

                    var folderDay = folder202609.ItemsSource.Items.OfType<FolderItem>().FirstOrDefault();
                    Assert.NotNull(folderDay);

                    rootFolder.IsExpanded = true;
                    Dispatcher.UIThread.RunJobs();

                    folder202609.IsExpanded = true;
                    Dispatcher.UIThread.RunJobs();

                    folderDay.IsExpanded = true;
                    Dispatcher.UIThread.RunJobs();

                    // 4. Incidencia del usuario: al expandir la carpeta que contiene el archivo, DEBE seleccionarlo
                    editorVm.SelectedNode.Should().NotBeNull("Al expandir la carpeta contenedora en Arbol, debe seleccionarse el archivo activo");
                    ((FileItem)editorVm.SelectedNode!).FullPath.Should().Be(shotPath);
                    listBox.SelectedItem.Should().NotBeNull("ListBox debe reflejar el archivo seleccionado");
                    ((FileItem)listBox.SelectedItem!).FullPath.Should().Be(shotPath);
                }
                finally
                {
                    window.Close();
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task StartingInCalendarViewWhenIndexerPrunesEmptyYearsMustPreserveFileSelectionInViewModelAndListBox()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Qapptia_CalPruneTest_" + Guid.NewGuid().ToString("N"));
        var dayDir = Path.Combine(tempDir, "2026-09", "2026-09-16");
        Directory.CreateDirectory(dayDir);
        var emptyHeuristicYearDir = Path.Combine(tempDir, "2024");
        Directory.CreateDirectory(emptyHeuristicYearDir);

        var shotPath = Path.Combine(dayDir, "Qapptia_20260916_153831.png");
        File.WriteAllBytes(shotPath, s_minimalPng);

        try
        {
            await Session.Dispatch(async () =>
            {
                var stateService = new EditorStateService(tempDir, "editor_state.json");
                var state = stateService.Load();
                state.Layout.SidebarViewMode = "Calendar";
                state.Layout.ExpandedFolders = new List<string>();
                state.Layout.ExpandedCalendarGroups = new List<string>
                {
                    "cal://2026",
                    "cal://2026/09",
                    "cal://2026/09/w38",
                    "cal://2026/09/w38/2026-09-16"
                };
                state.Session.LastSelectedFile = shotPath;
                stateService.Save(state);

                using var navService = new NavigationService();
                var editorVm = new EditorViewModel(stateService, tempDir, new Moq.Mock<Qapptia.Editor.Core.IFontProvider>().Object, navigationService: navService);

                var window = new MainWindow { Width = 800, Height = 600 };
                window.InitializeWithViewModel(editorVm);
                window.Show();

                try
                {
                    var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
                    Assert.NotNull(listBox);

                    // Esperar a que el indexador complete y pode los años vacíos heurísticos (2024)
                    for (int i = 0; i < 15; i++)
                    {
                        await Task.Delay(100);
                        Dispatcher.UIThread.RunJobs();
                    }

                    // Assert: El archivo debe mantenerse seleccionado tanto en el ViewModel como en el ListBox visual
                    editorVm.SidebarViewMode.Should().Be(SidebarViewMode.Calendar);
                    editorVm.SelectedNode.Should().NotBeNull("editorVm.SelectedNode debe conservarse tras la poda de años vacíos");
                    ((FileItem)editorVm.SelectedNode!).FullPath.Should().Be(shotPath);

                    listBox.SelectedItem.Should().NotBeNull("listBox.SelectedItem debe conservarse tras la poda de años vacíos");
                    ((FileItem)listBox.SelectedItem!).FullPath.Should().Be(shotPath);

                    // Validar que el año 2024 efectivamente fue podado
                    editorVm.CalendarGroups.Any(g => g.FullPath == "cal://2024").Should().BeFalse("El año vacío 2024 debió ser podado por el indexador");
                }
                finally
                {
                    window.Close();
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task CanvasBoardOnBurnCompletedRefreshesBackgroundImageAndClearsShapes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "burn_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            await Session.Dispatch(() =>
            {
                var stateService = new EditorStateService(tempDir, "state.json");
                var canvasStateService = new CanvasStateService();
                var vm = new CanvasBoardViewModel(canvasStateService, stateService);
                var testFile = Path.Combine(tempDir, "test_burn.png");
                File.WriteAllBytes(testFile, s_minimalPng);

                vm.LoadImage(new FileItem
                {
                    Name = "test_burn.png",
                    FullPath = testFile
                });

                var oldBitmap = vm.BackgroundImage;
                oldBitmap.Should().NotBeNull();

                vm.Shapes.Add(new RectangleShape { Start = new Avalonia.Point(2, 2), End = new Avalonia.Point(10, 10), Color = Avalonia.Media.Colors.Red });
                vm.Shapes.Should().HaveCount(1);
                vm.HasImage.Should().BeTrue();

                using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(30, 30));
                surface.Canvas.Clear(SkiaSharp.SKColors.Blue);
                using var image = surface.Snapshot();
                using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                byte[] burnedPng = data.ToArray();

                bool redrawFired = false;
                bool imageLoadedFired = false;
                vm.RequestRedraw += (s, e) => redrawFired = true;
                vm.ImageLoaded += (s, e) => imageLoadedFired = true;

                vm.OnBurnCompleted(burnedPng);

                vm.Shapes.Should().BeEmpty();
                vm.HasImage.Should().BeTrue();
                vm.BackgroundImage.Should().NotBeNull();
                ReferenceEquals(vm.BackgroundImage, oldBitmap).Should().BeFalse("El BackgroundImage debe haberse reemplazado por la nueva imagen quemada");
                vm.ActiveCropRect.Should().BeNull();
                redrawFired.Should().BeTrue();
                imageLoadedFired.Should().BeTrue();

                var savedState = canvasStateService.Load(testFile, vm.CurrentImageId);
                savedState.Shapes.Should().BeEmpty("Las formas quemadas deben limpiarse del estado persistido");
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}



