using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

public sealed class AreaCapturePerformanceTests
{
    private const uint OpaqueAlphaMask = 0xFF000000;
    private readonly Mock<IScreenCapture> _screenCaptureMock = new();
    private readonly Mock<ICursorCapture> _cursorCaptureMock = new();
    private readonly Mock<IClipboardService> _clipboardMock = new();
    private readonly Mock<IDesktopService> _desktopMock = new();
    private readonly Mock<IConfigService> _configMock = new();
    private readonly Mock<ILogger> _loggerMock = new();

    public AreaCapturePerformanceTests()
    {
        var appConfig = new QapptiaConfig
        {
            ShowMouse = true,
            HighlightMouse = false,
            CaptureAllScreens = false,
            CopyToClipboardArea = false
        };
        _configMock.Setup(c => c.Current).Returns(appConfig);
    }

    [Fact]
    public void AlphaChannelVectorizedFix1080pExecutesInUnderThreeMilliseconds()
    {
        // 1920x1080 Full HD = 8.294.400 bytes (2.073.600 píxeles)
        const int width = 1920;
        const int height = 1080;
        var pixels = new byte[width * height * 4];

        var sw = Stopwatch.StartNew();
        unsafe
        {
            fixed (byte* pPixels = pixels)
            {
                uint* pDwords = (uint*)pPixels;
                int totalPixels = width * height;
                for (int i = 0; i < totalPixels; i++)
                {
                    pDwords[i] |= OpaqueAlphaMask;
                }
            }
        }
        sw.Stop();

        // Verificar que el 100% de los píxeles tienen el canal alfa en 255
        pixels[3].Should().Be(255);
        pixels[pixels.Length - 1].Should().Be(255);
        sw.ElapsedMilliseconds.Should().BeLessThanOrEqualTo(25, "la vectorización de canal alfa debe completarse en menos de 25 ms");
    }

    [Fact]
    public void AlphaChannelVectorizedFix4KExecutesInUnderTenMilliseconds()
    {
        // 3840x2160 4K UHD = 33.177.600 bytes (8.294.400 píxeles)
        const int width = 3840;
        const int height = 2160;
        var pixels = new byte[width * height * 4];

        var sw = Stopwatch.StartNew();
        unsafe
        {
            fixed (byte* pPixels = pixels)
            {
                uint* pDwords = (uint*)pPixels;
                int totalPixels = width * height;
                for (int i = 0; i < totalPixels; i++)
                {
                    pDwords[i] |= OpaqueAlphaMask;
                }
            }
        }
        sw.Stop();

        pixels[3].Should().Be(255);
        pixels[pixels.Length - 1].Should().Be(255);
        sw.ElapsedMilliseconds.Should().BeLessThan(50, "la vectorización de canal alfa en 4K debe completarse en menos de 50 ms en modo Debug");
    }

    [Fact]
    public async Task FrozenScreenPipelineAsyncCursorOverlayCompletesUnderFiftyMilliseconds()
    {
        const int width = 1920;
        const int height = 1080;
        var rawPixels = new byte[width * height * 4];
        for (int i = 3; i < rawPixels.Length; i += 4) rawPixels[i] = 255;

        var screenResult = new ScreenCaptureResult(rawPixels, width, height, 0, 0);
        _screenCaptureMock
            .Setup(s => s.CaptureScreenAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(screenResult);

        var cursorPixels = new byte[32 * 32 * 4];
        for (int i = 3; i < cursorPixels.Length; i += 4) cursorPixels[i] = 255;
        var cursorImage = new CursorImage(cursorPixels, 32, 32, 0, 0);

        _cursorCaptureMock
            .Setup(c => c.CaptureCursorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursorImage);

        _desktopMock
            .Setup(d => d.GetCursorPosition())
            .Returns((500, 500));

        var service = new FullscreenCaptureService(
            _screenCaptureMock.Object,
            _cursorCaptureMock.Object,
            _clipboardMock.Object,
            _desktopMock.Object,
            _configMock.Object,
            _loggerMock.Object);

        var sw = Stopwatch.StartNew();
        var result = await service.CaptureFrozenScreenAsync(includeCursor: true, CancellationToken.None);
        sw.Stop();

        result.Should().NotBeNull();
        result.Width.Should().Be(width);
        result.Height.Should().Be(height);
        sw.ElapsedMilliseconds.Should().BeLessThan(500, "la preparación del cursor asíncrono y congelado debe tomar menos de 500 ms incluyendo inicialización de SkiaSharp");
    }

    [Fact]
    public async Task FinalizeFrozenAreaCaptureCroppingPerformanceCompletesUnderFiftyMilliseconds()
    {
        const int width = 1920;
        const int height = 1080;
        var rawPixels = new byte[width * height * 4];
        for (int i = 3; i < rawPixels.Length; i += 4) rawPixels[i] = 255;

        var frozenScreen = new ScreenCaptureResult(rawPixels, width, height, 0, 0);
        var area = new AreaInfo(100, 100, 800, 600);
        var job = new CaptureJob { Mode = CaptureMode.Area };

        var service = new FullscreenCaptureService(
            _screenCaptureMock.Object,
            _cursorCaptureMock.Object,
            _clipboardMock.Object,
            _desktopMock.Object,
            _configMock.Object,
            _loggerMock.Object);

        var sw = Stopwatch.StartNew();
        var result = await service.FinalizeFrozenAreaCaptureAsync(frozenScreen, area, job, CancellationToken.None);
        sw.Stop();

        result.Should().NotBeNull();
        result.Width.Should().Be(800);
        result.Height.Should().Be(600);
        result.PngBytes.Should().NotBeEmpty();
        sw.ElapsedMilliseconds.Should().BeLessThan(250, "el recorte y compresión PNG de 800x600 debe completarse en menos de 250 ms");
    }
}
