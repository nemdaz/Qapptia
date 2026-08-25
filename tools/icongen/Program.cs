using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using icongen;

class Program
{

    private const string EditorAssetsRelativeDir = @"..\..\src\Qapptia.App.Editor\Assets";
    private const string CaptureAssetsRelativeDir = @"..\..\src\Qapptia.App.Capture\Assets";
    private const string ConfigAssetsRelativeDir = @"..\..\src\Qapptia.App.Config\Assets";
    private const string AppIconFileName = "app_icon.ico";
    private const string ConfigIconFileName = "config_icon.ico";
    private const string TrayIconFileName = "tray_icon.ico";

    static void Main()
    {
        var editorOutDir = Path.GetFullPath(EditorAssetsRelativeDir);
        var captureOutDir = Path.GetFullPath(CaptureAssetsRelativeDir);
        var configOutDir = Path.GetFullPath(ConfigAssetsRelativeDir);
        Directory.CreateDirectory(editorOutDir);
        Directory.CreateDirectory(captureOutDir);
        Directory.CreateDirectory(configOutDir);

        Console.WriteLine("Generating App Window Icon...");
        
        // Generar Master Icon (256x256) idéntico al legacy
        byte[] masterIconBytes = GenerateAppIcon(IconMetrics.AppIconMasterSize);
        using var masterBitmap = SKBitmap.Decode(masterIconBytes);


        using var collection = new ImageMagick.MagickImageCollection();
        using var masterMagickImage = new ImageMagick.MagickImage(masterIconBytes);
        
        foreach (var size in IconMetrics.AppWindowIconSizes)
        {
            var resized = new ImageMagick.MagickImage(masterMagickImage);
            resized.Resize((uint)size, (uint)size);
            resized.Format = ImageMagick.MagickFormat.Ico;
            collection.Add(resized);
        }
        collection.Write(Path.Combine(editorOutDir, AppIconFileName), ImageMagick.MagickFormat.Ico);
        
        Console.WriteLine("Generating Config App Icon...");
        byte[] configIconBytes = GenerateConfigAppIcon(IconMetrics.AppIconMasterSize);
        using var configMagickImage = new ImageMagick.MagickImage(configIconBytes);
        using var configCollection = new ImageMagick.MagickImageCollection();
        foreach (var size in IconMetrics.AppWindowIconSizes)
        {
            var resized = new ImageMagick.MagickImage(configMagickImage);
            resized.Resize((uint)size, (uint)size);
            resized.Format = ImageMagick.MagickFormat.Ico;
            configCollection.Add(resized);
        }
        configCollection.Write(Path.Combine(configOutDir, ConfigIconFileName), ImageMagick.MagickFormat.Ico);

        Console.WriteLine("Generating App Tray Icon...");
        // Generar Master Icon para Tray (recortado)
        byte[] trayMasterBytes = GenerateTrayIcon();
        using var trayMagickImage = new ImageMagick.MagickImage(trayMasterBytes);
        using var trayCollection = new ImageMagick.MagickImageCollection();
        
        // El Tray de Windows usa típicamente 16, 20, 24 y 32
        int[] traySizes = { 32, 24, 20, 16 };
        foreach (var size in traySizes)
        {
            var resized = new ImageMagick.MagickImage(trayMagickImage);
            resized.Resize((uint)size, (uint)size);
            resized.Format = ImageMagick.MagickFormat.Ico;
            trayCollection.Add(resized);
        }
        
        trayCollection.Write(Path.Combine(captureOutDir, TrayIconFileName), ImageMagick.MagickFormat.Ico);

        Console.WriteLine("Done.");
    }

    static byte[] GenerateAppIcon(int targetSize)
    {
        using var surface = SKSurface.Create(new SKImageInfo(targetSize, targetSize, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var pad = IconMetrics.GetPadding(targetSize);
        var radius = IconMetrics.GetRadius(targetSize);
        var strokeW = IconMetrics.GetBorderWidth(targetSize);
        
        using var bgPaint = new SKPaint
        {
            Color = IconMetrics.BackgroundColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        
        using var outlinePaint = new SKPaint
        {
            Color = IconMetrics.OutlineColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeW
        };
        
        var rect = new SKRect(pad, pad, targetSize - pad, targetSize - pad);
        canvas.DrawRoundRect(rect, radius, radius, bgPaint);
        canvas.DrawRoundRect(rect, radius, radius, outlinePaint);

        IconMetrics.DrawCustomQ(canvas, targetSize / 2f, targetSize / 2f, IconMetrics.GetQSize(targetSize), IconMetrics.TextColor);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    static byte[] GenerateTrayIcon()
    {
        int size = IconMetrics.AppIconMasterSize;
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // Para el Tray Icon, no dibujamos un fondo. 
        // La "Q" se dibuja de color blanco (TextColor) y se le añade un contorno 
        // del color corporativo (BackgroundColor) para mantener la identidad sin el fondo sólido.
        IconMetrics.DrawCustomQ(canvas, size / 2f, size / 2f, size, IconMetrics.TextColor, size * 0.16f, IconMetrics.BackgroundColor);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    static byte[] GenerateConfigAppIcon(int targetSize)
    {
        // 1. Obtener la imagen base (la "Q" del Editor)
        byte[] baseImageBytes = GenerateAppIcon(targetSize);
        using var baseBitmap = SKBitmap.Decode(baseImageBytes);

        using var surface = SKSurface.Create(new SKImageInfo(targetSize, targetSize, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // 2. Dibujar la imagen base
        canvas.DrawBitmap(baseBitmap, 0, 0);

        // 3. SVG Path de una tuerca (Material Icons - settings)
        var gearSvg = "M19.14,12.94c0.04-0.3,0.06-0.61,0.06-0.94c0-0.32-0.02-0.64-0.06-0.94l2.03-1.58c0.18-0.14,0.23-0.41,0.12-0.61 l-1.92-3.32c-0.12-0.22-0.37-0.29-0.59-0.22l-2.39,0.96c-0.5-0.38-1.03-0.7-1.62-0.94L14.4,2.81c-0.04-0.24-0.24-0.41-0.48-0.41 h-3.84c-0.24,0-0.43,0.17-0.47,0.41L9.25,5.35C8.66,5.59,8.12,5.92,7.63,6.29L5.24,5.33c-0.22-0.08-0.47,0-0.59,0.22L2.73,8.87 C2.62,9.08,2.66,9.34,2.86,9.48l2.03,1.58C4.84,11.36,4.8,11.69,4.8,12s0.02,0.64,0.06,0.94l-2.03,1.58 c-0.18,0.14-0.23,0.41-0.12,0.61l1.92,3.32c0.12,0.22,0.37,0.29,0.59,0.22l2.39-0.96c0.5,0.38,1.03,0.7,1.62,0.94l0.36,2.54 c0.05,0.24,0.24,0.41,0.48,0.41h3.84c0.24,0,0.43-0.17,0.47-0.41l0.36-2.54c0.59-0.24,1.13-0.56,1.62-0.94l2.39,0.96 c0.22,0.08,0.47,0,0.59-0.22l1.92-3.32c0.12-0.22,0.07-0.49-0.12-0.61L19.14,12.94z M12,15.6c-1.98,0-3.6-1.62-3.6-3.6 s1.62-3.6,3.6-3.6s3.6,1.62,3.6,3.6S13.98,15.6,12,15.6z";
        using var gearPath = SKPath.ParseSvgPathData(gearSvg);

        // 4. Calcular transformaciones (escala y traslación hacia arriba a la derecha)
        // El path original tiene viewBox 24x24.
        float gearSize = targetSize * 0.53f; // Ampliado un ~10% extra (0.48 -> 0.53)
        float scale = gearSize / 24f;
        
        float strokeWidth = targetSize * 0.06f; // (0.04 + 50% extra)
        float padding = IconMetrics.GetPadding(targetSize);
        // Posicionar en la esquina superior derecha, con un margen extra para que no se recorte el borde
        float tx = targetSize - padding - gearSize - (strokeWidth / 2f);
        float ty = padding + (strokeWidth / 2f);

        var matrix = new SKMatrix
        {
            ScaleX = scale,
            ScaleY = scale,
            TransX = tx,
            TransY = ty,
            Persp2 = 1
        };

        gearPath.Transform(matrix);

        // 5. Dibujar el contorno oscuro de la tuerca (hace de máscara sobre la Q y da contraste)
        using var gearOutline = new SKPaint
        {
            Color = IconMetrics.BackgroundColor, // Fondo oscuro corporativo
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };

        // 6. Dibujar la tuerca clara (Acento brillante)
        using var gearFill = new SKPaint
        {
            Color = IconMetrics.OutlineColor, // Blanco corporativo
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        // Pintamos el halo/contorno por detrás para ocultar la "Q" que está debajo
        canvas.DrawPath(gearPath, gearOutline);
        
        // Pintamos la tuerca gris
        canvas.DrawPath(gearPath, gearFill);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
