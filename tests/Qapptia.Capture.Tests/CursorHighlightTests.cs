using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Qapptia.Capture;
using Qapptia.Core.Abstractions;
using Qapptia.Core.Capture;
using Qapptia.Core.Configuration;
using Serilog;
using SkiaSharp;
using Xunit;

namespace Qapptia.Capture.Tests;

public sealed class CursorHighlightTests
{
    private readonly Mock<IScreenCapture> _screenCaptureMock = new();
    private readonly Mock<ICursorCapture> _cursorCaptureMock = new();
    private readonly Mock<IClipboardService> _clipboardMock = new();
    private readonly Mock<IDesktopService> _desktopMock = new();
    private readonly Mock<IConfigService> _configMock = new();
    private readonly Mock<ILogger> _loggerMock = new();

    [Fact]
    public async Task CursorHighlightRendersLuminousHaloUnderneathCursor()
    {
        const int width = 200;
        const int height = 200;
        var rawPixels = new byte[width * height * 4];
        // Pantalla negra inicial opaca
        for (int i = 0; i < rawPixels.Length; i += 4)
        {
            rawPixels[i] = 0;       // B
            rawPixels[i + 1] = 0;   // G
            rawPixels[i + 2] = 0;   // R
            rawPixels[i + 3] = 255; // A
        }

        var screenResult = new ScreenCaptureResult(rawPixels, width, height, 0, 0);
        _screenCaptureMock
            .Setup(s => s.CaptureScreenAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(screenResult);

        // Cursor: un pixel blanco en el hotspot (0, 0)
        var cursorPixels = new byte[32 * 32 * 4];
        cursorPixels[0] = 255; // B
        cursorPixels[1] = 255; // G
        cursorPixels[2] = 255; // R
        cursorPixels[3] = 255; // A (blanco puro)

        var cursorImage = new CursorImage(cursorPixels, 32, 32, 0, 0);
        _cursorCaptureMock
            .Setup(c => c.CaptureCursorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursorImage);

        _desktopMock
            .Setup(d => d.GetCursorPosition())
            .Returns((100, 100));

        var config = new QapptiaConfig
        {
            ShowMouse = true,
            HighlightMouse = true
        };
        _configMock.Setup(c => c.Current).Returns(config);

        var service = new FullscreenCaptureService(
            _screenCaptureMock.Object,
            _cursorCaptureMock.Object,
            _clipboardMock.Object,
            _desktopMock.Object,
            _configMock.Object,
            _loggerMock.Object);

        var frozen = await service.CaptureFrozenScreenAsync(includeCursor: true, CancellationToken.None);

        frozen.Should().NotBeNull();

        // Alrededor del cursor (ej. a 10px de distancia), debe haber presencia del halo cálido (R y G elevados)
        int sampleX = 100 + 10;
        int sampleY = 100 + 10;
        int sampleIndex = (sampleY * width + sampleX) * 4;

        byte b = frozen.BgraPixels[sampleIndex];
        byte g = frozen.BgraPixels[sampleIndex + 1];
        byte r = frozen.BgraPixels[sampleIndex + 2];
        byte a = frozen.BgraPixels[sampleIndex + 3];

        // El halo amarillo cálido tiene R y G altos y B bajo sobre fondo negro
        r.Should().BeGreaterThan(0, "el halo debe iluminar el componente rojo");
        g.Should().BeGreaterThan(0, "el halo debe iluminar el componente verde");
        a.Should().Be(255);
    }
}
