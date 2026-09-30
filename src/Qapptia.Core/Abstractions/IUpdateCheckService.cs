using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Qapptia.Core.Abstractions;

/// <summary>
/// Estados de resultado del proceso de comprobación de actualizaciones.
/// </summary>
public enum UpdateCheckStatus
{
    /// <summary>La aplicación se encuentra en la versión más reciente.</summary>
    UpToDate,

    /// <summary>Existe una nueva versión disponible superior a la actual.</summary>
    UpdateAvailable,

    /// <summary>No se pudo conectar con el servidor o expiró el tiempo de espera.</summary>
    ConnectionFailed,

    /// <summary>La respuesta remota no pudo deserializarse o contenía un SemVer inválido.</summary>
    InvalidPayload
}

/// <summary>
/// DTO estructurado para la carga útil del endpoint remoto de versión.
/// Sigue las buenas prácticas de APIs RESTful con atributos en camelCase e inglés.
/// </summary>
public sealed class ReleaseInfoDto
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("checksum")]
    public string? Checksum { get; set; }

    [JsonPropertyName("publishedAt")]
    public string? PublishedAt { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("sponsorUrl")]
    public string? SponsorUrl { get; set; }

    [JsonPropertyName("isCritical")]
    public bool? IsCritical { get; set; }

    [JsonPropertyName("minSupportedVersion")]
    public string? MinSupportedVersion { get; set; }
}

/// <summary>
/// Resultado del proceso de verificación de versiones.
/// </summary>
public sealed class UpdateCheckResult
{
    public UpdateCheckStatus Status { get; init; }
    public ReleaseInfoDto? LatestRelease { get; init; }
    public string CurrentVersion { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }

    public bool IsUpdateAvailable => Status == UpdateCheckStatus.UpdateAvailable;

    public static UpdateCheckResult UpToDate(string currentVersion, ReleaseInfoDto? release = null) =>
        new() { Status = UpdateCheckStatus.UpToDate, CurrentVersion = currentVersion, LatestRelease = release };

    public static UpdateCheckResult UpdateAvailable(string currentVersion, ReleaseInfoDto release) =>
        new() { Status = UpdateCheckStatus.UpdateAvailable, CurrentVersion = currentVersion, LatestRelease = release };

    public static UpdateCheckResult Failed(string currentVersion, string errorMessage) =>
        new() { Status = UpdateCheckStatus.ConnectionFailed, CurrentVersion = currentVersion, ErrorMessage = errorMessage };

    public static UpdateCheckResult Invalid(string currentVersion, string errorMessage) =>
        new() { Status = UpdateCheckStatus.InvalidPayload, CurrentVersion = currentVersion, ErrorMessage = errorMessage };
}

/// <summary>
/// Contrato de servicio para verificar actualizaciones remotas del software.
/// </summary>
public interface IUpdateCheckService
{
    /// <summary>
    /// Consulta el endpoint remoto para determinar si existe una nueva versión de Qapptia.
    /// </summary>
    /// <param name="forceCheck">Si es true, ignora marcas de tiempo de caché previa.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Resultado estructurado de la verificación.</returns>
    Task<UpdateCheckResult> CheckForUpdatesAsync(bool forceCheck = false, CancellationToken ct = default);
}
