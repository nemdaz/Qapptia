using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Qapptia.App.Editor;
using Qapptia.App.Editor.Controls;
using Qapptia.App.Editor.ViewModels;
using Qapptia.App.Editor.ViewModels.Shapes;
using Qapptia.Editor.Models;
using Qapptia.Editor.Models.Geometry;
using Xunit;

namespace Qapptia.Editor.Tests;

public class FreehandLineShapeTests
{
    public static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(FreehandLineShapeTests));

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Qapptia.App.Editor.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });

    [Fact]
    public void HitTestAndBodyTranslationRespectsEndsOnlyHandlesAndMovesStroke()
    {
        var geo = new FreehandLineGeometry();
        geo.AddPoint(new Point(20, 20));
        geo.AddPoint(new Point(60, 40));
        geo.AddPoint(new Point(100, 80));

        // 1. Sin selección: no hay manetas en extremos, impacto sobre trazo es Body
        geo.IsSelected = false;
        geo.HitTest(new Point(20, 20)).Should().Be(HandleType.Body);
        geo.HitTest(new Point(100, 80)).Should().Be(HandleType.Body);

        // 2. Con selección: extremos devuelven Start y End
        geo.IsSelected = true;
        geo.HitTest(new Point(20, 20)).Should().Be(HandleType.Start);
        geo.HitTest(new Point(100, 80)).Should().Be(HandleType.End);

        // 3. Impacto sobre el cuerpo y rechazo de espacio vacío dentro del bounding box
        geo.HitTest(new Point(60, 40)).Should().Be(HandleType.Body);
        geo.HitTest(new Point(30, 70)).Should().Be(HandleType.None, "El espacio vacío dentro de la caja no debe activar la figura");

        // 4. Traslación integral mediante Move(dx, dy)
        geo.Move(15, -10);
        geo.Points[0].Should().Be(new Point(35, 10));
        geo.Points[1].Should().Be(new Point(75, 30));
        geo.Points[2].Should().Be(new Point(115, 70));
        geo.Start.Should().Be(new Point(35, 10));
        geo.End.Should().Be(new Point(115, 70));
    }

    [Fact]
    public void ContinuationContractStartInvertsPointsAndEndAppendsFluently()
    {
        var geo = new FreehandLineGeometry();
        geo.AddPoint(new Point(10, 10));
        geo.AddPoint(new Point(30, 30));

        (geo is IContinuableShape).Should().BeTrue();
        (new FreehandLineShape(geo) is IContinuableShape).Should().BeTrue();

        // 1. Continuar desde el final (End)
        geo.TryStartContinuation(HandleType.End).Should().BeTrue();
        geo.ContinueDrawing(new Point(50, 50));
        geo.Points.Count.Should().Be(3);
        geo.End.Should().Be(new Point(50, 50));

        // 2. Continuar desde el inicio (Start) invierte la secuencia para continuar naturalmente
        geo.TryStartContinuation(HandleType.Start).Should().BeTrue();
        geo.Start.Should().Be(new Point(50, 50));
        geo.End.Should().Be(new Point(10, 10));
        geo.ContinueDrawing(new Point(5, 5));
        geo.Points.Count.Should().Be(4);
        geo.End.Should().Be(new Point(5, 5));

        // 3. Manetas inválidas son rechazadas
        geo.TryStartContinuation(HandleType.Body).Should().BeFalse();
        geo.TryStartContinuation(HandleType.TopLeft).Should().BeFalse();
        geo.ShouldCommitContinuation().Should().BeTrue();
    }

    [Fact]
    public async Task BoardCanvasContinuationFromEndpointGestureExtendsShapeAgnostically()
    {
        await Session.Dispatch(() =>
        {
            var services = Program.ConfigureServices();
            var vm = services.GetRequiredService<EditorViewModel>();
            var canvas = new BoardCanvas { Width = 800, Height = 600, ViewModel = vm };
            var window = new Window { Width = 800, Height = 600, Content = canvas };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var geo = new FreehandLineGeometry();
            geo.AddPoint(new Point(100, 100));
            geo.AddPoint(new Point(150, 120));
            geo.AddPoint(new Point(200, 180));

            var shape = new FreehandLineShape(geo);
            vm.Shapes.Add(shape);
            shape.IsSelected = true;

            int initialCount = geo.Points.Count;
            int shapeCountBefore = vm.Shapes.Count;

            var pointer = new Pointer(0, PointerType.Mouse, true);

            // Clic en nodo final
            canvas.RaiseEvent(new PointerPressedEventArgs(
                canvas, pointer, canvas, new Point(200, 180), 0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None));
            Dispatcher.UIThread.RunJobs();

            // Arrastre continuando el trazo
            canvas.RaiseEvent(new PointerEventArgs(
                InputElement.PointerMovedEvent, canvas, pointer, canvas, new Point(250, 230), 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other),
                KeyModifiers.None));
            Dispatcher.UIThread.RunJobs();

            // Liberación
            canvas.RaiseEvent(new PointerReleasedEventArgs(
                canvas, pointer, canvas, new Point(250, 230), 0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
                KeyModifiers.None, MouseButton.Left));
            Dispatcher.UIThread.RunJobs();

            // Verificaciones: el trazo se prolongó, la figura no se duplicó y permanece seleccionada
            geo.Points.Count.Should().BeGreaterThan(initialCount);
            geo.End.Should().Be(new Point(250, 230));
            vm.Shapes.Count.Should().Be(shapeCountBefore);
            shape.IsSelected.Should().BeTrue();

            window.Close();
        }, TestContext.Current.CancellationToken);
    }
}
