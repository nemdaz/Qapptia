using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Qapptia.App.Editor.ViewModels.Shapes;
using Qapptia.Editor.Models;

namespace Qapptia.App.Editor.Services;

/// <summary>
/// Servicio de muestreo de color de fondo y composición visual sobre el lienzo.
/// Extrae con precisión el color predominante circundante para el borrador inteligente.
/// </summary>
public static class ImageColorSampler
{
    public static Color SampleColor(
        Bitmap? backgroundBitmap,
        IReadOnlyList<VectorShape>? shapes,
        Point canvasPoint,
        double imageWidth,
        double imageHeight)
    {
        // 1. Si el punto cae dentro de una figura de fondo sólido previo, reutilizar su color para continuidad
        if (shapes != null)
        {
            for (int i = shapes.Count - 1; i >= 0; i--)
            {
                var shape = shapes[i];
                if (shape.HasSolidBackground && shape.HitTest(canvasPoint) != HandleType.None)
                {
                    return shape.Color;
                }
            }
        }

        // 2. Si no hay imagen de fondo válida, devolver blanco por defecto
        if (backgroundBitmap == null || imageWidth <= 0 || imageHeight <= 0)
        {
            return Colors.White;
        }

        int imgW = backgroundBitmap.PixelSize.Width;
        int imgH = backgroundBitmap.PixelSize.Height;
        if (imgW <= 0 || imgH <= 0) return Colors.White;

        // 3. Mapear de coordenadas lógicas del lienzo a píxeles físicos del bitmap base
        double scaleX = imgW / Math.Max(1.0, imageWidth);
        double scaleY = imgH / Math.Max(1.0, imageHeight);

        int px = (int)Math.Clamp(Math.Round(canvasPoint.X * scaleX), 0, imgW - 1);
        int py = (int)Math.Clamp(Math.Round(canvasPoint.Y * scaleY), 0, imgH - 1);

        // 4. Muestreo de ventana 3x3 centrada en (px, py)
        int startX = Math.Max(0, px - 1);
        int startY = Math.Max(0, py - 1);
        int endX = Math.Min(imgW - 1, px + 1);
        int endY = Math.Min(imgH - 1, py + 1);

        int width = endX - startX + 1;
        int height = endY - startY + 1;
        int stride = width * 4;
        int bufferSize = stride * height;
        byte[] buffer = new byte[bufferSize];

        try
        {
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                backgroundBitmap.CopyPixels(
                    new PixelRect(startX, startY, width, height),
                    handle.AddrOfPinnedObject(),
                    bufferSize,
                    stride);
            }
            finally
            {
                handle.Free();
            }

            bool isBgra = backgroundBitmap.Format == null || backgroundBitmap.Format == PixelFormats.Bgra8888;

            var rList = new List<byte>(width * height);
            var gList = new List<byte>(width * height);
            var bList = new List<byte>(width * height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int offset = (y * stride) + (x * 4);
                    byte b = isBgra ? buffer[offset] : buffer[offset + 2];
                    byte g = buffer[offset + 1];
                    byte r = isBgra ? buffer[offset + 2] : buffer[offset];
                    rList.Add(r);
                    gList.Add(g);
                    bList.Add(b);
                }
            }

            // Aplicar mediana en cada canal RGB para eliminar ruido o bordes oscuros de texto
            rList.Sort();
            gList.Sort();
            bList.Sort();

            int medianIdx = rList.Count / 2;
            return Color.FromRgb(rList[medianIdx], gList[medianIdx], bList[medianIdx]);
        }
        catch
        {
            return Colors.White;
        }
    }
}
