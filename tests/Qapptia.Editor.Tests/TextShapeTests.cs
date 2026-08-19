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
}
