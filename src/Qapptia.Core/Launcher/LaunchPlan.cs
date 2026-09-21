namespace Qapptia.Core.Launcher;

/// <summary>
/// Especifica el objetivo de ejecución interpretado a partir de los argumentos de entrada.
/// </summary>
public enum LaunchTarget
{
    Suite,
    CaptureOnly,
    EditorOnly,
    ConfigOnly
}

/// <summary>
/// Representa el plan de despacho de procesos determinado por el launcher.
/// </summary>
public sealed record LaunchPlan(
    LaunchTarget Target,
    string? AdditionalArguments,
    bool ShouldEnsureCapture,
    bool ShouldEnsureEditor,
    bool ShouldEnsureConfig);
