using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Qapptia.Core;
using Qapptia.Core.Abstractions;
using Serilog;

namespace Qapptia.Capture;

/// <summary>
/// Servicio de fondo que verifica periódicamente si existe una nueva versión de Qapptia.
/// Ejecuta una primera verificación tras un delay inicial de 3 segundos y luego cada 24 horas.
/// </summary>
public sealed class UpdateCheckBackgroundService : BackgroundService
{
    private static readonly System.Text.CompositeFormat s_updateMessageFormat = System.Text.CompositeFormat.Parse(Constants.NotificationMessageUpdateAvailableFormat);

    private readonly IUpdateCheckService _updateCheckService;
    private readonly ITrayIconService? _trayIconService;
    private readonly ILogger _logger;

    public UpdateCheckBackgroundService(
        IUpdateCheckService updateCheckService,
        ITrayIconService? trayIconService = null,
        ILogger? logger = null)
    {
        _updateCheckService = updateCheckService;
        _trayIconService = trayIconService;
        _logger = (logger ?? Log.Logger).ForContext<UpdateCheckBackgroundService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Servicio de verificación de actualizaciones en segundo plano iniciado.");

        // Espera no bloqueante inicial de 3 segundos para no impactar el arranque
        try
        {
            await Task.Delay(Constants.StartupUpdateCheckDelayMs, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _updateCheckService.CheckForUpdatesAsync(ct: stoppingToken).ConfigureAwait(false);
                if (result.Status == UpdateCheckStatus.UpdateAvailable && result.LatestRelease != null)
                {
                    _logger.Information("Nueva versión de Qapptia disponible: {Version}", result.LatestRelease.Version);
                    var message = string.Format(System.Globalization.CultureInfo.InvariantCulture, s_updateMessageFormat, result.LatestRelease.Version);
                    _trayIconService?.ShowNotification(
                        Constants.NotificationTitleUpdateAvailable,
                        message,
                        TrayNotificationType.Info,
                        Constants.NotificationDurationMs);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error durante la comprobación de actualización periódica.");
            }

            try
            {
                await Task.Delay(Constants.PeriodicUpdateCheckInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
