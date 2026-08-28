using Qapptia.Core.Abstractions;
using Qapptia.Core.Capture;

namespace Qapptia.Capture;

public interface IAreaCaptureService
{
    Task<AreaInfo?> SelectAreaAsync(ScreenCaptureResult frozenScreen, CancellationToken ct = default);
}
