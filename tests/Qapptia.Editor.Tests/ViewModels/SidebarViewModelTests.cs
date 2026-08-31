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
    public async Task SidebarViewModelLoadSidebarImagesAsyncPopulatesFolders()
    {
        var subDir = Path.Combine(_testDir, "Screenshots");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "shot1.png");
        File.WriteAllBytes(testFile, new byte[] { 1, 2, 3 });

        var vm = new SidebarViewModel(_navigationService, _stateService, _testDir);

        await vm.LoadSidebarImagesAsync();

        vm.SidebarFolders.Should().NotBeEmpty();
        var foundNode = vm.FindNodeByPath(testFile);
        foundNode.Should().NotBeNull();
        foundNode.Should().BeOfType<FileItem>();
    }

    [Fact]
    public void SidebarViewModelSelectedNodeChangeRaisesFileSelectedEvent()
    {
        var vm = new SidebarViewModel(_navigationService, _stateService, _testDir);
        FileItem? selectedFile = null;
        vm.FileSelected += (s, file) => selectedFile = file;

        var dummyFile = new FileItem { Name = "test.png", FullPath = Path.Combine(_testDir, "test.png") };
        vm.SelectedNode = dummyFile;

        selectedFile.Should().Be(dummyFile);
    }
}
