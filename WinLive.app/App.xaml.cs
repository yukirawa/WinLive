using System.Windows;
using WinLive.Presentation;

namespace WinLive.app;

public partial class App : Application
{
    private WinLiveBackendHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _host = await WinLiveBackendHost.CreateAsync();
            await _host.StartAsync();

            var window = new MainWindow
            {
                DataContext = _host.ShellViewModel
            };

            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "WinLive 起動エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        base.OnExit(e);
    }
}