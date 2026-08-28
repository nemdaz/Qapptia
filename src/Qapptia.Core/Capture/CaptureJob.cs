using System.Text.Json.Serialization;

namespace Qapptia.Core.Capture;

public sealed record CaptureJob
{
    public CaptureMode Mode { get; init; } = CaptureMode.Fullscreen;
    public int DelayMs { get; init; }
    public bool IncludeCursor { get; init; } = true;

    [JsonIgnore]
    public AreaInfo? Area { get; init; }

    [JsonIgnore]
    public CancellationToken CancellationToken { get; init; }
}

public sealed record AreaInfo(
    int X,
    int Y,
    int Width,
    int Height);
