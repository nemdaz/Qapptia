using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DynamicData;
using FluentAssertions;
using Qapptia.Editor.Models;
using Qapptia.Editor.Models.Navigation;
using Xunit;

namespace Qapptia.Editor.Tests.Models;

public class GroupItemTests
{
    [Fact]
    public void BatchInjectionWithAddRangeNotifiesOnlyOncePerBatch()
    {
        // Arrange
        using var groupItem = new GroupItem();
        int propertyChangedCount = 0;
        
        groupItem.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(GroupItem.HasFiles) || args.PropertyName == nameof(GroupItem.IsDimmed))
            {
                propertyChangedCount++;
            }
        };

        var batchFiles = new List<FileItem>();
        for (int i = 0; i < 5000; i++)
        {
            batchFiles.Add(new FileItem { FullPath = $"C:/file_{i}.jpg" });
        }

        // Act
        // Insertamos los 5000 archivos en una sola inyección atómica (AddRange)
        groupItem.ItemsSource.AddRange(batchFiles.Cast<NavigationItem>());

        // Assert
        groupItem.HasFiles.Should().BeTrue("El nodo debe marcarse como contenedor de archivos");
        groupItem.IsDimmed.Should().BeFalse("El nodo debe perder opacidad al recibir archivos");

        // DynamicData con AddRange emite 1 solo ChangeSet: la cascada de notificaciones derivadas
        // se reduce a la transición de HasFiles, no a una avalancha por ítem
        propertyChangedCount.Should().Be(1, "Se esperaba una única notificación de cambio gracias al empalme por lotes atómico, evitando el congelamiento UI");
    }
    
    [Fact]
    public void LoadingSpinnerDisablesUponScanCompleted()
    {
        // Arrange
        using var groupItem = new GroupItem { IsLoading = true, Kind = GroupKind.Folder };
        
        // Act: La llegada del primer lote mantiene el spinner activo para microbatching
        groupItem.ItemsSource.Add(new FileItem { FullPath = "C:/test.png" });
        groupItem.IsLoading.Should().BeTrue("El spinner debe permanecer activo durante el streaming progresivo de lotes");

        // Al culminar la inspección completa de la carpeta, el spinner se apaga
        groupItem.IsScanCompleted = true;
        
        // Assert
        groupItem.IsLoading.Should().BeFalse("El spinner debe apagarse al culminar toda la carpeta");
    }

    [Fact]
    public void EmptyNodeKeepsChevronUntilScanConfirmed()
    {
        // Ciclo de vida del chevron: todo nodo nace explorable aunque esté vacío
        using var groupItem = new GroupItem { Kind = GroupKind.Day };

        groupItem.IsEmptyConfirmed.Should().BeFalse("el nodo nace con chevron explorable aunque no tenga contenido");
        groupItem.IsDimmed.Should().BeFalse("un nodo sin verificar no debe atenuarse");
    }

    [Fact]
    public void EmptyNodeHidesChevronAfterScanCompleted()
    {
        using var groupItem = new GroupItem { Kind = GroupKind.Day };

        groupItem.IsScanCompleted = true;

        groupItem.IsEmptyConfirmed.Should().BeTrue("día inspeccionado sin archivos queda confirmado como vacío");
        groupItem.IsDimmed.Should().BeTrue("la fila vacía confirmada se atenúa");
    }

    [Fact]
    public void EmptyConfirmedNodeRecoversWhenFileArrives()
    {
        using var groupItem = new GroupItem { Kind = GroupKind.Day, IsScanCompleted = true };
        groupItem.IsEmptyConfirmed.Should().BeTrue();

        groupItem.ItemsSource.Add(new FileItem { FullPath = "C:/late.png" });

        groupItem.IsEmptyConfirmed.Should().BeFalse("la llegada de un archivo reactiva el chevron del nodo");
        groupItem.IsDimmed.Should().BeFalse();
        groupItem.HasFiles.Should().BeTrue();
    }

    [Fact]
    public void PerformanceMassiveFolderExpansionDisablesSpinnerInstantly()
    {
        // Arrange
        using var groupItem = new GroupItem { IsLoading = true, Kind = GroupKind.Folder };
        var batchFiles = Enumerable.Range(0, 5000)
            .Select(i => new FileItem { FullPath = $"C:/mass_file_{i}.jpg" })
            .ToList();

        // Precalentar la ruta JIT de inyección masiva: la medición apunta a la latencia en caliente del hilo,
        // no al coste único de compilación JIT del proceso de pruebas
        using (var warmupGroup = new GroupItem { IsLoading = true, Kind = GroupKind.Folder })
        {
            warmupGroup.ItemsSource.AddRange(batchFiles.Cast<NavigationItem>());
        }

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        groupItem.ItemsSource.AddRange(batchFiles.Cast<NavigationItem>());
        stopwatch.Stop();
        groupItem.IsLoading.Should().BeTrue("El spinner debe permanecer activo mientras la inspección no haya culminado");

        groupItem.IsScanCompleted = true;

        // Assert
        groupItem.IsLoading.Should().BeFalse("El spinner debe apagarse al culminar toda la carpeta");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50, "La inyección en memoria de 5000 elementos a través de AddRange (DynamicData) debe tomar menos de 50 milisegundos, garantizando UX imperceptible");
    }
}
