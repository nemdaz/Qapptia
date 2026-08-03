using System.Collections.Concurrent;
using SkiaSharp;

namespace Qapptia.Core.Assets;

/// <summary>
/// Genera iconos proceduralmente con SkiaSharp (sin assets embebidos).
/// App icon: rectángulo redondeado dark (#1f2933) con borde (#f5f7fa) y letra "Q" blanca.
/// Cache por tamaño para evitar re-renderear.
/// </summary>
public static class IconProvider
{
    public const int AppIconMasterSize = 256;
    public static readonly int[] WindowIconSizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

    private static readonly SKColor BgColor = new(0x1f, 0x29, 0x33, 0xff);
    private static readonly SKColor BorderColor = new(0xf5, 0xf7, 0xfa, 0xff);
    private static readonly SKColor TextColor = SKColors.White;

    private static readonly ConcurrentDictionary<int, SKBitmap> _appIconCache = new();

    public static SKBitmap GetAppIcon(int size)
    {
        if (size <= 0) size = 64;
        return _appIconCache.GetOrAdd(size, RenderAppIcon);
    }

    public static byte[] GetAppIconPng(int size)
    {
        using var bmp = GetAppIcon(size);
        using var ms = new System.IO.MemoryStream();
        bmp.Encode(ms, SKEncodedImageFormat.Png, 100);
        return ms.ToArray();
    }

    private static SKBitmap RenderAppIcon(int size)
    {
        var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bmp = new SKBitmap(info);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Transparent);

        var pad = Math.Max(1, size / 16);
        var radius = Math.Max(4, size / 5);
        var strokeWidth = Math.Max(1f, size / 18f);

        var rect = new SKRect(pad, pad, size - pad, size - pad);

        using var fillPaint = new SKPaint
        {
            Color = BgColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawRoundRect(rect, radius, radius, fillPaint);

        using var borderPaint = new SKPaint
        {
            Color = BorderColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
        };
        canvas.DrawRoundRect(rect, radius, radius, borderPaint);

        var fontPx = (int)(size * 0.62);
        using var typeface = SKTypeface.FromFamilyName(
            "Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            ?? SKTypeface.Default;
        using var font = new SKFont(typeface, fontPx)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Subpixel = true,
        };

        var text = "Q";
        var textWidth = font.MeasureText(text);
        var metrics = font.Metrics;
        var textHeight = metrics.Descent - metrics.Ascent;
        var textX = (size - textWidth) / 2f;
        var textY = (size - textHeight) / 2f - metrics.Ascent - Math.Max(0, size / 60f);

        using var textPaint = new SKPaint
        {
            Color = TextColor,
            IsAntialias = true,
        };
        canvas.DrawText(text, textX, textY, font, textPaint);

        return bmp;
    }

    public static void ClearCache()
    {
        foreach (var kv in _appIconCache)
        {
            try { kv.Value.Dispose(); } catch { }
        }
        _appIconCache.Clear();
    }
}
