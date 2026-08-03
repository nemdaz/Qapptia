using Qapptia.Core.Capture;

namespace Qapptia.Capture;

public interface IAreaCaptureService
{
    Task<AreaInfo?> SelectAreaAsync(CancellationToken ct = default);
}