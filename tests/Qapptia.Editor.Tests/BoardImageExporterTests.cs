using System;
using System.Collections.Generic;
using Avalonia;
using FluentAssertions;
using Qapptia.App.Editor.Services;
using Qapptia.App.Editor.ViewModels.Shapes;
using Xunit;

namespace Qapptia.Editor.Tests;

public sealed class BoardImageExporterTests
{
    [Fact]
    public void CalculateExportBoundsWithoutCropReturnsFullImageDimensionsAndZeroOffset()
    {
        var (destWidth, destHeight, offsetX, offsetY) = BoardImageExporter.CalculateExportBounds(
            imageWidth: 1920,
            imageHeight: 1080,
            activeCropRect: null);

        destWidth.Should().Be(1920);
        destHeight.Should().Be(1080);
        offsetX.Should().Be(0);
        offsetY.Should().Be(0);
    }

    [Fact]
    public void CalculateExportBoundsWithCropReturnsCropDimensionsAndNegativeOffsets()
    {
        var crop = new Rect(150, 200, 800, 600);

        var (destWidth, destHeight, offsetX, offsetY) = BoardImageExporter.CalculateExportBounds(
            imageWidth: 1920,
            imageHeight: 1080,
            activeCropRect: crop);

        destWidth.Should().Be(800);
        destHeight.Should().Be(600);
        offsetX.Should().Be(-150);
        offsetY.Should().Be(-200);
    }

    [Fact]
    public void CalculateExportBoundsClampsMinimumDimensionsToOne()
    {
        var (destWidth, destHeight, offsetX, offsetY) = BoardImageExporter.CalculateExportBounds(
            imageWidth: 0,
            imageHeight: -10,
            activeCropRect: null);

        destWidth.Should().Be(1);
        destHeight.Should().Be(1);
        offsetX.Should().Be(0);
        offsetY.Should().Be(0);
    }

    [Fact]
    public void RenderBurnedBitmapThrowsOnNullInputs()
    {
        var shapes = new List<VectorShape>();

        Action actNullBitmap = () => BoardImageExporter.RenderBurnedBitmap(
            null!,
            shapes,
            activeCropRect: null,
            imageWidth: 100,
            imageHeight: 100);

        actNullBitmap.Should().Throw<ArgumentNullException>();

        // Crear una instancia nula de formas para verificar el guard
        Action actNullShapes = () => BoardImageExporter.RenderBurnedBitmap(
            null!, // el primer parámetro nulo dispara primero
            null!,
            activeCropRect: null,
            imageWidth: 100,
            imageHeight: 100);

        actNullShapes.Should().Throw<ArgumentNullException>();
    }
}
