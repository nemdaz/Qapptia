using Avalonia.Threading;
using Qapptia.Core.Abstractions;
using Qapptia.Core.Capture;
using Qapptia.UI.Components.Overlay;

namespace Qapptia.Capture;

public sealed class AvaloniaAreaCaptureService : IAreaCaptureService
{
    public Task<AreaInfo?> SelectAreaAsync(ScreenCaptureResult frozenScreen, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<AreaInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var overlay = new SelectionOverlayWindow(tcs, frozenScreen);
                ct.Register(() =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        try { overlay.Close(); } catch { }
                        tcs.TrySetResult(null);
                    });
                });
                overlay.Show();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }
}
