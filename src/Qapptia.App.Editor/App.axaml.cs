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
            var stateStoreLogger = Serilog.Log.Logger.ForContext<Qapptia.Editor.Models.EditorStateStore>();
            var stateStore = new Qapptia.Editor.Models.EditorStateStore(
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

            var sidebarServiceLogger = Serilog.Log.Logger.ForContext<Qapptia.Editor.Sidebar.Services.SidebarService>();
            var sidebarService = new Qapptia.Editor.Sidebar.Services.SidebarService(sidebarServiceLogger);

            var vm = new Qapptia.App.Editor.ViewModels.EditorViewModel(stateStore, savePath, fontProvider, clipboardService, sidebarService);

            var mainWindow = new MainWindow();
            mainWindow.InitializeWithViewModel(vm);
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}