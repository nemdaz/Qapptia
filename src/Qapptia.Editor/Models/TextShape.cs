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

        using var font = new SKFont(SKTypeface.Default, TextSize);
        using var paint = new SKPaint
        {
            Color = new SKColor(Color.R, Color.G, Color.B, Color.A),
            IsAntialias = true,
            ImageFilter = IsBurning ? Constants.CreateBurnedShadow() : Constants.CreateEditingShadow()
        };

        var lines = Text.Split(s_lineSeparators, StringSplitOptions.None);
        float y = (float)Start.Y + TextSize; // Base line approximation
        
        foreach (var line in lines)
        {
            canvas.DrawText(line, (float)Start.X, y, SKTextAlign.Left, font, paint);
            y += TextSize * 1.2f; // line height
        }
    }

    public override HandleType HitTest(Point point)
    {
        if (string.IsNullOrWhiteSpace(Text)) return HandleType.None;

        using var font = new SKFont(SKTypeface.Default, TextSize);

        var lines = Text.Split(s_lineSeparators, StringSplitOptions.None);
        float maxWidth = 0;
        float totalHeight = 0;

        foreach (var line in lines)
        {
            float width = font.MeasureText(line);
            if (width > maxWidth) maxWidth = width;
            totalHeight += TextSize * 1.2f;
        }

        var rect = new Rect(Start.X, Start.Y, maxWidth, totalHeight);
        
        if (rect.Contains(point))
        {
            return HandleType.Body;
        }

        return HandleType.None;
    }
}
