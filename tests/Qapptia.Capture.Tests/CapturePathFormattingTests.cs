using System;
using System.IO;
using FluentAssertions;
using Moq;
using Qapptia.Capture;
using Qapptia.Core.Abstractions;
using Qapptia.Core.Configuration;
using Serilog;
using Xunit;

namespace Qapptia.Capture.Tests;

public sealed class CapturePathFormattingTests
{
    private readonly Mock<IScreenCapture> _screenCaptureMock = new();
    private readonly Mock<ICursorCapture> _cursorCaptureMock = new();
    private readonly Mock<IClipboardService> _clipboardMock = new();
    private readonly Mock<IDesktopService> _desktopMock = new();
    private readonly Mock<IConfigService> _configMock = new();
    private readonly Mock<ILogger> _loggerMock = new();

    private FullscreenCaptureService CreateService(QapptiaConfig config)
    {
        _configMock.Setup(c => c.Current).Returns(config);
        return new FullscreenCaptureService(
            _screenCaptureMock.Object,
            _cursorCaptureMock.Object,
            _clipboardMock.Object,
            _desktopMock.Object,
            _configMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void SubfolderHourFormatsCorrectlyAsHHhForMidnightHourZero()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "QapptiaTest_" + Guid.NewGuid().ToString("N"));
        var config = new QapptiaConfig
        {
            SavePath = tempDir,
            SubfolderMonth = true,
            SubfolderDay = true,
            SubfolderHour = true
        };
        var service = CreateService(config);
        var testTime = new DateTime(2026, 9, 21, 0, 15, 30);

        var path = service.BuildFilePath(testTime);

        var dir = Path.GetDirectoryName(path)!;
        Path.GetFileName(dir).Should().Be("00h");
        dir.Should().EndWith(Path.Combine("2026-09", "2026-09-21", "00h"));
    }

    [Fact]
    public void SubfolderHourFormatsCorrectlyAsHHhForMorningHourEight()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "QapptiaTest_" + Guid.NewGuid().ToString("N"));
        var config = new QapptiaConfig
        {
            SavePath = tempDir,
            SubfolderMonth = true,
            SubfolderDay = true,
            SubfolderHour = true
        };
        var service = CreateService(config);
        var testTime = new DateTime(2026, 9, 21, 8, 45, 0);

        var path = service.BuildFilePath(testTime);

        var dir = Path.GetDirectoryName(path)!;
        Path.GetFileName(dir).Should().Be("08h");
        dir.Should().EndWith(Path.Combine("2026-09", "2026-09-21", "08h"));
    }

    [Fact]
    public void SubfolderHourFormatsCorrectlyAsHHhForNightHourTwentyTwo()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "QapptiaTest_" + Guid.NewGuid().ToString("N"));
        var config = new QapptiaConfig
        {
            SavePath = tempDir,
            SubfolderMonth = true,
            SubfolderDay = true,
            SubfolderHour = true
        };
        var service = CreateService(config);
        var testTime = new DateTime(2026, 9, 21, 22, 10, 5);

        var path = service.BuildFilePath(testTime);

        var dir = Path.GetDirectoryName(path)!;
        Path.GetFileName(dir).Should().Be("22h");
        dir.Should().EndWith(Path.Combine("2026-09", "2026-09-21", "22h"));
    }

    [Fact]
    public void SubfolderHourDisabledDoesNotIncludeHourFolder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "QapptiaTest_" + Guid.NewGuid().ToString("N"));
        var config = new QapptiaConfig
        {
            SavePath = tempDir,
            SubfolderMonth = true,
            SubfolderDay = true,
            SubfolderHour = false
        };
        var service = CreateService(config);
        var testTime = new DateTime(2026, 9, 21, 14, 0, 0);

        var path = service.BuildFilePath(testTime);

        var dir = Path.GetDirectoryName(path)!;
        Path.GetFileName(dir).Should().Be("2026-09-21");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FilenameFormatFallbackWhenBlankUsesDefaultFormat(string? blankFormat)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "QapptiaTest_" + Guid.NewGuid().ToString("N"));
        var config = new QapptiaConfig
        {
            SavePath = tempDir,
            FilenameFormat = blankFormat!,
            SubfolderMonth = false,
            SubfolderDay = false,
            SubfolderHour = false
        };
        var service = CreateService(config);
        var testTime = new DateTime(2026, 9, 21, 11, 8, 30);

        var path = service.BuildFilePath(testTime);

        Path.GetFileName(path).Should().Be("Qapptia_20260921_110830.png");
    }

    [Fact]
    public void FilenameCollisionResolutionAppendsSequentialNumericSuffixes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "QapptiaTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var config = new QapptiaConfig
            {
                SavePath = tempDir,
                FilenameFormat = "CustomTest",
                SubfolderMonth = false,
                SubfolderDay = false,
                SubfolderHour = false
            };
            var service = CreateService(config);

            // 1. Primer archivo: no existe colisión -> CustomTest.png
            var path1 = service.BuildFilePath();
            Path.GetFileName(path1).Should().Be("CustomTest.png");
            File.WriteAllText(path1, "test content");

            // 2. Segunda captura con mismo nombre -> colisión 1 -> CustomTest_1.png
            var path2 = service.BuildFilePath();
            Path.GetFileName(path2).Should().Be("CustomTest_1.png");
            File.WriteAllText(path2, "test content 2");

            // 3. Tercera captura con mismo nombre -> colisión 2 -> CustomTest_2.png
            var path3 = service.BuildFilePath();
            Path.GetFileName(path3).Should().Be("CustomTest_2.png");
            File.WriteAllText(path3, "test content 3");

            // 4. Cuarta captura con mismo nombre -> colisión 3 -> CustomTest_3.png
            var path4 = service.BuildFilePath();
            Path.GetFileName(path4).Should().Be("CustomTest_3.png");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
