using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using icongen;

class Program
{
    private const string FontRelativePath = @"Fonts\AtkinsonHyperlegible-Bold.ttf";
    private const string EditorAssetsRelativeDir = @"..\..\src\Qapptia.App.Editor\Assets";
    private const string CaptureAssetsRelativeDir = @"..\..\src\Qapptia.App.Capture\Assets";
    private const string AppIconFileName = "app_icon.ico";
    private const string TrayIconFileName = "tray_icon.ico";

    static void Main()
    {
        var editorOutDir = Path.GetFullPath(EditorAssetsRelativeDir);
        var captureOutDir = Path.GetFullPath(CaptureAssetsRelativeDir);
        Directory.CreateDirectory(editorOutDir);
        Directory.CreateDirectory(captureOutDir);

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

        var fontPath = Path.GetFullPath(FontRelativePath);
        using var typeface = SKTypeface.FromFile(fontPath) ?? SKTypeface.Default;
        using var textPaint = new SKPaint
        {
            Color = IconMetrics.TextColor,
            IsAntialias = true,
            Typeface = typeface,
            TextSize = IconMetrics.GetFontSize(targetSize),
            TextAlign = SKTextAlign.Center
        };

        var fontMetrics = textPaint.FontMetrics;
        var textY = (targetSize / 2f) - (fontMetrics.Ascent + fontMetrics.Descent) / 2f;
        canvas.DrawText(IconMetrics.IconText, targetSize / 2f, textY, textPaint);

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

        var radius = IconMetrics.GetRadius(size);
        
        using var bgPaint = new SKPaint
        {
            Color = IconMetrics.BackgroundColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        
        var rect = new SKRect(0, 0, size, size);
        canvas.DrawRoundRect(rect, radius, radius, bgPaint);

        var fontPath = Path.GetFullPath(FontRelativePath);
        using var typeface = SKTypeface.FromFile(fontPath) ?? SKTypeface.Default;
        using var textPaint = new SKPaint
        {
            Color = IconMetrics.TextColor,
            IsAntialias = true,
            Typeface = typeface,
            TextSize = (int)(size * 0.85),
            TextAlign = SKTextAlign.Center
        };

        var fontMetrics = textPaint.FontMetrics;
        var textY = (size / 2f) - (fontMetrics.Ascent + fontMetrics.Descent) / 2f;
        canvas.DrawText(IconMetrics.IconText, size / 2f, textY, textPaint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
