using FluentAssertions;
using Qapptia.Core.Abstractions;
using Xunit;

namespace Qapptia.Core.Tests;

public sealed class NotificationContractTests
{
    [Fact]
    public void NotificationConstantsHaveExpectedValues()
    {
        Constants.CaptureAppName.Should().Be("Qapptia Capture");
        Constants.EditorAppName.Should().Be("Qapptia Editor");
        Constants.ConfigAppName.Should().Be("Qapptia Config");
        Constants.ArgRestart.Should().Be("--restart");
        Constants.NotificationDurationMs.Should().Be(5000);
        Constants.NotificationTitleCapture.Should().Be("Qapptia Capture");
        Constants.NotificationTitleEditor.Should().Be("Qapptia Editor");
        Constants.NotificationTitleConfig.Should().Be("Qapptia Config");
        Constants.NotificationMessageCaptureStarted.Should().Be("El capturador está activo en segundo plano.");
        Constants.NotificationMessageCaptureRestarted.Should().Be("El capturador se ha reiniciado correctamente.");
    }

    [Fact]
    public void TrayNotificationTypeEnumCoversStandardSeverities()
    {
        TrayNotificationType.Info.Should().BeDefined();
        TrayNotificationType.Warning.Should().BeDefined();
        TrayNotificationType.Error.Should().BeDefined();
    }
}
