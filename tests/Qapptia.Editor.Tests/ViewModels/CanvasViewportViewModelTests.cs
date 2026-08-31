using FluentAssertions;
using Qapptia.App.Editor.ViewModels;
using Xunit;

namespace Qapptia.Editor.Tests.ViewModels;

public sealed class CanvasViewportViewModelTests
{
    [Fact]
    public void CanvasViewportViewModelHasCorrectInitialValues()
    {
        var vm = new CanvasViewportViewModel();

        vm.ZoomLevel.Should().Be(1.0f);
        vm.SelectedZoomString.Should().Be("100%");
        vm.ZoomOptions.Should().Contain("100%");
        vm.ZoomOptions.Should().Contain("50%");
        vm.ZoomOptions.Should().Contain("200%");
    }

    [Fact]
    public void CanvasViewportViewModelZoomLevelChangeUpdatesSelectedZoomString()
    {
        var vm = new CanvasViewportViewModel();

        vm.ZoomLevel = 1.5f;

        vm.SelectedZoomString.Should().Be("150%");
        vm.ZoomOptions.Should().Contain("150%");
    }

    [Fact]
    public void CanvasViewportViewModelSelectedZoomStringParsesAndClampsCorrectly()
    {
        var vm = new CanvasViewportViewModel();

        vm.SelectedZoomString = "75%";
        vm.ZoomLevel.Should().Be(0.75f);

        // Clamping mínimo (10%)
        vm.SelectedZoomString = "5%";
        vm.ZoomLevel.Should().Be(0.1f);

        // Clamping máximo (9999%)
        vm.SelectedZoomString = "20000%";
        vm.ZoomLevel.Should().Be(99.99f);
    }

    [Fact]
    public void CanvasViewportViewModelRealSizeSetsZoomLevelToOne()
    {
        var vm = new CanvasViewportViewModel { ZoomLevel = 2.5f };

        vm.RealSize();

        vm.ZoomLevel.Should().Be(1.0f);
        vm.SelectedZoomString.Should().Be("100%");
    }

    [Fact]
    public void CanvasViewportViewModelFitImageRaisesEvent()
    {
        var vm = new CanvasViewportViewModel();
        bool eventRaised = false;
        vm.FitImageRequested += (s, e) => eventRaised = true;

        vm.FitImage();

        eventRaised.Should().BeTrue();
    }
}
