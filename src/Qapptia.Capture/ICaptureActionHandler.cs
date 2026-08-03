namespace Qapptia.Capture;

public interface ICaptureActionHandler
{
    Task HandleWakeUpAsync(CancellationToken ct);
    Task HandleQuitAsync(CancellationToken ct);
    Task HandleRefreshTrayAsync(CancellationToken ct);
    Task HandleFullscreenCaptureAsync(CancellationToken ct);
    Task HandleAreaCaptureAsync(CancellationToken ct);
}