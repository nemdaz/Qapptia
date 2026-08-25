using Qapptia.Core.Abstractions;
using Qapptia.Core.Capture;

namespace Qapptia.Capture;

public interface IFullscreenCaptureService
{
    Task<CaptureResult> CaptureAsync(CaptureJob job, CancellationToken ct = default);
    Task<ScreenCaptureResult> CaptureFrozenScreenAsync(bool includeCursor, CancellationToken ct = default);
    Task<CaptureResult> FinalizeFrozenAreaCaptureAsync(ScreenCaptureResult frozenScreen, AreaInfo area, CaptureJob job, CancellationToken ct = default);
}

public sealed record CaptureResult(
    string FilePath,
    byte[] PngBytes,
    int Width,
    int Height);