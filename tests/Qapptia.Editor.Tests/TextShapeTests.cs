using Avalonia.Input;
using Qapptia.Editor.Core;
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
    public void GetCursorTypeReturnsIbeamInsideAndSizeAllOnBorder()
    {
        var shape = new TextShape
        {
            Start = new Avalonia.Point(50, 50),
            End = new Avalonia.Point(350, 80),
            Text = "Hola Mundo",
            IsEditing = true
        };

        var insidePoint = new Avalonia.Point(100, 65);
        var borderPoint = new Avalonia.Point(100, 50);
        var outsidePoint = new Avalonia.Point(10, 10);

        // Editando dentro del texto -> Ibeam
        Assert.Equal(StandardCursorType.Ibeam, shape.GetCursorType(insidePoint));

        // Sobre el borde perimetral -> SizeAll
        Assert.Equal(StandardCursorType.SizeAll, shape.GetCursorType(borderPoint));

        // Fuera del texto -> null
        Assert.Null(shape.GetCursorType(outsidePoint));
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
        var text = Qapptia.Editor.Core.ShapeFactory.Create(ToolType.Text, new Avalonia.Point(100, 200), Avalonia.Media.Colors.Red);
        var rect = Qapptia.Editor.Core.ShapeFactory.Create(ToolType.Rectangle, new Avalonia.Point(10, 10), Avalonia.Media.Colors.Red);
        var arrow = Qapptia.Editor.Core.ShapeFactory.Create(ToolType.Arrow, new Avalonia.Point(10, 10), Avalonia.Media.Colors.Red);

        Assert.IsType<TextShape>(text);
        Assert.IsType<RectangleShape>(rect);
        Assert.IsType<ArrowShape>(arrow);

        var textShape = (TextShape)text!;
        // El punto de inicio del texto debe estar desplazado para alinear el cursor/texto con (100, 200)
        using var font = TextShape.CreateSKFont(24);
        textShape.GetCaretPosition(font, out float caretX, out float caretY, out float caretHeight);

        Assert.Equal(100.0f, caretX, 1.0f);
        // El centro vertical del caret debe coincidir con el Y del clic (200)
        float caretCenterY = caretY + (caretHeight / 2.0f);
        Assert.Equal(200.0f, caretCenterY, 1.0f);
    }

    [Fact]
    public void BoxWidthAndUsableWidthRespectDynamicDimensionsAndMinimum()
    {
        var shape = new TextShape
        {
            Start = new Avalonia.Point(50, 50),
            End = new Avalonia.Point(450, 100),
            Text = "Hola Mundo"
        };

        // Ancho explícito de 400px
        Assert.Equal(400.0, shape.BoxWidth);
        Assert.Equal(390.0, shape.UsableWidth);

        // Ancho menor al mínimo (50px) -> se asegura el mínimo de 100px
        shape.End = new Avalonia.Point(100, 100);
        Assert.Equal(100.0, shape.BoxWidth);
        Assert.Equal(90.0, shape.UsableWidth);
    }

    [Fact]
    public void HitTestReturnsGripHandlesWhenSelectedAndNotEditing()
    {
        var shape = new TextShape
        {
            Start = new Avalonia.Point(100, 100),
            End = new Avalonia.Point(400, 130),
            Text = "Prueba de Manetas",
            IsSelected = true,
            IsEditing = false
        };

        var box = shape.HitTest(new Avalonia.Point(100, 100)); // Cerca de la esquina superior o cuerpo
        Assert.True(box == HandleType.Body || box == HandleType.LeftCenter);

        // Hit en la maneta derecha (Left=100, Right=400, CenterY aprox 115)
        using var font = TextShape.CreateSKFont(24);
        float h = shape.CalculateHeight(font);
        var handleRight = shape.HitTest(new Avalonia.Point(400, 100 + h / 2.0));
        Assert.Equal(HandleType.RightCenter, handleRight);

        // Hit en la maneta izquierda
        var handleLeft = shape.HitTest(new Avalonia.Point(100, 100 + h / 2.0));
        Assert.Equal(HandleType.LeftCenter, handleLeft);

        // Hit en el centro del cuerpo
        var handleBody = shape.HitTest(new Avalonia.Point(250, 100 + h / 2.0));
        Assert.Equal(HandleType.Body, handleBody);
    }

    [Fact]
    public void HitTestAndCursorReturnHandlesDuringTextEditing()
    {
        var shape = new TextShape
        {
            Start = new Avalonia.Point(100, 100),
            End = new Avalonia.Point(400, 130),
            Text = "Prueba Coexistencia",
            IsSelected = true,
            IsEditing = true
        };

        using var font = TextShape.CreateSKFont(24);
        float h = shape.CalculateHeight(font);

        // Durante IsEditing == true, las manetas laterales siguen activas e interactivas
        var handleRight = shape.HitTest(new Avalonia.Point(400, 100 + h / 2.0));
        Assert.Equal(HandleType.RightCenter, handleRight);
        Assert.Equal(Avalonia.Input.StandardCursorType.LeftSide, shape.GetCursorType(new Avalonia.Point(400, 100 + h / 2.0)));

        // Sobre el cuerpo de texto en edición, devuelve cursor Ibeam
        var handleBody = shape.HitTest(new Avalonia.Point(250, 100 + h / 2.0));
        Assert.Equal(HandleType.Body, handleBody);
        Assert.Equal(Avalonia.Input.StandardCursorType.Ibeam, shape.GetCursorType(new Avalonia.Point(250, 100 + h / 2.0)));
    }

    [Fact]
    public void DragHandleResizesTextShapeWidthAndMovesBody()
    {
        var shape = new TextShape
        {
            Start = new Avalonia.Point(100, 100),
            End = new Avalonia.Point(400, 130),
            Text = "Texto de Prueba",
            IsSelected = true
        };

        var handle = HandleType.RightCenter;
        shape.DragHandle(HandleType.RightCenter, 50, 0, ref handle);
        Assert.Equal(350.0, shape.BoxWidth);

        var bodyHandle = HandleType.Body;
        shape.DragHandle(HandleType.Body, 20, 30, ref bodyHandle);
        Assert.Equal(120.0, shape.Start.X);
        Assert.Equal(130.0, shape.Start.Y);
        Assert.Equal(350.0, shape.BoxWidth); // Ancho permanece intacto al mover
    }

    [Fact]
    public void IsOnBorderDetectsPerimeterAndReturnsSizeAllCursor()
    {
        var shape = new TextShape
        {
            Start = new Avalonia.Point(100, 100),
            End = new Avalonia.Point(400, 130),
            Text = "Texto con Borde",
            IsSelected = true
        };

        // Borde superior (Y = 100) -> IsOnBorder debe ser true y cursor SizeAll
        var borderPoint = new Avalonia.Point(250, 100);
        Assert.True(shape.IsOnBorder(borderPoint));
        Assert.Equal(Avalonia.Input.StandardCursorType.SizeAll, shape.GetCursorType(borderPoint));

        // Interior profundo (X = 250, Y = 115) -> IsOnBorder debe ser false y cursor Ibeam
        using var font = TextShape.CreateSKFont(24);
        float h = shape.CalculateHeight(font);
        var interiorPoint = new Avalonia.Point(250, 100 + h / 2.0);
        Assert.False(shape.IsOnBorder(interiorPoint));
        Assert.Equal(Avalonia.Input.StandardCursorType.Ibeam, shape.GetCursorType(interiorPoint));
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Se verifica explícitamente el contrato ITextInputShape.")]
    public void TextShapeImplementsITextInputShapeContract()
    {
        var shape = new TextShape
        {
            Start = new Avalonia.Point(100, 100),
            End = new Avalonia.Point(400, 130),
            Text = "Texto Polimórfico"
        };

        ITextInputShape inputShape = shape;
        Assert.Equal("Texto Polimórfico", inputShape.Text);
        Assert.False(inputShape.IsEmpty);
        Assert.Equal(100.0, inputShape.TextBounds.X);
        Assert.Equal(68.0, inputShape.TextBounds.Y); // 100 - 32
        Assert.Equal(300.0, inputShape.TextBounds.Width);

        inputShape.Text = "";
        Assert.True(inputShape.IsEmpty);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Se verifica explícitamente el contrato ITextInputShape.")]
    public void TextShapeFontSizePropertyClampsCorrectly()
    {
        var shape = new TextShape();
        ITextInputShape inputShape = shape;

        // Valor por defecto
        Assert.Equal(Constants.TextToolDefaultFontSize, inputShape.TextSize);

        // Asignación directa
        inputShape.TextSize = 36f;
        Assert.Equal(36f, inputShape.TextSize);

        // Incremento y decremento
        inputShape.TextSize += 4f;
        Assert.Equal(40f, inputShape.TextSize);

        inputShape.TextSize -= 10f;
        Assert.Equal(30f, inputShape.TextSize);

        // Clamping inferior
        inputShape.TextSize = 2f;
        Assert.Equal(Constants.TextToolMinFontSize, inputShape.TextSize);

        inputShape.TextSize -= 50f;
        Assert.Equal(Constants.TextToolMinFontSize, inputShape.TextSize);

        // Clamping superior
        inputShape.TextSize = 500f;
        Assert.Equal(Constants.TextToolMaxFontSize, inputShape.TextSize);

        inputShape.TextSize += 50f;
        Assert.Equal(Constants.TextToolMaxFontSize, inputShape.TextSize);
    }
}
