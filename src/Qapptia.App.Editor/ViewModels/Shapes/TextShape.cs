using Avalonia;
using Avalonia.Input;
using Qapptia.Editor.Core;
using Qapptia.Editor.Models;
using Qapptia.Editor.Models.Geometry;
using Qapptia.Editor.Services;
using SkiaSharp;

namespace Qapptia.App.Editor.ViewModels.Shapes;

/// <summary>
/// Renderizado de texto (front). Envuelve una <see cref="TextGeometry"/> (back) y
/// delega en ella el cálculo y la edición; aquí solo se dibuja la capa visual.
/// </summary>
public class TextShape : VectorShape, ITextInputShape
{
    private static readonly (float dx, float dy)[] s_contourOffsets =
    [
        (-1.0f,  0.0f),
        ( 1.0f,  0.0f),
        ( 0.0f, -1.0f),
        ( 0.0f,  1.0f),
        (-0.707f, -0.707f),
        ( 0.707f, -0.707f),
        (-0.707f,  0.707f),
        ( 0.707f,  0.707f)
    ];

    public TextShape() : base(new TextGeometry()) { }
    public TextShape(TextGeometry geometry) : base(geometry) { }

    private TextGeometry TextGeometry => (TextGeometry)Geometry;

    // Passthroughs del contrato ITextInputShape hacia la geometría de texto.
    public string Text { get => TextGeometry.Text; set => TextGeometry.Text = value; }
    public float TextSize { get => TextGeometry.TextSize; set => TextGeometry.TextSize = value; }
    public bool IsEditing { get => TextGeometry.IsEditing; set => TextGeometry.IsEditing = value; }
    public bool IsCaretVisible { get => TextGeometry.IsCaretVisible; set => TextGeometry.IsCaretVisible = value; }
    public int CaretIndex { get => TextGeometry.CaretIndex; set => TextGeometry.CaretIndex = value; }
    public int SelectionStart { get => TextGeometry.SelectionStart; set => TextGeometry.SelectionStart = value; }
    public int SelectionEnd { get => TextGeometry.SelectionEnd; set => TextGeometry.SelectionEnd = value; }
    public Rect TextBounds => TextGeometry.TextBounds;
    public bool IsEmpty => TextGeometry.IsEmpty;
    public bool HasSelection => TextGeometry.HasSelection;
    public string SelectedText => TextGeometry.SelectedText;

    public event System.EventHandler? FocusRequested
    {
        add => TextGeometry.FocusRequested += value;
        remove => TextGeometry.FocusRequested -= value;
    }

    public void RequestFocus() => TextGeometry.RequestFocus();
    public bool IsOnBorder(Point point, float zoom = 1.0f, double baseTolerance = 6.0) => TextGeometry.IsOnBorder(point, zoom, baseTolerance);
    public bool HandleKeyDown(Key key, KeyModifiers modifiers, out bool shouldCommit) => TextGeometry.HandleKeyDown(key, modifiers, out shouldCommit);
    public void InsertText(string text) => TextGeometry.InsertText(text);
    public void DeleteBackward() => TextGeometry.DeleteBackward();

    public int GetCaretIndexFromPoint(Point point) => TextGeometry.GetCaretIndexFromPoint(point);

    public override void RenderSkia(SKCanvas canvas, float zoom = 1.0f)
    {
        var tg = TextGeometry;
        using var font = tg.CreateSKFont();
        float usableWidth = (float)tg.UsableWidth;
        var lines = TextGeometry.GetLayoutLines(tg.Text, font, usableWidth);

        var boxRect = tg.BoundingBox;
        var (baseOffsetX, baseOffsetY, startY) = tg.GetRenderOffsets(font);
        float safeZoom = Math.Max(0.01f, zoom);

        // 1. Marco delimitador y manetas laterales (activos simultáneamente en selección y edición)
        if (IsSelected || IsEditing)
        {
            using var borderPaint = new SKPaint
            {
                Color = IsEditing ? Constants.TextToolBorderSKColor : new SKColor(0, 120, 215, 140),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.0f / safeZoom,
                PathEffect = IsEditing ? null : SKPathEffect.CreateDash([4f / safeZoom, 4f / safeZoom], 0),
                IsAntialias = true
            };
            var skBoxRect = new SKRect((float)boxRect.X, (float)boxRect.Y, (float)boxRect.Right, (float)boxRect.Bottom);
            canvas.DrawRoundRect(skBoxRect, 2f / safeZoom, 2f / safeZoom, borderPaint);

            ShapeRenderHelper.DrawHandlesSkiaSides(canvas, boxRect, zoom);
        }

        // 2. Fondo de selección de texto (si hay selección activa)
        if (IsEditing && HasSelection)
        {
            using var selPaint = new SKPaint
            {
                Color = Constants.TextToolSelectionSKColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = false
            };

            foreach (var rect in tg.GetSelectionRects(font))
            {
                canvas.DrawRect(new SKRect((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom), selPaint);
            }
        }

        if (!string.IsNullOrWhiteSpace(Text))
        {
            // 3. Contorno claro (8 direcciones para halo suave 360° en fondos oscuros)
            using var paintLight = new SKPaint
            {
                Color = Constants.TextToolLightContourSKColor,
                IsAntialias = true
            };
            foreach (var (dx, dy) in s_contourOffsets)
            {
                foreach (var line in lines)
                {
                    if (!string.IsNullOrEmpty(line.Text))
                    {
                        canvas.DrawText(line.Text, baseOffsetX + dx, startY + line.YOffset + dy, SKTextAlign.Left, font, paintLight);
                    }
                }
            }

            // 4. Contorno oscuro (8 direcciones para nitidez suave 360° en fondos claros)
            using var paintDark = new SKPaint
            {
                Color = Constants.TextToolDarkContourSKColor,
                IsAntialias = true
            };
            foreach (var (dx, dy) in s_contourOffsets)
            {
                foreach (var line in lines)
                {
                    if (!string.IsNullOrEmpty(line.Text))
                    {
                        canvas.DrawText(line.Text, baseOffsetX + dx, startY + line.YOffset + dy, SKTextAlign.Left, font, paintDark);
                    }
                }
            }

            // 5. Capa principal de texto
            using var paintMain = new SKPaint
            {
                Color = new SKColor(Color.R, Color.G, Color.B, Color.A),
                IsAntialias = true
            };
            foreach (var line in lines)
            {
                if (!string.IsNullOrEmpty(line.Text))
                {
                    canvas.DrawText(line.Text, baseOffsetX, startY + line.YOffset, SKTextAlign.Left, font, paintMain);
                }
            }
        }

        // 6. Cursor parpadeante de alto contraste (100% Monomotor)
        if (IsEditing && IsCaretVisible)
        {
            tg.GetCaretPosition(font, out float caretX, out float caretY, out float caretHeight);

            using var caretPaintBlack = new SKPaint { Color = Constants.TextToolCaretBlack, IsAntialias = false };
            using var caretPaintWhite = new SKPaint { Color = Constants.TextToolCaretWhite, IsAntialias = false };

            float caretW = 1.0f / safeZoom;
            canvas.DrawRect(caretX, caretY, caretW, caretHeight, caretPaintBlack);
            canvas.DrawRect(caretX + caretW, caretY, caretW, caretHeight, caretPaintWhite);
        }
    }
}
