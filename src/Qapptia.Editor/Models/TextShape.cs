using System;
using System.Collections.Generic;
using Avalonia;
using SkiaSharp;
using Qapptia.Editor.Core;

namespace Qapptia.Editor.Models;

public class TextShape : VectorShape
{
    private static readonly string[] s_lineSeparators = { "\r\n", "\r", "\n" };

    public string Text { get; set; } = string.Empty;
    public int TextSize { get; set; } = 24;

    public override void RenderSkia(SKCanvas canvas)
    {
        if (string.IsNullOrWhiteSpace(Text)) return;

        using var font = new SKFont(Constants.TextToolTypeface, TextSize);
        font.Subpixel = true;
        font.Edging = SKFontEdging.SubpixelAntialias;
        font.GetFontMetrics(out var metrics);

        using var paint = new SKPaint
        {
            Color = new SKColor(Color.R, Color.G, Color.B, Color.A),
            IsAntialias = true,
            ImageFilter = IsBurning ? Constants.CreateBurnedShadow() : Constants.CreateEditingShadow()
        };

        var lines = Text.Split(s_lineSeparators, StringSplitOptions.None);
        
        // Desfase calibrado unificado
        float offsetX = (float)(Start.X + Constants.TextToolOffset);
        float y = (float)(Start.Y + Constants.TextToolOffset - metrics.Ascent);
        
        foreach (var line in lines)
        {
            canvas.DrawText(line, offsetX, y, SKTextAlign.Left, font, paint);
            y += font.Spacing;
        }
    }

    public override HandleType HitTest(Point point)
    {
        if (string.IsNullOrWhiteSpace(Text)) return HandleType.None;

        using var font = new SKFont(Constants.TextToolTypeface, TextSize);
        font.GetFontMetrics(out var metrics);

        var lines = Text.Split(s_lineSeparators, StringSplitOptions.None);
        float maxWidth = 0;

        foreach (var line in lines)
        {
            float width = font.MeasureText(line);
            if (width > maxWidth) maxWidth = width;
        }

        float totalHeight = lines.Length * font.Spacing;

        // Área de selección
        var rect = new Rect(Start.X, Start.Y, maxWidth + 10, totalHeight + 10);
        
        if (rect.Contains(point))
        {
            return HandleType.Body;
        }

        return HandleType.None;
    }
}
