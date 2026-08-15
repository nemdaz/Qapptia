using System;
using System.Collections.Generic;
using Avalonia;
using SkiaSharp;
using Qapptia.Editor.Core;

namespace Qapptia.Editor.Models;

public class TextShape : VectorShape
{
    private static readonly string[] s_lineSeparators = { "\r\n", "\r", "\n" };

    private static readonly (float dx, float dy)[] s_contourOffsets = new[]
    {
        (-1.0f,  0.0f),
        ( 1.0f,  0.0f),
        ( 0.0f, -1.0f),
        ( 0.0f,  1.0f),
        (-0.707f, -0.707f),
        ( 0.707f, -0.707f),
        (-0.707f,  0.707f),
        ( 0.707f,  0.707f)
    };

    public string Text { get; set; } = string.Empty;
    public int TextSize { get; set; } = 24;

    public override void RenderSkia(SKCanvas canvas)
    {
        if (string.IsNullOrWhiteSpace(Text)) return;

        using var font = new SKFont(Constants.TextToolTypeface, TextSize);
        font.Subpixel = true;
        font.Edging = SKFontEdging.Antialias;
        font.GetFontMetrics(out var metrics);

        using var paintLight = new SKPaint
        {
            Color = Constants.TextToolLightContourSKColor,
            IsAntialias = true
        };

        using var paintDark = new SKPaint
        {
            Color = Constants.TextToolDarkContourSKColor,
            IsAntialias = true
        };

        using var paintMain = new SKPaint
        {
            Color = new SKColor(Color.R, Color.G, Color.B, Color.A),
            IsAntialias = true
        };

        var lines = Text.Split(s_lineSeparators, StringSplitOptions.None);
        
        // Coordenadas base
        float baseOffsetX = (float)(Start.X + Constants.TextToolOffset);
        float startY = (float)(Start.Y + Constants.TextToolOffset - metrics.Ascent);

        // 1. Contorno claro (8 direcciones para halo suave 360° en fondos oscuros)
        foreach (var (dx, dy) in s_contourOffsets)
        {
            float y = startY + dy;
            foreach (var line in lines)
            {
                canvas.DrawText(line, baseOffsetX + dx, y, SKTextAlign.Left, font, paintLight);
                y += font.Spacing;
            }
        }

        // 2. Contorno oscuro (8 direcciones para nitidez suave 360° en fondos claros)
        foreach (var (dx, dy) in s_contourOffsets)
        {
            float y = startY + dy;
            foreach (var line in lines)
            {
                canvas.DrawText(line, baseOffsetX + dx, y, SKTextAlign.Left, font, paintDark);
                y += font.Spacing;
            }
        }

        // 3. Capa principal de texto
        float mainY = startY;
        foreach (var line in lines)
        {
            canvas.DrawText(line, baseOffsetX, mainY, SKTextAlign.Left, font, paintMain);
            mainY += font.Spacing;
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
