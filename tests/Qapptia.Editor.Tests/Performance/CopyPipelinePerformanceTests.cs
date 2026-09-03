using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using FluentAssertions;
using Moq;
using Qapptia.App.Editor.Services;
using Qapptia.App.Editor.ViewModels;
using Qapptia.App.Editor.ViewModels.Shapes;
using Qapptia.Core.Abstractions;
using Qapptia.Editor.Core;
using Qapptia.Editor.Models;
using Qapptia.Editor.Services;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;
using EditorGeometry = Qapptia.Editor.Models.Geometry;

namespace Qapptia.Editor.Tests.Performance;

public sealed class CopyPipelinePerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testDir;
    private readonly EditorStateService _stateService;
    private readonly CanvasStateService _canvasStateService;
    private readonly Mock<IFontProvider> _fontProviderMock;

    static CopyPipelinePerformanceTests()
    {
        try
        {
            AppBuilder.Configure<Application>()
                .UsePlatformDetect()
                .SetupWithoutStarting();
        }
        catch
        {
            // Ignorar si la plataforma de Avalonia ya fue inicializada en este proceso
        }
    }

    public CopyPipelinePerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _testDir = Path.Combine(Path.GetTempPath(), "Qapptia_Perf_" + Guid.NewGuid().ToString("N"));
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
        catch
        {
            // Ignorar limpieza de archivos temporales
        }
    }

    [Fact]
    public async Task CopyPipelineWith1080pImageAndMultipleVectorsCompletesInLessThanOneSecond()
    {
        // 1. Arrange: Simular imagen 1920x1080 Full HD con fondo y formas dibujadas
        const int width = 1920;
        const int height = 1080;

        using var memoryStream = new MemoryStream();
        using (var skBmp = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul))
        {
            using var canvas = new SKCanvas(skBmp);
            canvas.Clear(SKColors.DarkSlateGray);
            using var paint = new SKPaint { Color = SKColors.LightYellow, StrokeWidth = 3 };
            for (int i = 0; i < 20; i++)
            {
                canvas.DrawRect(i * 90, i * 50, 80, 40, paint);
            }
            skBmp.Encode(memoryStream, SKEncodedImageFormat.Png, 100);
        }
        memoryStream.Position = 0;
        var backgroundImage = new Bitmap(memoryStream);

        // 2. Arrange: Simular múltiples figuras vectoriales sobre el tablero (rectángulos, líneas, flechas, texto)
        var shapes = new List<VectorShape>();

        var rectGeom = new EditorGeometry.RectangleGeometry
        {
            Start = new Point(100, 100),
            End = new Point(400, 300),
            Color = Colors.LimeGreen,
            StrokeWidth = 4
        };
        shapes.Add(ShapeViewFactory.Wrap(rectGeom));

        var lineGeom = new EditorGeometry.LineGeometry
        {
            Start = new Point(50, 50),
            End = new Point(500, 400),
            Color = Colors.Red,
            StrokeWidth = 3
        };
        shapes.Add(ShapeViewFactory.Wrap(lineGeom));

        var arrowGeom = new EditorGeometry.ArrowGeometry
        {
            Start = new Point(80, 80),
            End = new Point(600, 350),
            Color = Colors.Yellow,
            StrokeWidth = 4
        };
        shapes.Add(ShapeViewFactory.Wrap(arrowGeom));

        var textGeom = new EditorGeometry.TextGeometry
        {
            Start = new Point(200, 200),
            End = new Point(500, 260),
            Text = "Texto de anotación para benchmark de rendimiento",
            Color = Colors.White,
            StrokeWidth = 2
        };
        shapes.Add(ShapeViewFactory.Wrap(textGeom));

        // Mock del servicio de portapapeles
        byte[]? clipboardPixelsReceived = null;
        byte[]? clipboardPngReceived = null;
        var clipboardMock = new Mock<IClipboardService>();
        clipboardMock
            .Setup(c => c.SetRawImageAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<byte[]?>(), default))
            .Callback<byte[], int, int, byte[]?, System.Threading.CancellationToken>((pixels, _, _, png, _) =>
            {
                clipboardPixelsReceived = pixels;
                clipboardPngReceived = png;
            })
            .Returns(Task.CompletedTask);

        var vm = new EditorViewModel(
            _stateService,
            _testDir,
            _fontProviderMock.Object,
            clipboardService: clipboardMock.Object,
            canvasStateService: _canvasStateService);

        // 3. Act: Ejecutar el pipeline de copiado midiendo el tiempo exacto
        var totalStopwatch = Stopwatch.StartNew();

        // Paso A: Renderizado off-screen aislado + extracción de píxeles crudos + compresión PNG rápida
        var exportStopwatch = Stopwatch.StartNew();
        var (rawPixels, outWidth, outHeight, pngBytes) = BoardImageExporter.ExportBurnedImage(
            backgroundImage,
            shapes,
            activeCropRect: null,
            width,
            height);
        exportStopwatch.Stop();

        // Paso B: Despacho al portapapeles y activación del Toast de confirmación
        var clipboardStopwatch = Stopwatch.StartNew();
        await vm.CopyImageToClipboardAsync(rawPixels, outWidth, outHeight, pngBytes);
        clipboardStopwatch.Stop();

        totalStopwatch.Stop();
        long totalElapsedMs = totalStopwatch.ElapsedMilliseconds;

        // 4. Reporte detallado de tiempos
        _output.WriteLine($"=== RESULTADOS BENCHMARK PIPELINE DE COPIADO (1080p + Vectores) ===");
        _output.WriteLine($"Paso A (ExportBurnedImage - Render + CopyPixels + Fast PNG): {exportStopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"Paso B (Portapapeles + Toast): {clipboardStopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"TIEMPO TOTAL DEL PIPELINE: {totalElapsedMs} ms");
        _output.WriteLine($"==================================================================");

        // 5. Assert: Invariantes y rendimiento estricto (< 1 segundo)
        clipboardPixelsReceived.Should().NotBeNull();
        clipboardPixelsReceived!.Length.Should().BeGreaterThan(0);
        clipboardPngReceived.Should().NotBeNull();
        clipboardPngReceived!.Length.Should().BeGreaterThan(0);
        vm.IsToastVisible.Should().BeTrue();
        vm.ToastMessage.Should().Be(Qapptia.App.Editor.Common.Constants.ToastImageCopied);

        totalElapsedMs.Should().BeLessThan(1000, "el proceso completo de copiado hasta el Toast debe tomar estrictamente menos de 1 segundo (1000 ms)");
    }
}
