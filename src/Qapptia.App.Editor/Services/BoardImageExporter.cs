using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Qapptia.App.Editor.ViewModels.Shapes;

namespace Qapptia.App.Editor.Services;

/// <summary>
/// Servicio de renderizado off-screen para exportar el contenido del tablero (guardar y copiar)
/// con anotaciones quemadas y recorte aplicado de forma nativa sin tocar el canvas de la UI.
/// </summary>
public static class BoardImageExporter
{
    public static (int destWidth, int destHeight, double offsetX, double offsetY) CalculateExportBounds(
        double imageWidth,
        double imageHeight,
        Rect? activeCropRect)
    {
        int destWidth = Math.Max(1, (int)imageWidth);
        int destHeight = Math.Max(1, (int)imageHeight);
        double offsetX = 0;
        double offsetY = 0;

        if (activeCropRect.HasValue)
        {
            var crop = activeCropRect.Value;
            destWidth = Math.Max(1, (int)crop.Width);
            destHeight = Math.Max(1, (int)crop.Height);
            offsetX = -crop.X;
            offsetY = -crop.Y;
        }

        return (destWidth, destHeight, offsetX, offsetY);
    }

    public static RenderTargetBitmap RenderBurnedBitmap(
        Bitmap backgroundImage,
        IReadOnlyList<VectorShape> shapes,
        Rect? activeCropRect,
        double imageWidth,
        double imageHeight)
    {
        ArgumentNullException.ThrowIfNull(backgroundImage);
        ArgumentNullException.ThrowIfNull(shapes);

        var (destWidth, destHeight, offsetX, offsetY) = CalculateExportBounds(imageWidth, imageHeight, activeCropRect);

        var rtb = new RenderTargetBitmap(new PixelSize(destWidth, destHeight), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        {
            if (offsetX != 0 || offsetY != 0)
            {
                using (ctx.PushTransform(Matrix.CreateTranslation(offsetX, offsetY)))
                {
                    DrawContent(ctx, backgroundImage, shapes, imageWidth, imageHeight);
                }
            }
            else
            {
                DrawContent(ctx, backgroundImage, shapes, imageWidth, imageHeight);
            }
        }

        return rtb;
    }

    /// <summary>
    /// Exporta la imagen quemada con alta eficiencia, extrayendo píxeles crudos BGRA directamente
    /// del búfer en ~4 ms y generando una compresión PNG rápida (~18 ms) para el portapapeles y disco.
    /// </summary>
    public static (byte[] rawPixels, int width, int height, byte[] pngBytes) ExportBurnedImage(
        Bitmap backgroundImage,
        IReadOnlyList<VectorShape> shapes,
        Rect? activeCropRect,
        double imageWidth,
        double imageHeight)
    {
        using var rtb = RenderBurnedBitmap(backgroundImage, shapes, activeCropRect, imageWidth, imageHeight);

        int width = (int)rtb.PixelSize.Width;
        int height = (int)rtb.PixelSize.Height;
        int stride = width * 4;
        int bufferSize = stride * height;
        byte[] rawPixels = new byte[bufferSize];

        var handle = System.Runtime.InteropServices.GCHandle.Alloc(rawPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            rtb.CopyPixels(new PixelRect(0, 0, width, height), handle.AddrOfPinnedObject(), bufferSize, stride);

            using var ms = new System.IO.MemoryStream();
            using var skBmp = new SkiaSharp.SKBitmap();
            var info = new SkiaSharp.SKImageInfo(width, height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            skBmp.InstallPixels(info, handle.AddrOfPinnedObject(), stride);

            var options = new SkiaSharp.SKPngEncoderOptions(SkiaSharp.SKPngEncoderFilterFlags.None, 1);
            skBmp.PeekPixels().Encode(ms, options);
            byte[] pngBytes = ms.ToArray();

            return (rawPixels, width, height, pngBytes);
        }
        finally
        {
            handle.Free();
        }
    }

    private static void DrawContent(
        DrawingContext ctx,
        Bitmap backgroundImage,
        IReadOnlyList<VectorShape> shapes,
        double imageWidth,
        double imageHeight)
    {
        // 1. Dibujar imagen de fondo base
        var fullRect = new Rect(0, 0, imageWidth, imageHeight);
        ctx.DrawImage(backgroundImage, fullRect);

        // 2. Dibujar formas vectoriales con sombras de quemado puras (sin tocar UI)
        if (shapes.Count > 0)
        {
            ctx.Custom(new ExportSkiaDrawOperation(shapes, fullRect));
        }
    }

    private sealed class ExportSkiaDrawOperation : ICustomDrawOperation
    {
        private readonly IReadOnlyList<VectorShape> _shapes;
        private readonly Rect _bounds;

        public ExportSkiaDrawOperation(IReadOnlyList<VectorShape> shapes, Rect bounds)
        {
            _shapes = shapes;
            _bounds = bounds;
        }

        public Rect Bounds => _bounds;

        public void Dispose() { }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null) return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            foreach (var shape in _shapes)
            {
                bool wasBurning = shape.IsBurning;
                bool wasSelected = shape.IsSelected;
                try
                {
                    shape.IsBurning = true;   // Aplicar sombra de quemado
                    shape.IsSelected = false; // Ocultar bordes de selección y manetas
                    shape.RenderSkia(canvas);
                }
                finally
                {
                    shape.IsBurning = wasBurning;
                    shape.IsSelected = wasSelected;
                }
            }
        }
    }
}
