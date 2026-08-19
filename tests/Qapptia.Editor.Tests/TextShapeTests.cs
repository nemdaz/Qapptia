using Avalonia.Input;
using Qapptia.Editor.Models;
using Xunit;

namespace Qapptia.Editor.Tests;

public class TextShapeTests
{
    [Fact]
    public void InsertTextWithoutSelectionInsertsAtCaret()
    {
        var shape = new TextShape { Text = "Hola", CaretIndex = 4, IsEditing = true };
        shape.InsertText(" Mundo");

        Assert.Equal("Hola Mundo", shape.Text);
        Assert.Equal(10, shape.CaretIndex);
        Assert.False(shape.HasSelection);
    }

    [Fact]
    public void InsertTextWithSelectionReplacesSelectedRange()
    {
        var shape = new TextShape { Text = "Hola Mundo", SelectionStart = 5, SelectionEnd = 10, CaretIndex = 10, IsEditing = true };
        Assert.True(shape.HasSelection);
        Assert.Equal("Mundo", shape.SelectedText);

        shape.InsertText("Amigo");

        Assert.Equal("Hola Amigo", shape.Text);
        Assert.Equal(10, shape.CaretIndex);
        Assert.False(shape.HasSelection);
    }

    [Fact]
    public void DeleteBackwardWithSelectionDeletesEntireSelection()
    {
        var shape = new TextShape { Text = "Texto a borrar", SelectionStart = 6, SelectionEnd = 14, CaretIndex = 14, IsEditing = true };
        shape.DeleteBackward();

        Assert.Equal("Texto ", shape.Text);
        Assert.Equal(6, shape.CaretIndex);
        Assert.False(shape.HasSelection);
    }

    [Fact]
    public void DeleteForwardWithSelectionDeletesEntireSelection()
    {
        var shape = new TextShape { Text = "Texto a borrar", SelectionStart = 6, SelectionEnd = 14, CaretIndex = 6, IsEditing = true };
        shape.DeleteForward();

        Assert.Equal("Texto ", shape.Text);
        Assert.Equal(6, shape.CaretIndex);
        Assert.False(shape.HasSelection);
    }

    [Fact]
    public void MoveCaretWithShiftExpandsSelection()
    {
        var shape = new TextShape { Text = "ABCDE", CaretIndex = 2, IsEditing = true };
        shape.ClearSelection();

        shape.MoveCaretRight(select: true);
        shape.MoveCaretRight(select: true);

        Assert.True(shape.HasSelection);
        Assert.Equal(2, shape.SelectionMin);
        Assert.Equal(4, shape.SelectionMax);
        Assert.Equal("CD", shape.SelectedText);

        shape.MoveCaretLeft(select: false);
        Assert.False(shape.HasSelection);
    }

    [Fact]
    public void SelectAllSelectsCompleteString()
    {
        var shape = new TextShape { Text = "Qapptia Editor", CaretIndex = 0, IsEditing = true };
        shape.SelectAll();

        Assert.True(shape.HasSelection);
        Assert.Equal("Qapptia Editor", shape.SelectedText);
    }

    [Fact]
    public void GetCursorTypeReturnsIbeamWhenEditingAndSizeAllWhenNotEditing()
    {
        var shape = new TextShape
        {
            Start = new Avalonia.Point(50, 50),
            End = new Avalonia.Point(50, 50),
            Text = "Hola Mundo",
            IsEditing = true
        };

        var insidePoint = new Avalonia.Point(60, 60);
        var outsidePoint = new Avalonia.Point(10, 10);

        // Editando dentro del texto -> Ibeam
        Assert.Equal(StandardCursorType.Ibeam, shape.GetCursorType(insidePoint));

        // Editando fuera del texto -> null
        Assert.Null(shape.GetCursorType(outsidePoint));

        // Sin editar dentro del texto -> SizeAll
        shape.IsEditing = false;
        Assert.Equal(StandardCursorType.SizeAll, shape.GetCursorType(insidePoint));
    }

    [Fact]
    public void SupportsTextInputAndAutoStartsTextInputOnCreationAreTrueOnlyForTextShape()
    {
        var textShape = new TextShape();
        var rectShape = new RectangleShape();
        var lineShape = new LineShape();
        var arrowShape = new ArrowShape();

        Assert.True(textShape.SupportsTextInput);
        Assert.True(textShape.AutoStartsTextInputOnCreation);

        Assert.False(rectShape.SupportsTextInput);
        Assert.False(rectShape.AutoStartsTextInputOnCreation);

        Assert.False(lineShape.SupportsTextInput);
        Assert.False(lineShape.AutoStartsTextInputOnCreation);

        Assert.False(arrowShape.SupportsTextInput);
        Assert.False(arrowShape.AutoStartsTextInputOnCreation);
    }

    [Fact]
    public void OnPointerPressedInTextInputHandlesClickPositionAndSelection()
    {
        var shape = new TextShape
        {
            Start = new Avalonia.Point(50, 50),
            End = new Avalonia.Point(50, 50),
            Text = "ABCDEFG",
            IsEditing = true
        };

        // 1. Clic simple posiciona caret y activa bandera de selección por arrastre
        shape.OnPointerPressedInTextInput(new Avalonia.Point(55, 55), Avalonia.Input.KeyModifiers.None, clickCount: 1, out bool isSelecting);
        Assert.True(isSelecting);
        Assert.False(shape.HasSelection);

        // 2. Doble clic selecciona todo el texto
        shape.OnPointerPressedInTextInput(new Avalonia.Point(55, 55), Avalonia.Input.KeyModifiers.None, clickCount: 2, out bool isSelecting2);
        Assert.False(isSelecting2);
        Assert.True(shape.HasSelection);
        Assert.Equal("ABCDEFG", shape.SelectedText);
    }

    [Fact]
    public void ShapeFactoryCreatesExpectedShapeTypes()
    {
        var text = Qapptia.Editor.Core.ShapeFactory.Create(ToolType.Text, new Avalonia.Point(10, 10), Avalonia.Media.Colors.Red);
        var rect = Qapptia.Editor.Core.ShapeFactory.Create(ToolType.Rectangle, new Avalonia.Point(10, 10), Avalonia.Media.Colors.Red);
        var arrow = Qapptia.Editor.Core.ShapeFactory.Create(ToolType.Arrow, new Avalonia.Point(10, 10), Avalonia.Media.Colors.Red);

        Assert.IsType<TextShape>(text);
        Assert.IsType<RectangleShape>(rect);
        Assert.IsType<ArrowShape>(arrow);
    }
}
