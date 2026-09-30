using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qapptia.Core;
using Qapptia.Core.Abstractions;
using Qapptia.Core.Configuration;
using Qapptia.Core.Ipc;
using Qapptia.Core.Services;
using Qapptia.Core.Theme;
using Qapptia.UI.Components.Theme;
#if WINDOWS
using Qapptia.Platform.Windows;
#elif LINUX
using Qapptia.Platform.Linux;
#elif MAC
using Qapptia.Platform.MacOS;
#endif
using ConfigConstants = Qapptia.App.Config.Common.Constants;

namespace Qapptia.App.Config.ViewModels;

public sealed partial class ConfigViewModel : ObservableObject, IDisposable
{
    private static readonly System.Text.CompositeFormat s_updateAvailableFormat = System.Text.CompositeFormat.Parse(ConfigConstants.UpdateStatusAvailableFormat);
    private static readonly System.Text.CompositeFormat s_updateUpToDateFormat = System.Text.CompositeFormat.Parse(ConfigConstants.UpdateStatusUpToDateFormat);

    private readonly JsonConfigService _configService;
    private readonly IUpdateCheckService _updateCheckService;
    private readonly IShellService _shellService;
    private readonly SponsorVisualInfo _sponsorVisual;
    private QapptiaConfig _config;

    public IReadOnlyList<string> ThemeOptions { get; } = ThemeConstants.DisplayNames;

    [ObservableProperty]
    private int _selectedTabIndex;

    public string SponsorText => _sponsorVisual.Text;
    public string SponsorIconKey => _sponsorVisual.IconKey;
    public string SponsorStyleKey => _sponsorVisual.StyleKey;
    public bool IsSponsorStyleA => _sponsorVisual.StyleKey == SponsorVisualHelper.StyleA;
    public bool IsSponsorStyleB => _sponsorVisual.StyleKey == SponsorVisualHelper.StyleB;
    public bool IsSponsorStyleC => _sponsorVisual.StyleKey == SponsorVisualHelper.StyleC;
    public bool IsSponsorStyleD => _sponsorVisual.StyleKey == SponsorVisualHelper.StyleD;
    public IImage? SponsorIcon => Application.Current?.TryGetResource(SponsorIconKey, null, out var res) == true ? res as IImage : null;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExecuteUpdateAction))]
    private bool _isCheckingUpdate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExecuteUpdateAction))]
    private bool _isUpdateCooldown;

    public bool CanExecuteUpdateAction => !IsCheckingUpdate && !IsUpdateCooldown;

    [ObservableProperty]
    private string _updateStatusMessage = ConfigConstants.UpdateStatusPrompt;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string _updateButtonText = ConfigConstants.UpdateButtonCheck;

    [ObservableProperty]
    private string? _downloadUrl;

    [ObservableProperty]
    private string _selectedTheme = ThemeConstants.DisplayNameSystem;

    [ObservableProperty]
    private string _savePath = string.Empty;

    [ObservableProperty]
    private string _filenameFormat = string.Empty;

    [ObservableProperty]
    private bool _subfolderMonth;

    [ObservableProperty]
    private bool _subfolderDay;

    [ObservableProperty]
    private bool _subfolderHour;

    [ObservableProperty]
    private bool _showMouse;

    [ObservableProperty]
    private bool _highlightMouse;

    [ObservableProperty]
    private int _manualTimer;

    [ObservableProperty]
    private string _shortcutScreen = string.Empty;

    [ObservableProperty]
    private string _shortcutArea = string.Empty;

    [ObservableProperty]
    private bool _copyToClipboardScreen;

    [ObservableProperty]
    private bool _copyToClipboardArea;

    [ObservableProperty]
    private bool _captureAllScreens;

    [ObservableProperty]
    private string _footerMessage = string.Empty;

    [ObservableProperty]
    private bool _isFooterError;

    public Action? RequestClose { get; set; }
    public Func<Task<string?>>? RequestBrowsePath { get; set; }

    public ConfigViewModel(
        IUpdateCheckService? updateCheckService = null,
        IShellService? shellService = null,
        bool startInAboutTab = false)
    {
        var configPath = Qapptia.Core.Constants.DefaultConfigPath;
        _configService = new JsonConfigService(configPath);
        _config = _configService.Current;

#if WINDOWS
        _shellService = shellService ?? (OperatingSystem.IsWindowsVersionAtLeast(6, 1) ? new WindowsShellService() : NullShellService.Instance);
#elif LINUX
        _shellService = shellService ?? new LinuxShellService();
#elif MAC
        _shellService = shellService ?? new MacOSShellService();
#else
        _shellService = shellService ?? NullShellService.Instance;
#endif
        _updateCheckService = updateCheckService ?? new HttpUpdateCheckService();
        _sponsorVisual = SponsorVisualHelper.GetRandomCombination();
        _selectedTabIndex = startInAboutTab ? 3 : 0;

        LoadFromConfig();
    }

    private void LoadFromConfig()
    {
        SelectedTheme = ThemeConstants.ToDisplayName(_config.Theme);
        SavePath = _config.SavePath;
        FilenameFormat = _config.FilenameFormat;
        SubfolderMonth = _config.SubfolderMonth;
        SubfolderDay = _config.SubfolderDay;
        SubfolderHour = _config.SubfolderHour;
        ShowMouse = _config.ShowMouse;
        HighlightMouse = _config.HighlightMouse;
        ManualTimer = _config.ManualTimer;
        ShortcutScreen = _config.ShortcutScreen;
        ShortcutArea = _config.ShortcutArea;
        CopyToClipboardScreen = _config.CopyToClipboardScreen;
        CopyToClipboardArea = _config.CopyToClipboardArea;
        CaptureAllScreens = _config.CaptureAllScreens;
    }

    [RelayCommand]
    private async Task BrowsePathAsync()
    {
        if (RequestBrowsePath != null)
        {
            var result = await RequestBrowsePath();
            if (!string.IsNullOrWhiteSpace(result))
            {
                SavePath = result;
            }
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(SavePath) || !Directory.Exists(Environment.ExpandEnvironmentVariables(SavePath)))
        {
            ShowFooter("Error: La ruta de guardado es inválida o no existe.", isError: true);
            return;
        }

        _config.Theme = ThemeConstants.FromDisplayName(SelectedTheme);
        _config.SavePath = SavePath;
        _config.FilenameFormat = string.IsNullOrWhiteSpace(FilenameFormat) ? Qapptia.Core.Constants.DefaultFilenameFormat : FilenameFormat;
        _config.SubfolderMonth = SubfolderMonth;
        _config.SubfolderDay = SubfolderDay;
        _config.SubfolderHour = SubfolderHour;
        _config.ShowMouse = ShowMouse;
        _config.HighlightMouse = HighlightMouse && ShowMouse;
        _config.ManualTimer = ManualTimer;
        _config.ShortcutScreen = string.IsNullOrWhiteSpace(ShortcutScreen) ? "ctrl+shift+q" : ShortcutScreen;
        _config.ShortcutArea = string.IsNullOrWhiteSpace(ShortcutArea) ? "ctrl+shift+a" : ShortcutArea;
        _config.CopyToClipboardScreen = CopyToClipboardScreen;
        _config.CopyToClipboardArea = CopyToClipboardArea;
        _config.CaptureAllScreens = CaptureAllScreens;

        try
        {
            _configService.Save();

            // Aplicar el nuevo tema a la propia ventana de Configuración al guardar
            ThemeManager.ApplyTheme(_config.Theme);

            ShowFooter(ConfigConstants.FooterConfigSaved, isError: false);

            // Difundir notificación de tema y refresco a Capture y Editor en caliente
            NotifyProcesses(new ThemeChangedNotification { Theme = _config.Theme });
            NotifyProcesses(new RefreshTrayIconRequest());
        }
        catch (Exception ex)
        {
            ShowFooter($"{ConfigConstants.FooterSaveErrorPrefix}{ex.Message}", isError: true);
        }
    }

    private static void NotifyProcesses(IpcMessage message)
    {
        string[] channels = [IpcChannels.Capture, IpcChannels.Editor];
        foreach (var channel in channels)
        {
            _ = Task.Run(async () =>
            {
                try { await QapptiaIpcClient.SendAsync(channel, message).ConfigureAwait(false); } catch { }
            });
        }
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }

    private void ShowFooter(string message, bool isError)
    {
        FooterMessage = message;
        IsFooterError = isError;

        if (!isError)
        {
            Task.Delay(5000).ContinueWith(_ =>
            {
                if (FooterMessage == message) FooterMessage = string.Empty;
            });
        }
    }

    [RelayCommand]
    private void Sponsor()
    {
        try
        {
            _shellService.OpenUrl(Constants.DefaultSponsorUrl);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error al abrir enlace de patrocinio/sponsor.");
        }
    }


    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        if (IsCheckingUpdate) return;

        IsCheckingUpdate = true;
        UpdateStatusMessage = ConfigConstants.UpdateStatusChecking;

        try
        {
            var result = await _updateCheckService.CheckForUpdatesAsync().ConfigureAwait(false);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (result.Status == UpdateCheckStatus.UpdateAvailable && result.LatestRelease != null)
                {
                    IsUpdateAvailable = true;
                    DownloadUrl = !string.IsNullOrWhiteSpace(result.LatestRelease.DownloadUrl)
                        ? result.LatestRelease.DownloadUrl
                        : Constants.DefaultDownloadUrl;
                    var notes = !string.IsNullOrWhiteSpace(result.LatestRelease.ReleaseNotes)
                        ? $" ({result.LatestRelease.ReleaseNotes})"
                        : string.Empty;
                    UpdateStatusMessage = string.Format(System.Globalization.CultureInfo.InvariantCulture, s_updateAvailableFormat, result.LatestRelease.Version, notes);
                    UpdateButtonText = ConfigConstants.UpdateButtonDownload;
                }
                else if (result.Status == UpdateCheckStatus.UpToDate)
                {
                    IsUpdateAvailable = false;
                    DownloadUrl = null;
                    UpdateStatusMessage = string.Format(System.Globalization.CultureInfo.InvariantCulture, s_updateUpToDateFormat, Constants.AppVersion);
                    UpdateButtonText = ConfigConstants.UpdateButtonCheck;
                }
                else
                {
                    IsUpdateAvailable = false;
                    DownloadUrl = null;
                    UpdateStatusMessage = result.ErrorMessage ?? ConfigConstants.UpdateStatusConnectError;
                    UpdateButtonText = ConfigConstants.UpdateButtonRetry;
                }
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsUpdateAvailable = false;
                DownloadUrl = null;
                UpdateStatusMessage = ConfigConstants.UpdateStatusServerError;
                UpdateButtonText = ConfigConstants.UpdateButtonRetry;
            });
            Serilog.Log.Error(ex, "Error comprobando actualizaciones desde Config");
        }
        finally
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsCheckingUpdate = false;
                IsUpdateCooldown = true;
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(Constants.UpdateCheckCooldownMs).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore task failures
                }
                finally
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        IsUpdateCooldown = false;
                    });
                }
            });
        }
    }

    [RelayCommand]
    private void DownloadUpdate()
    {
        try
        {
            var targetUrl = !string.IsNullOrWhiteSpace(DownloadUrl) ? DownloadUrl : Constants.DefaultDownloadUrl;
            _shellService.OpenUrl(targetUrl);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error al abrir enlace de descarga de actualización.");
        }
    }

    [RelayCommand]
    private async Task UpdateActionAsync()
    {
        if (IsUpdateAvailable)
        {
            DownloadUpdate();
        }
        else
        {
            if (!CanExecuteUpdateAction) return;
            await CheckUpdatesAsync();
        }
    }

    public void Dispose()
    {
        if (_updateCheckService is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
