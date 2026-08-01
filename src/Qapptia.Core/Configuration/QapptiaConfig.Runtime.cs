using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Qapptia.Core.Configuration;

/// <summary>
/// Seccion: parametros de runtime (no editables por UI; ajustables manualmente
/// en config.json).
/// </summary>
public sealed partial class QapptiaConfig
{
    [JsonPropertyName("main_loop_sleep_seconds")]
    [Range(0.1, 60.0)]
    public double MainLoopSleepSeconds { get; set; } = 1.0;

    [JsonPropertyName("suspend_jump_threshold_seconds")]
    [Range(1.0, 600.0)]
    public double SuspendJumpThresholdSeconds { get; set; } = 10.0;

    [JsonPropertyName("editor_double_click_seconds")]
    [Range(0.05, 5.0)]
    public double EditorDoubleClickSeconds { get; set; } = 0.4;

    [JsonPropertyName("restart_grace_period_seconds")]
    [Range(0.0, 30.0)]
    public double RestartGracePeriodSeconds { get; set; } = 1.5;

    [JsonPropertyName("editor_launch_guard_seconds")]
    [Range(0.0, 60.0)]
    public double EditorLaunchGuardSeconds { get; set; } = 5.0;
}
