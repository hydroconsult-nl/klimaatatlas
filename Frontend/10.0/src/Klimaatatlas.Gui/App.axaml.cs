using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Klimaatatlas.Core;
using Klimaatatlas.Gui.ViewModels;
using Klimaatatlas.Gui.Views;

namespace Klimaatatlas.Gui;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var viewModel = new MainViewModel(new FileDialogs(window));
            window.DataContext = viewModel;
            desktop.MainWindow = window;

            // A configuration file can be passed on the command line, otherwise the last used one is opened.
            var start = desktop.Args is { Length: > 0 } args ? args[0] : UserSettings.LastConfigPath;
            if (start is not null && File.Exists(start)) viewModel.LoadConfiguration(start);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
