using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Qapptia.Core;
using Qapptia.Core.Services;
using Xunit;

namespace Qapptia.Core.Tests;

public sealed class ImageMetadataTests : IDisposable
{
    private readonly string _testDir;

    public ImageMetadataTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Qapptia_MetaTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
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
    public async Task EnsureImageMetadataInjectsMediaIdAndMediaType()
    {
        var filePath = Path.Combine(_testDir, "sample.png");
        await File.WriteAllBytesAsync(filePath, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }); // PNG header

        var (mediaId, mediaType) = await ImageMetadataService.EnsureImageMetadataAsync(filePath);

        mediaId.Should().NotBeNullOrWhiteSpace();
        mediaType.Should().Be("image/png");

        // Leer sincrónicamente
        var (readId, readType) = ImageMetadataService.GetImageMetadata(filePath);
        readId.Should().Be(mediaId);
        readType.Should().Be("image/png");

        // Leer asincrónicamente
        var (asyncId, asyncType) = await ImageMetadataService.GetImageMetadataAsync(filePath);
        asyncId.Should().Be(mediaId);
        asyncType.Should().Be("image/png");
    }

    [Fact]
    public async Task EnsureImageMetadataDetectsJpegMimeType()
    {
        var filePath = Path.Combine(_testDir, "photo.jpg");
        await File.WriteAllBytesAsync(filePath, new byte[] { 0xFF, 0xD8, 0xFF });

        var (mediaId, mediaType) = await ImageMetadataService.EnsureImageMetadataAsync(filePath);

        mediaId.Should().NotBeNullOrWhiteSpace();
        mediaType.Should().Be("image/jpeg");

        var (readId, readType) = ImageMetadataService.GetImageMetadata(filePath);
        readId.Should().Be(mediaId);
        readType.Should().Be("image/jpeg");
    }

    [Fact]
    public void ResolveMediaTypeResolvesExpectedMimeTypes()
    {
        Constants.ResolveMediaType("file.png").Should().Be("image/png");
        Constants.ResolveMediaType("file.PNG").Should().Be("image/png");
        Constants.ResolveMediaType("file.jpg").Should().Be("image/jpeg");
        Constants.ResolveMediaType("file.jpeg").Should().Be("image/jpeg");
        Constants.ResolveMediaType("file.unknown").Should().Be("image/png");
    }

    [Fact]
    public async Task ImageBurnServiceCreatesBackupWithMediaId()
    {
        var filePath = Path.Combine(_testDir, "capture_burn.png");
        await File.WriteAllBytesAsync(filePath, new byte[] { 1, 2, 3, 4 });

        string testMediaId = Guid.NewGuid().ToString();
        string backupPath = await ImageBurnService.CreateCompressedBackupAsync(filePath, testMediaId);

        File.Exists(backupPath).Should().BeTrue();
        backupPath.Should().Contain(testMediaId);
        backupPath.Should().Contain(Constants.DrawingExtension);
    }
}
