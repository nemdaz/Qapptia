using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qapptia.Core.Configuration;
using Qapptia.Core.Ipc;
using Qapptia.Core.Theme;
using Qapptia.UI.Components.Theme;

namespace Qapptia.App.Config.ViewModels;

public sealed partial class ConfigViewModel : ObservableObject
{
    private readonly JsonConfigService _configService;
    private QapptiaConfig _config;

    public IReadOnlyList<string> ThemeOptions { get; } = ThemeConstants.DisplayNames;

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
    private string _footerMessage = string.Empty;

    [ObservableProperty]
    private bool _isFooterError;

    public Action? RequestClose { get; set; }
    public Func<Task<string?>>? RequestBrowsePath { get; set; }

    public ConfigViewModel()
    {
        var configPath = Qapptia.Core.AppConstants.DefaultConfigPath;
        _configService = new JsonConfigService(configPath);
        _config = _configService.Current;
        
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
    }

    partial void OnSelectedThemeChanged(string value)
    {
        // Aplica el tema seleccionado en caliente en Configuración
        ThemeManager.ApplyTheme(ThemeConstants.FromDisplayName(value));
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
        _config.FilenameFormat = string.IsNullOrWhiteSpace(FilenameFormat) ? "Qapptia_YYYYMMDD_HHmmSS" : FilenameFormat;
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

        try
        {
            _configService.Save();
            ShowFooter("Configuración guardada exitosamente.", isError: false);
            
            // Difundir notificación de tema y refresco a Capture y Editor en caliente
            NotifyProcesses(new ThemeChangedNotification { Theme = _config.Theme });
            NotifyProcesses(new RefreshTrayIconRequest());
        }
        catch (Exception ex)
        {
            ShowFooter($"Error al guardar: {ex.Message}", isError: true);
        }
    }

    private static void NotifyProcesses(IpcMessage message)
    {
        Task.Run(async () =>
        {
            string[] channels = [IpcChannels.Capture, IpcChannels.Editor];
            foreach (var channel in channels)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", IpcChannels.GetPipeName(channel), PipeDirection.InOut);
                    await client.ConnectAsync(300);
                    await IpcWire.WriteFrameAsync(client, message);
                }
                catch { }
            }
        });
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
                if (FooterMessage == message)
                    FooterMessage = string.Empty;
            });
        }
    }
}
