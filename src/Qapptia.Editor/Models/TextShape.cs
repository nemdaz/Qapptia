using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Input;
using SkiaSharp;
using Qapptia.Editor.Core;

namespace Qapptia.Editor.Models;

public record struct TextLayoutLine(string Text, int StartIndex, int Length, float Width, float YOffset);

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

    private static readonly string[] s_lineSeparators = ["\r\n", "\r", "\n"];

    private float _textSize = Constants.TextToolDefaultFontSize;

    public string Text { get; set; } = string.Empty;
    public float TextSize
    {
        get => _textSize;
        set => _textSize = Math.Clamp(value, Constants.TextToolMinFontSize, Constants.TextToolMaxFontSize);
    }
    
    public SKTypeface Typeface { get; set; } = SKTypeface.Default;

    public int CaretIndex { get; set; }
    public int SelectionStart { get; set; }
    public int SelectionEnd { get; set; }
    public bool IsEditing { get; set; }
    public bool IsCaretVisible { get; set; } = true;

    public event EventHandler? FocusRequested;
    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    public double BoxWidth => Math.Max(Constants.TextToolMinWidth, Math.Abs(End.X - Start.X));
    public double UsableWidth => Math.Max(Constants.TextToolMinWidth - (Constants.TextToolOffset * 2), BoxWidth - (Constants.TextToolOffset * 2));
    public Rect TextBounds => new Rect(Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y) - 32, BoxWidth, 32);
    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);

    public bool HasSelection => SelectionStart != SelectionEnd && !string.IsNullOrEmpty(Text);
    public int SelectionMin => Math.Clamp(Math.Min(SelectionStart, SelectionEnd), 0, Text.Length);
    public int SelectionMax => Math.Clamp(Math.Max(SelectionStart, SelectionEnd), 0, Text.Length);
    public string SelectedText => HasSelection ? Text.Substring(SelectionMin, SelectionMax - SelectionMin) : string.Empty;

    public override bool SupportsTextInput => true;
    public override bool AutoStartsTextInputOnCreation => true;

    public override void OnPointerPressedInTextInput(Point point, KeyModifiers modifiers, int clickCount, out bool isSelecting)
    {
        isSelecting = false;
        int clickIdx = GetCaretIndexFromPoint(point);
        CaretIndex = clickIdx;

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            SelectionEnd = clickIdx;
        }
        else if (clickCount >= 2)
        {
            SelectAll();
        }
        else
        {
            SelectionStart = clickIdx;
            SelectionEnd = clickIdx;
            isSelecting = true;
        }

        IsCaretVisible = true;
    }

    public void ClearSelection()
    {
        SelectionStart = CaretIndex;
        SelectionEnd = CaretIndex;
    }

    public void SelectAll()
    {
        SelectionStart = 0;
        SelectionEnd = Text.Length;
        CaretIndex = Text.Length;
        IsCaretVisible = true;
    }

    #region Text Editing Operations (Monomotor)

    public void InsertText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (HasSelection)
        {
            int min = SelectionMin;
            int max = SelectionMax;
            Text = Text.Remove(min, max - min).Insert(min, text);
            CaretIndex = min + text.Length;
            ClearSelection();
        }
        else
        {
            int idx = Math.Clamp(CaretIndex, 0, Text.Length);
            Text = Text.Insert(idx, text);
            CaretIndex = idx + text.Length;
            ClearSelection();
        }
        IsCaretVisible = true;
    }

    public void InsertNewLine()
    {
        InsertText("\n");
    }

    public void DeleteBackward()
    {
        if (HasSelection)
        {
            int min = SelectionMin;
            int max = SelectionMax;
            Text = Text.Remove(min, max - min);
            CaretIndex = min;
            ClearSelection();
            IsCaretVisible = true;
        }
        else if (CaretIndex > 0 && Text.Length > 0)
        {
            int removeIdx = CaretIndex - 1;
            Text = Text.Remove(removeIdx, 1);
            CaretIndex = removeIdx;
            ClearSelection();
            IsCaretVisible = true;
        }
    }

    public void DeleteForward()
    {
        if (HasSelection)
        {
            int min = SelectionMin;
            int max = SelectionMax;
            Text = Text.Remove(min, max - min);
            CaretIndex = min;
            ClearSelection();
            IsCaretVisible = true;
        }
        else if (CaretIndex < Text.Length)
        {
            Text = Text.Remove(CaretIndex, 1);
            ClearSelection();
            IsCaretVisible = true;
        }
    }

    public void MoveCaretLeft(bool select = false)
    {
        if (!select && HasSelection)
        {
            CaretIndex = SelectionMin;
            ClearSelection();
        }
        else
        {
            int oldIdx = CaretIndex;
            CaretIndex = Math.Max(0, CaretIndex - 1);
            if (select)
            {
                if (!HasSelection) SelectionStart = oldIdx;
                SelectionEnd = CaretIndex;
            }
            else
            {
                ClearSelection();
            }
        }
        IsCaretVisible = true;
    }

    public void MoveCaretRight(bool select = false)
    {
        if (!select && HasSelection)
        {
            CaretIndex = SelectionMax;
            ClearSelection();
        }
        else
        {
            int oldIdx = CaretIndex;
            CaretIndex = Math.Min(Text.Length, CaretIndex + 1);
            if (select)
            {
                if (!HasSelection) SelectionStart = oldIdx;
                SelectionEnd = CaretIndex;
            }
            else
            {
                ClearSelection();
            }
        }
        IsCaretVisible = true;
    }

    public void MoveCaretHome(bool select = false)
    {
        int oldIdx = CaretIndex;
        CaretIndex = 0;
        if (select)
        {
            if (!HasSelection) SelectionStart = oldIdx;
            SelectionEnd = CaretIndex;
        }
        else
        {
            ClearSelection();
        }
        IsCaretVisible = true;
    }

    public void MoveCaretEnd(bool select = false)
    {
        int oldIdx = CaretIndex;
        CaretIndex = Text.Length;
        if (select)
        {
            if (!HasSelection) SelectionStart = oldIdx;
            SelectionEnd = CaretIndex;
        }
        else
        {
            ClearSelection();
        }
        IsCaretVisible = true;
    }

    public bool HandleKeyDown(Key key, KeyModifiers modifiers, out bool shouldCommit)
    {
        shouldCommit = false;
        if (!IsEditing) return false;

        bool hasShift = modifiers.HasFlag(KeyModifiers.Shift);
        bool hasCtrl = modifiers.HasFlag(KeyModifiers.Control);

        switch (key)
        {
            case Key.Back:
                DeleteBackward();
                return true;

            case Key.Delete:
                DeleteForward();
                return true;

            case Key.Left:
                MoveCaretLeft(hasShift);
                return true;

            case Key.Right:
                MoveCaretRight(hasShift);
                return true;

            case Key.Home:
                MoveCaretHome(hasShift);
                return true;

            case Key.End:
                MoveCaretEnd(hasShift);
                return true;

            case Key.Enter:
                if (hasShift)
                {
                    InsertNewLine();
                }
                else
                {
                    shouldCommit = true;
                }
                return true;

            case Key.Escape:
                shouldCommit = true;
                return true;

            case Key.A when hasCtrl:
                SelectAll();
                return true;

            default:
                return false;
        }
    }

    #endregion

    public SKFont CreateSKFont()
    {
        return new SKFont(Typeface ?? SKTypeface.Default, TextSize)
        {
            Subpixel = true,
            LinearMetrics = true,
            Hinting = SKFontHinting.None,
            Edging = SKFontEdging.SubpixelAntialias,
            BaselineSnap = false
        };
    }

    public static List<TextLayoutLine> GetLayoutLines(string text, SKFont font, float maxWidth)
    {
        var result = new List<TextLayoutLine>();
        if (string.IsNullOrEmpty(text))
        {
            result.Add(new TextLayoutLine(string.Empty, 0, 0, 0, 0));
            return result;
        }

        int currentIndex = 0;
        float currentY = 0;
        var rawLines = text.Split(s_lineSeparators, StringSplitOptions.None);

        for (int r = 0; r < rawLines.Length; r++)
        {
            var rawLine = rawLines[r];
            if (string.IsNullOrEmpty(rawLine))
            {
                result.Add(new TextLayoutLine(string.Empty, currentIndex, 0, 0, currentY));
                currentY += font.Spacing;
                if (r < rawLines.Length - 1)
                {
                    if (currentIndex + 1 < text.Length && text[currentIndex] == '\r' && text[currentIndex + 1] == '\n')
                        currentIndex += 2;
                    else
                        currentIndex += 1;
                }
                continue;
            }

            var words = rawLine.Split(' ');
            var currentLineStr = new StringBuilder();
            int lineStartIdx = currentIndex;

            for (int w = 0; w < words.Length; w++)
            {
                var word = words[w];
                string testStr = currentLineStr.Length == 0 ? word : $"{currentLineStr} {word}";
                float testWidth = font.MeasureText(testStr);

                if (testWidth <= maxWidth)
                {
                    if (currentLineStr.Length > 0) currentLineStr.Append(' ');
                    currentLineStr.Append(word);
                }
                else
                {
                    if (currentLineStr.Length > 0)
                    {
                        string lineText = currentLineStr.ToString();
                        result.Add(new TextLayoutLine(lineText, lineStartIdx, lineText.Length, font.MeasureText(lineText), currentY));
                        currentY += font.Spacing;
                        lineStartIdx += lineText.Length + 1; // +1 por el espacio divisor
                        currentLineStr.Clear();
                    }

                    float singleWordWidth = font.MeasureText(word);
                    if (singleWordWidth <= maxWidth)
                    {
                        currentLineStr.Append(word);
                    }
                    else
                    {
                        // Dividir carácter a carácter si la palabra excede el ancho útil
                        for (int c = 0; c < word.Length; c++)
                        {
                            char ch = word[c];
                            string charTest = currentLineStr.ToString() + ch;
                            if (font.MeasureText(charTest) <= maxWidth || currentLineStr.Length == 0)
                            {
                                currentLineStr.Append(ch);
                            }
                            else
                            {
                                string lineText = currentLineStr.ToString();
                                result.Add(new TextLayoutLine(lineText, lineStartIdx, lineText.Length, font.MeasureText(lineText), currentY));
                                currentY += font.Spacing;
                                lineStartIdx += lineText.Length;
                                currentLineStr.Clear();
                                currentLineStr.Append(ch);
                            }
                        }
                    }
                }
            }

            if (currentLineStr.Length > 0)
            {
                string lineText = currentLineStr.ToString();
                result.Add(new TextLayoutLine(lineText, lineStartIdx, lineText.Length, font.MeasureText(lineText), currentY));
                currentY += font.Spacing;
            }

            currentIndex += rawLine.Length;
            if (r < rawLines.Length - 1)
            {
                if (currentIndex + 1 < text.Length && text[currentIndex] == '\r' && text[currentIndex + 1] == '\n')
                    currentIndex += 2;
                else
                    currentIndex += 1;
            }
        }

        return result;
    }

    public float CalculateHeight(SKFont font)
    {
        var lines = GetLayoutLines(Text, font, (float)UsableWidth);
        return Math.Max(lines.Count * font.Spacing, 30f) + (float)Constants.TextToolOffset * 2;
    }

    public void GetCaretPosition(SKFont font, out float caretX, out float caretY, out float caretHeight)
    {
        double left = Math.Min(Start.X, End.X);
        double top = Math.Min(Start.Y, End.Y);
        float baseOffsetX = (float)(left + Constants.TextToolOffset);
        float baseOffsetY = (float)(top + Constants.TextToolOffset);

        font.GetFontMetrics(out var metrics);
        caretHeight = Math.Max(metrics.Descent - metrics.Ascent, font.Spacing * 0.9f);
        float usableWidth = (float)UsableWidth;

        var lines = GetLayoutLines(Text, font, usableWidth);
        int targetIndex = Math.Clamp(CaretIndex, 0, Text.Length);

        TextLayoutLine activeLine = lines[0];
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (targetIndex >= line.StartIndex && targetIndex <= line.StartIndex + line.Length)
            {
                activeLine = line;
                break;
            }
            if (i == lines.Count - 1)
            {
                activeLine = line;
            }
        }

        int offsetInLine = Math.Clamp(targetIndex - activeLine.StartIndex, 0, activeLine.Length);
        string textBeforeCaret = offsetInLine > 0 && offsetInLine <= activeLine.Text.Length
            ? activeLine.Text.Substring(0, offsetInLine)
            : string.Empty;

        float textWidthBeforeCaret = string.IsNullOrEmpty(textBeforeCaret) ? 0f : font.MeasureText(textBeforeCaret);
        caretX = baseOffsetX + textWidthBeforeCaret;
        caretY = baseOffsetY + activeLine.YOffset;
    }

    public int GetCaretIndexFromPoint(Point canvasPoint)
    {
        using var font = CreateSKFont();
        float usableWidth = (float)UsableWidth;
        var lines = GetLayoutLines(Text, font, usableWidth);

        double left = Math.Min(Start.X, End.X);
        double top = Math.Min(Start.Y, End.Y);
        float relativeY = (float)(canvasPoint.Y - (top + Constants.TextToolOffset));
        float relativeX = (float)(canvasPoint.X - (left + Constants.TextToolOffset));

        int lineIndex = (int)(relativeY / font.Spacing);
        lineIndex = Math.Clamp(lineIndex, 0, lines.Count - 1);
        var line = lines[lineIndex];

        if (string.IsNullOrEmpty(line.Text) || relativeX <= 0)
        {
            return line.StartIndex;
        }

        float prevDist = float.MaxValue;
        int bestChar = 0;
        for (int i = 0; i <= line.Text.Length; i++)
        {
            string sub = line.Text.Substring(0, i);
            float w = font.MeasureText(sub);
            float dist = Math.Abs(w - relativeX);
            if (dist < prevDist)
            {
                prevDist = dist;
                bestChar = i;
            }
            else
            {
                break;
            }
        }

        return line.StartIndex + bestChar;
    }

    public override void RenderSkia(SKCanvas canvas)
    {
        using var font = CreateSKFont();
        font.GetFontMetrics(out var metrics);
        float usableWidth = (float)UsableWidth;
        var lines = GetLayoutLines(Text, font, usableWidth);

        float totalHeight = Math.Max(lines.Count * font.Spacing, 30f) + (float)Constants.TextToolOffset * 2;
        double left = Math.Min(Start.X, End.X);
        double top = Math.Min(Start.Y, End.Y);
        float baseOffsetX = (float)(left + Constants.TextToolOffset);
        float baseOffsetY = (float)(top + Constants.TextToolOffset);
        float startY = (float)(top + Constants.TextToolOffset - metrics.Ascent);
        var boxRect = new Rect(left, top, BoxWidth, totalHeight);

        // 1. Marco delimitador y manetas laterales (activos simultáneamente en selección y edición)
        if (IsSelected || IsEditing)
        {
            using var borderPaint = new SKPaint
            {
                Color = IsEditing ? Constants.TextToolBorderSKColor : new SKColor(0, 120, 215, 140),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.0f,
                PathEffect = IsEditing ? null : SKPathEffect.CreateDash([4f, 4f], 0),
                IsAntialias = true
            };
            var skBoxRect = new SKRect((float)left, (float)top, (float)(left + BoxWidth), (float)(top + totalHeight));
            canvas.DrawRoundRect(skBoxRect, 2f, 2f, borderPaint);

            // Manetas laterales de control activas siempre durante la edición
            HitTestEngine.DrawHandlesSkiaSides(canvas, boxRect);
        }

        // 2. Fondo de selección de texto (si hay selección activa)
        if (IsEditing && HasSelection)
        {
            int selMin = SelectionMin;
            int selMax = SelectionMax;

            using var selPaint = new SKPaint
            {
                Color = Constants.TextToolSelectionSKColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = false
            };

            foreach (var line in lines)
            {
                int lineStart = line.StartIndex;
                int lineEnd = line.StartIndex + line.Length;

                int overlapStart = Math.Max(lineStart, selMin);
                int overlapEnd = Math.Min(lineEnd, selMax);

                if (overlapStart < overlapEnd)
                {
                    int offsetInLine = overlapStart - lineStart;
                    int lengthInLine = overlapEnd - overlapStart;

                    string beforeOverlap = offsetInLine > 0 ? line.Text.Substring(0, offsetInLine) : string.Empty;
                    string overlapText = line.Text.Substring(offsetInLine, lengthInLine);

                    float x1 = baseOffsetX + (string.IsNullOrEmpty(beforeOverlap) ? 0f : font.MeasureText(beforeOverlap));
                    float x2 = x1 + font.MeasureText(overlapText);
                    float y1 = baseOffsetY + line.YOffset;
                    float y2 = y1 + font.Spacing;

                    canvas.DrawRect(new SKRect(x1, y1, x2, y2), selPaint);
                }
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
            GetCaretPosition(font, out float caretX, out float caretY, out float caretHeight);

            using var caretPaintBlack = new SKPaint { Color = Constants.TextToolCaretBlack, IsAntialias = false };
            using var caretPaintWhite = new SKPaint { Color = Constants.TextToolCaretWhite, IsAntialias = false };

            // 2px de ancho vertical: 1px negro (izq) y 1px blanco (der) para visibilidad perfecta en cualquier fondo
            canvas.DrawRect(caretX, caretY, 1.0f, caretHeight, caretPaintBlack);
            canvas.DrawRect(caretX + 1.0f, caretY, 1.0f, caretHeight, caretPaintWhite);
        }
    }

    protected override Rect GetBoundingBox()
    {
        using var font = CreateSKFont();
        float totalHeight = CalculateHeight(font);
        double left = Math.Min(Start.X, End.X);
        double top = Math.Min(Start.Y, End.Y);
        return new Rect(left, top, BoxWidth, totalHeight);
    }

    public override HandleType HitTest(Point point)
    {
        var boxRect = GetBoundingBox();
        if (IsSelected || IsEditing)
        {
            var handle = HitTestEngine.HitTestHandlesSides(point, boxRect);
            if (handle != HandleType.None) return handle;
        }

        var inflatedRect = new Rect(boxRect.X - 4, boxRect.Y - 4, boxRect.Width + 8, boxRect.Height + 8);
        return inflatedRect.Contains(point) ? HandleType.Body : HandleType.None;
    }

    public override void DragHandle(HandleType handle, double dx, double dy, ref HandleType activeHandle)
    {
        if (handle == HandleType.Body)
        {
            Move(dx, dy);
            return;
        }

        if (handle == HandleType.RightCenter)
        {
            double newWidth = Math.Max(Constants.TextToolMinWidth, BoxWidth + dx);
            End = new Point(Start.X + newWidth, Start.Y);
        }
        else if (handle == HandleType.LeftCenter)
        {
            double currentWidth = BoxWidth;
            double newWidth = Math.Max(Constants.TextToolMinWidth, currentWidth - dx);
            double shift = currentWidth - newWidth;
            Start = new Point(Start.X + shift, Start.Y);
            End = new Point(Start.X + newWidth, Start.Y);
        }
    }

    /// <summary>
    /// Determina si un punto se encuentra en la zona perimetral del recuadro de texto (borde de agarre/selección).
    /// </summary>
    public bool IsOnBorder(Point point, double tolerance = 6.0)
    {
        var box = GetBoundingBox();
        var outer = new Rect(box.X - tolerance, box.Y - tolerance, box.Width + tolerance * 2, box.Height + tolerance * 2);
        if (!outer.Contains(point)) return false;

        var inner = new Rect(box.X + tolerance, box.Y + tolerance, Math.Max(0, box.Width - tolerance * 2), Math.Max(0, box.Height - tolerance * 2));
        return !inner.Contains(point);
    }

    public override StandardCursorType? GetCursorType(Point point)
    {
        var handle = HitTest(point);
        if (handle == HandleType.LeftCenter || handle == HandleType.RightCenter)
        {
            return StandardCursorType.LeftSide;
        }

        if (handle == HandleType.Body)
        {
            if (IsOnBorder(point))
            {
                return StandardCursorType.SizeAll;
            }
            return StandardCursorType.Ibeam;
        }

        return null;
    }
}
