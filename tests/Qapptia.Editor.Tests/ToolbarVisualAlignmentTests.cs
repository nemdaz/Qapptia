using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Headless;
using FluentAssertions;
using Xunit;

namespace Qapptia.Editor.Tests;

public sealed class ToolbarVisualAlignmentTests
{
    public static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(ToolbarVisualAlignmentTests));

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Qapptia.App.Editor.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });


    [Fact]
    public async System.Threading.Tasks.Task EditorToolbarWidgetRightActionsStructureShouldMatchDesign()
    {
        await Session.Dispatch(() =>
        {
            var widget = new Qapptia.App.Editor.Controls.EditorToolbarWidget();
            var window = new Window { Content = widget, Width = 1200, Height = 100 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var zoomBox = widget.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault(cb => cb.Name == "ZoomComboBox");
            zoomBox.Should().NotBeNull("ZoomComboBox must exist in the toolbar");
            zoomBox!.FontSize.Should().Be(14, "ZoomComboBox font size should be 14");

            var rightStack = widget.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault(sp => Grid.GetColumn(sp) == 1);
            rightStack.Should().NotBeNull("Right action group should exist");

            var buttons = rightStack!.GetVisualDescendants().OfType<Button>().ToList();
            buttons.Count.Should().Be(4, "Right group must contain exactly 4 action buttons (Sponsor, Capture, Config, About)");

            // Button 0: Sponsor (has Image and TextBlock with matching font size and 18x18 icon, pastel styling and outline)
            var sponsorBtn = buttons[0];
            sponsorBtn.Classes.Should().Contain("sponsor-button", "Sponsor button must have sponsor-button class");
            sponsorBtn.Bounds.Height.Should().BeApproximately(30, 1.0, "Sponsor button height must align with toolbar buttons (30px)");
            sponsorBtn.Background.Should().BeAssignableTo<ISolidColorBrush>();
            ((ISolidColorBrush)sponsorBtn.Background!).Color.Should().Be(Color.Parse("#FDE68A"), "Sponsor button default background must be pastel mustard (#FDE68A)");
            sponsorBtn.BorderBrush.Should().BeAssignableTo<ISolidColorBrush>();
            ((ISolidColorBrush)sponsorBtn.BorderBrush!).Color.Should().Be(Color.Parse("#D97706"), "Sponsor button border must be warm amber outline (#D97706)");
            sponsorBtn.BorderThickness.Should().Be(new Thickness(1), "Sponsor button must have 1px outline");
            sponsorBtn.CornerRadius.Should().Be(new CornerRadius(6), "Sponsor button corner radius should be 6");

            // Verify SponsorStyleB dynamically applies mint palette
            sponsorBtn.Classes.Add("SponsorStyleB");
            Dispatcher.UIThread.RunJobs();
            ((ISolidColorBrush)sponsorBtn.Background!).Color.Should().Be(Color.Parse("#A7F3D0"), "SponsorStyleB must apply pastel mint background (#A7F3D0)");
            ((ISolidColorBrush)sponsorBtn.BorderBrush!).Color.Should().Be(Color.Parse("#059669"), "SponsorStyleB must apply mint outline (#059669)");
            sponsorBtn.Classes.Remove("SponsorStyleB");
            Dispatcher.UIThread.RunJobs();

            var sponsorImg = sponsorBtn.GetVisualDescendants().OfType<Image>().FirstOrDefault();
            sponsorImg.Should().NotBeNull();
            sponsorImg!.Width.Should().Be(18);
            sponsorImg.Height.Should().Be(18);

            var sponsorTb = sponsorBtn.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
            sponsorTb.Should().NotBeNull();
            sponsorTb!.FontSize.Should().Be(zoomBox.FontSize, "Sponsor button text size must match ZoomComboBox font size");
            sponsorTb.FontFamily.Should().Be(zoomBox.FontFamily, "Sponsor button font family must match ZoomComboBox font family");
            sponsorTb.FontWeight.Should().Be(FontWeight.Normal, "Sponsor button font weight must be Normal (not bold)");
            sponsorTb.Foreground.Should().BeAssignableTo<ISolidColorBrush>();
            ((ISolidColorBrush)sponsorTb.Foreground!).Color.Should().Be(Color.Parse("#78350F"), "Sponsor text must have high contrast dark warm amber foreground");

            // Button 1: Capture status
            var captureBtn = buttons[1];
            captureBtn.GetVisualDescendants().OfType<Image>().Should().NotBeEmpty();

            // Button 2: Configuración (tuerca)
            var configBtn = buttons[2];
            var configIcon = configBtn.GetVisualDescendants().OfType<PathIcon>().FirstOrDefault();
            configIcon.Should().NotBeNull();
            configIcon!.Data.Should().Be((StreamGeometry)Application.Current!.FindResource("IconConfig")!);

            // Button 3: Acerca de (to the right of Configuración, only icon, no TextBlock)
            var aboutBtn = buttons[3];
            var aboutIcon = aboutBtn.GetVisualDescendants().OfType<PathIcon>().FirstOrDefault();
            aboutIcon.Should().NotBeNull();
            aboutIcon!.Data.Should().Be((StreamGeometry)Application.Current!.FindResource("IconInfo")!);
            aboutBtn.GetVisualDescendants().OfType<TextBlock>().Should().BeEmpty("Acerca de must not contain text, icon only");

            window.Close();
        }, TestContext.Current.CancellationToken);
    }
}
