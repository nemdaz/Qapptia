using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Qapptia.Core;
using Qapptia.Core.Abstractions;
using Qapptia.Core.Launcher;
using Serilog;

namespace Qapptia.Platform.Windows;

/// <summary>
/// Implementación de IAutoStartService para Windows basada en la clave de registro Run del usuario actual.
/// Registra o remueve la ejecución de Qapptia con el parámetro --capture al iniciar sesión.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsAutoStartService : IAutoStartService
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = Constants.LauncherAppName;
    private readonly ILogger? _logger;

    public WindowsAutoStartService(ILogger? logger = null)
    {
        _logger = logger?.ForContext<WindowsAutoStartService>();
    }

    public bool IsSupported => OperatingSystem.IsWindows();

    public bool IsAutoStartEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Error al consultar estado de autostart en registro de Windows");
            return false;
        }
    }

    public bool SetAutoStartEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunRegistryKey);
            if (key == null)
            {
                _logger?.Error("No se pudo abrir o crear la clave de registro {Key}", RunRegistryKey);
                return false;
            }

            if (enabled)
            {
                string launcherPath = LauncherOrchestrator.ResolveLauncherPath().Trim('"');
                string command = $"\"{launcherPath}\" {Constants.ArgCapture}";
                key.SetValue(ValueName, command);
                _logger?.Information("Autostart de Windows registrado: {Command}", command);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                _logger?.Information("Autostart de Windows eliminado del registro.");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Error al configurar autostart en registro de Windows (enabled={Enabled})", enabled);
            return false;
        }
    }
}
