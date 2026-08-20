using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using icongen;

class Program
{
    static void Main()
    {
        var editorOutDir = Path.GetFullPath(@"..\..\src\Qapptia.App.Editor\Assets");
        var captureOutDir = Path.GetFullPath(@"..\..\src\Qapptia.App.Capture\Assets");
        Directory.CreateDirectory(editorOutDir);
        Directory.CreateDirectory(captureOutDir);

        Console.WriteLine("Generating App Window Icon...");
        
        // Generar Master Icon (256x256) idéntico al legacy
        byte[] masterIconBytes = GenerateIconPng(IconMetrics.AppIconMasterSize, false);
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
        collection.Write(Path.Combine(editorOutDir, "app_icon.ico"), ImageMagick.MagickFormat.Ico);
        
        Console.WriteLine("Generating App Tray Icon...");
        using var trayCollection = new ImageMagick.MagickImageCollection();
        var trayResized = new ImageMagick.MagickImage(masterMagickImage);
        
        trayResized.Resize(32u, 32u);
        trayResized.Format = ImageMagick.MagickFormat.Ico;
        trayCollection.Add(trayResized);
        trayCollection.Write(Path.Combine(captureOutDir, "tray_icon.ico"), ImageMagick.MagickFormat.Ico);

        Console.WriteLine("Done.");
    }

    static byte[] GenerateIconPng(int targetSize, bool isTray)
    {
        int drawSize = isTray ? IconMetrics.AppIconMasterSize : targetSize;
        
        using var surface = SKSurface.Create(new SKImageInfo(drawSize, drawSize, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var pad = IconMetrics.GetPadding(drawSize);
        var radius = IconMetrics.GetRadius(drawSize);
        var strokeW = IconMetrics.GetBorderWidth(drawSize);
        
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
        
        var rect = new SKRect(pad, pad, drawSize - pad, drawSize - pad);
        canvas.DrawRoundRect(rect, radius, radius, bgPaint);
        canvas.DrawRoundRect(rect, radius, radius, outlinePaint);

        var fontSize = IconMetrics.GetFontSize(drawSize);
        // Cargar fuente desde ruta local
        var fontPath = Path.GetFullPath(@"Fonts\AtkinsonHyperlegible-Bold.ttf");
        using var typeface = SKTypeface.FromFile(fontPath) ?? SKTypeface.Default;
        using var textPaint = new SKPaint
        {
            Color = IconMetrics.TextColor,
            IsAntialias = true,
            Typeface = typeface,
            TextSize = fontSize,
            TextAlign = SKTextAlign.Center
        };

        // Medir texto para centrado vertical
        var fontMetrics = textPaint.FontMetrics;
        var textY = (drawSize / 2f) - (fontMetrics.Ascent + fontMetrics.Descent) / 2f;
        
        canvas.DrawText(IconMetrics.IconText, drawSize / 2f, textY, textPaint);

        using var image = surface.Snapshot();
        
        if (isTray)
        {
            // Recortar margen y escalar al tamaño objetivo
            var inset = IconMetrics.GetTrayIconInset();
            var croppedRect = SKRectI.Create(inset, inset, drawSize - inset * 2, drawSize - inset * 2);
            using var croppedImage = image.Subset(croppedRect);
            
            var scaledInfo = new SKImageInfo(targetSize, targetSize, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var scaledImage = SKImage.Create(scaledInfo);
            croppedImage.ScalePixels(scaledImage.PeekPixels(), SKFilterQuality.High);
            using var data = scaledImage.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        else
        {
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
    }
}
