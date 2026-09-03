using System;
using System.IO;
using Avalonia;
using Avalonia.Media;
using FluentAssertions;
using Moq;
using Qapptia.App.Editor.ViewModels;
using Qapptia.App.Editor.ViewModels.Shapes;
using Qapptia.Editor.Core;
using Qapptia.Editor.Models;
using Qapptia.Editor.Services;
using Qapptia.Editor.Tools;
using Qapptia.UI.Components.Controls;
using Xunit;

namespace Qapptia.Editor.Tests.ViewModels;

public sealed class EditorViewModelTests : IDisposable
{
    private readonly string _testDir;
    private readonly EditorStateService _stateService;
    private readonly CanvasStateService _canvasStateService;
    private readonly Mock<IFontProvider> _fontProviderMock;

    public EditorViewModelTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Qapptia_EditorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _stateService = new EditorStateService(_testDir, "state.json");
        _canvasStateService = new CanvasStateService();
        _fontProviderMock = new Mock<IFontProvider>();
    }

    public void Dispose()
    {
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
    public void EditorViewModelSwitchingToolDoesNotRecolorSelectedShape()
    {
        // 1. Configurar estado con colores favoritos diferentes para Line y Ellipse
        var state = new EditorState();
        state.Palette.ToolFavoriteColors["line"] = "#FFFF8000"; // Naranja
        state.Palette.ToolFavoriteColors["ellipse"] = "#FF0000FF"; // Azul
        _stateService.Save(state);

        var vm = new EditorViewModel(_stateService, _testDir, _fontProviderMock.Object, canvasStateService: _canvasStateService);

        // 2. Seleccionar herramienta Line y agregar una figura seleccionada en Naranja
        vm.SelectTool(ShapeFactory.Line);
        var originalColor = Color.Parse("#FFFF8000");
        var lineShape = new LineShape
        {
            Start = new Point(10, 10),
            End = new Point(100, 100),
            Color = originalColor,
            IsSelected = true
        };
        vm.Shapes.Add(lineShape);

        // 3. Cambiar a herramienta Ellipse (que tiene favorito Azul)
        vm.SelectTool(ShapeFactory.Ellipse);

        // 4. La figura anterior debe conservar su color original (Naranja) y quedar deseleccionada
        lineShape.Color.Should().Be(originalColor);
        lineShape.IsSelected.Should().BeFalse();

        // 5. La herramienta activa debe ser Ellipse con su color Azul
        vm.ActiveTool.Should().BeOfType<EllipseTool>();
        vm.ActiveColor.Should().Be(Color.Parse("#FF0000FF"));
    }

    [Fact]
    public async Task CopyImageToClipboardAsyncDelegatesToClipboardService()
    {
        var clipboardMock = new Mock<Qapptia.Core.Abstractions.IClipboardService>();
        var vm = new EditorViewModel(_stateService, _testDir, _fontProviderMock.Object, clipboardService: clipboardMock.Object, canvasStateService: _canvasStateService);

        byte[] testBytes = [1, 2, 3, 4];
        await vm.CopyImageToClipboardAsync(testBytes);

        clipboardMock.Verify(c => c.SetImageAsync(testBytes, System.Threading.CancellationToken.None), Times.Once);
        vm.ToastMessage.Should().Be(Qapptia.App.Editor.Common.Constants.ToastImageCopied);
        vm.ToastType.Should().Be(ToastNotificationType.Success);
    }

    [Fact]
    public async Task CopyImageToClipboardAsyncWarnsWhenClipboardServiceIsNull()
    {
        var vm = new EditorViewModel(_stateService, _testDir, _fontProviderMock.Object, clipboardService: null, canvasStateService: _canvasStateService);

        byte[] testBytes = [1, 2, 3];
        await vm.CopyImageToClipboardAsync(testBytes);

        vm.ToastMessage.Should().Be(Qapptia.App.Editor.Common.Constants.ToastClipboardUnavailable);
        vm.ToastType.Should().Be(ToastNotificationType.Warning);
    }

    [Fact]
    public async Task CopyImageToClipboardAsyncHandlesExceptionGracefully()
    {
        var clipboardMock = new Mock<Qapptia.Core.Abstractions.IClipboardService>();
        clipboardMock.Setup(c => c.SetImageAsync(It.IsAny<byte[]>(), System.Threading.CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Win32 error"));

        var vm = new EditorViewModel(_stateService, _testDir, _fontProviderMock.Object, clipboardService: clipboardMock.Object, canvasStateService: _canvasStateService);

        byte[] testBytes = [1, 2, 3];
        await vm.CopyImageToClipboardAsync(testBytes);

        vm.ToastMessage.Should().Be(Qapptia.App.Editor.Common.Constants.ToastCopyError);
        vm.ToastType.Should().Be(ToastNotificationType.Error);
    }

    [Fact]
    public async Task CopyImageToClipboardAsyncWithRawPixelsDelegatesToSetRawImageAsync()
    {
        var clipboardMock = new Mock<Qapptia.Core.Abstractions.IClipboardService>();
        var vm = new EditorViewModel(_stateService, _testDir, _fontProviderMock.Object, clipboardService: clipboardMock.Object, canvasStateService: _canvasStateService);

        byte[] rawPixels = [1, 2, 3, 4];
        byte[] pngBytes = [5, 6, 7, 8];
        await vm.CopyImageToClipboardAsync(rawPixels, 1, 1, pngBytes);

        clipboardMock.Verify(c => c.SetRawImageAsync(rawPixels, 1, 1, pngBytes, System.Threading.CancellationToken.None), Times.Once);
        vm.ToastMessage.Should().Be(Qapptia.App.Editor.Common.Constants.ToastImageCopied);
        vm.ToastType.Should().Be(ToastNotificationType.Success);
    }
}
