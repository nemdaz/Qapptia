using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Qapptia.Editor.Sidebar.Models;
using Qapptia.Editor.Sidebar.Services;
using Xunit;

namespace Qapptia.Editor.Tests;

public sealed class SidebarServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly SidebarService _sut;

    public SidebarServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Qapptia_SidebarTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _sut = new SidebarService();
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
        catch
        {
            // Limpieza en temp
        }
    }

    [Fact]
    public async Task BuildTreeAsyncOrdersFilesNewestToOldestRegardlessOfName()
    {
        // Arrange: Crear archivo con nombre alfabéticamente posterior pero fecha más antigua
        var oldFile = Path.Combine(_testDir, "zzz_old_file.png");
        File.WriteAllBytes(oldFile, new byte[] { 1, 2, 3 });
        File.SetCreationTimeUtc(oldFile, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(oldFile, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));

        // Crear archivo con nombre alfabéticamente anterior pero fecha más reciente
        var newFile = Path.Combine(_testDir, "aaa_new_file.png");
        File.WriteAllBytes(newFile, new byte[] { 4, 5, 6 });
        File.SetCreationTimeUtc(newFile, new DateTime(2026, 8, 20, 15, 30, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newFile, new DateTime(2026, 8, 20, 15, 30, 0, DateTimeKind.Utc));

        // Act
        var tree = await _sut.BuildTreeAsync(_testDir, Array.Empty<string>());

        // Assert
        tree.Should().NotBeNull();
        tree!.Items.Should().HaveCount(2);

        var firstFile = tree.Items[0].Should().BeOfType<SidebarFile>().Subject;
        var secondFile = tree.Items[1].Should().BeOfType<SidebarFile>().Subject;

        firstFile.Name.Should().Be("aaa_new_file.png");
        secondFile.Name.Should().Be("zzz_old_file.png");
    }

    [Fact]
    public async Task BuildTreeAsyncOrdersFoldersByEffectiveDateOfContentsRegardlessOfFolderName()
    {
        // Arrange
        var folderA = Path.Combine(_testDir, "A_OldContent");
        var folderB = Path.Combine(_testDir, "Z_RecentContent");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);

        var oldCapture = Path.Combine(folderA, "capture1.png");
        File.WriteAllBytes(oldCapture, new byte[] { 1 });
        File.SetCreationTimeUtc(oldCapture, new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(oldCapture, new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        var recentCapture = Path.Combine(folderB, "capture2.png");
        File.WriteAllBytes(recentCapture, new byte[] { 2 });
        File.SetCreationTimeUtc(recentCapture, new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(recentCapture, new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc));

        // Act
        var tree = await _sut.BuildTreeAsync(_testDir, Array.Empty<string>());

        // Assert: folderB (Z_RecentContent) debe aparecer ANTES que folderA (A_OldContent)
        tree.Should().NotBeNull();
        var folders = tree!.Items.OfType<SidebarFolder>().ToList();
        folders.Should().HaveCount(2);
        folders[0].Name.Should().Be("Z_RecentContent");
        folders[1].Name.Should().Be("A_OldContent");
    }

    [Fact]
    public async Task BuildTreeAsyncIgnoresHiddenFoldersSuchAsAnnotations()
    {
        // Arrange
        var hiddenDir = Path.Combine(_testDir, ".annotations");
        Directory.CreateDirectory(hiddenDir);
        File.WriteAllBytes(Path.Combine(hiddenDir, "test.png"), new byte[] { 1 });

        var visibleDir = Path.Combine(_testDir, "2026-08");
        Directory.CreateDirectory(visibleDir);
        File.WriteAllBytes(Path.Combine(visibleDir, "capture.png"), new byte[] { 2 });

        // Act
        var tree = await _sut.BuildTreeAsync(_testDir, Array.Empty<string>());

        // Assert
        tree.Should().NotBeNull();
        tree!.Items.OfType<SidebarFolder>().Should().ContainSingle(f => f.Name == "2026-08");
        tree.Items.OfType<SidebarFolder>().Should().NotContain(f => f.Name == ".annotations");
    }

    [Fact]
    public async Task FindNodeByPathLocatesDeeplyNestedItems()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "sub1", "sub2");
        Directory.CreateDirectory(subDir);
        var targetFile = Path.Combine(subDir, "target.png");
        File.WriteAllBytes(targetFile, new byte[] { 1 });

        var tree = await _sut.BuildTreeAsync(_testDir, Array.Empty<string>());

        // Act
        var found = _sut.FindNodeByPath(new[] { tree! }, targetFile);

        // Assert
        found.Should().NotBeNull();
        found!.Name.Should().Be("target.png");
    }
}
