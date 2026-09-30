using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Qapptia.Core;
using Qapptia.Core.Configuration;
using Qapptia.UI.Components.Theme;

namespace Qapptia.App.Config;

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
            var startInAboutTab = desktop.Args != null &&
                System.Linq.Enumerable.Any(desktop.Args, a => string.Equals(a, Constants.ArgAbout, StringComparison.OrdinalIgnoreCase));
            desktop.MainWindow = new MainWindow(startInAboutTab);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
