using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Qapptia.Core;
using Qapptia.Core.Configuration;
using Qapptia.UI.Components.Theme;

namespace Qapptia.App.Editor;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        var configPath = Constants.DefaultConfigPath;
        var configService = new JsonConfigService(configPath);
        RequestedThemeVariant = ThemeManager.GetThemeVariant(configService.Current.Theme);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configService = new Qapptia.Core.Configuration.JsonConfigService(Qapptia.Core.Constants.DefaultConfigPath);
            var savePath = string.IsNullOrWhiteSpace(configService.Current.SavePath) ? Qapptia.Core.Constants.DefaultSavePath : configService.Current.SavePath;
            var stateStoreLogger = Serilog.Log.Logger.ForContext<Qapptia.Editor.Services.EditorStateStore>();
            var stateStore = new Qapptia.Editor.Services.EditorStateStore(
                savePath, 
                Qapptia.Core.Constants.EditorStateFileName,
                stateStoreLogger);
            
#if WINDOWS
            var clipboardService = new Qapptia.Platform.Windows.WindowsClipboardService(Serilog.Log.Logger);
#else
            Qapptia.Core.Abstractions.IClipboardService? clipboardService = null;
#endif

            var fontProviderLogger = Serilog.Log.Logger.ForContext<Qapptia.Editor.Core.AssetFontProvider>();
            var fontProvider = new Qapptia.Editor.Core.AssetFontProvider(fontProviderLogger);

            var navigationServiceLogger = Serilog.Log.Logger.ForContext<Qapptia.Editor.Services.NavigationService>();
            var navigationService = new Qapptia.Editor.Services.NavigationService(navigationServiceLogger);

            var vm = new Qapptia.App.Editor.ViewModels.EditorViewModel(stateStore, savePath, fontProvider, clipboardService, navigationService);

            var mainWindow = new MainWindow();
            mainWindow.InitializeWithViewModel(vm);
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}