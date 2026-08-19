using Avalonia;
using Avalonia.Input;

namespace Qapptia.Editor.Models;

/// <summary>
/// Contrato para cualquier figura vectorial que admite entrada, edición y formato de texto.
/// </summary>
public interface ITextInputShape
{
    string Text { get; set; }
    float TextSize { get; set; }
    bool IsEditing { get; set; }
    bool IsCaretVisible { get; set; }
    int CaretIndex { get; set; }
    Rect TextBounds { get; }
    bool IsEmpty { get; }

    bool IsOnBorder(Point point, double tolerance = 6.0);
    void OnPointerPressedInTextInput(Point point, KeyModifiers modifiers, int clickCount, out bool isSelecting);
    bool HandleKeyDown(Key key, KeyModifiers modifiers, out bool shouldCommit);
    void InsertText(string text);
    void DeleteBackward();
    bool HasSelection { get; }
    string SelectedText { get; }
}
