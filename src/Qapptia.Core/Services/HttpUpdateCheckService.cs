using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Qapptia.Core.Abstractions;
using Serilog;

namespace Qapptia.Core.Services;

/// <summary>
/// Implementación de IUpdateCheckService que consulta un endpoint HTTP RESTful.
/// Desacoplado, resiliente ante caídas de red y con timeouts estrictos para no penalizar el hilo de UI.
/// </summary>
public sealed class HttpUpdateCheckService : IUpdateCheckService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;
    private readonly string _endpointUrl;
    private readonly string _currentVersion;
    private readonly ILogger _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly Qapptia.Core.Serialization.QapptiaJsonContext Context = new(JsonOptions);

    public HttpUpdateCheckService(
        HttpClient? httpClient = null,
        string? endpointUrl = null,
        string? currentVersion = null,
        ILogger? logger = null)
    {
        _endpointUrl = string.IsNullOrWhiteSpace(endpointUrl) ? Constants.DefaultVersionApiUrl : endpointUrl;
        _currentVersion = string.IsNullOrWhiteSpace(currentVersion) ? Constants.AppVersion : currentVersion;
        _logger = (logger ?? Log.Logger).ForContext<HttpUpdateCheckService>();

        if (httpClient != null)
        {
            _httpClient = httpClient;
            _disposeHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(Constants.DefaultHttpTimeoutMs)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Qapptia", _currentVersion));
            _disposeHttpClient = true;
        }
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(bool forceCheck = false, CancellationToken ct = default)
    {
        try
        {
            _logger.Debug("Iniciando consulta de actualización a {Url} (Versión local: {Version})...", _endpointUrl, _currentVersion);

            using var request = new HttpRequestMessage(HttpMethod.Get, _endpointUrl);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("El servidor de actualizaciones respondió con código {StatusCode}", response.StatusCode);
                return UpdateCheckResult.Failed(_currentVersion, $"Servidor no disponible (HTTP {(int)response.StatusCode}).");
            }

            var jsonContent = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.Warning("La respuesta del servidor de actualizaciones está vacía.");
                return UpdateCheckResult.Invalid(_currentVersion, "Respuesta vacía del servidor.");
            }

            var releaseDto = JsonSerializer.Deserialize(jsonContent, Context.ReleaseInfoDto);
            if (releaseDto == null || string.IsNullOrWhiteSpace(releaseDto.Version))
            {
                _logger.Warning("El JSON recibido no contiene un atributo de versión válido.");
                return UpdateCheckResult.Invalid(_currentVersion, "Carga útil no reconocida.");
            }

            if (!SemanticVersion.TryParse(_currentVersion, out var localSemver) ||
                !SemanticVersion.TryParse(releaseDto.Version, out var remoteSemver))
            {
                _logger.Warning("Fallo al comparar versiones SemVer: Local='{Local}', Remota='{Remote}'", _currentVersion, releaseDto.Version);
                return UpdateCheckResult.Invalid(_currentVersion, "Formato SemVer no reconocido.");
            }

            if (remoteSemver > localSemver)
            {
                _logger.Information("Nueva versión detectada: {NewVersion} (Actual: {LocalVersion})", remoteSemver, localSemver);
                return UpdateCheckResult.UpdateAvailable(_currentVersion, releaseDto);
            }

            _logger.Debug("Qapptia está actualizado (Local: {LocalVersion}, Servidor: {RemoteVersion}).", localSemver, remoteSemver);
            return UpdateCheckResult.UpToDate(_currentVersion, releaseDto);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("La comprobación de actualización fue cancelada.");
            return UpdateCheckResult.Failed(_currentVersion, "Comprobación cancelada.");
        }
        catch (JsonException ex)
        {
            _logger.Warning(ex, "Error al deserializar la respuesta JSON de actualización.");
            return UpdateCheckResult.Invalid(_currentVersion, "Carga útil JSON no válida o malformada.");
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error al verificar actualizaciones en {Url}", _endpointUrl);
            return UpdateCheckResult.Failed(_currentVersion, "No se pudo conectar con el servidor de actualizaciones.");
        }
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
