using System.Windows;

namespace WinLive.App;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private WinLiveBackendHost? _host;
    private SettingsWindow? _settingsWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        WinLiveDiagnostics.Write("OnStartup begin");

        try
        {
            _singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                name: "Local\\WinLive.App.SingleInstance",
                createdNew: out var createdNew);
            if (!createdNew)
            {
                WinLiveDiagnostics.Write("Another WinLive instance is already running");
                MessageBox.Show(
                    "WinLive is already running. Use the tray icon to open settings or exit it first.",
                    "WinLive",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            _host = await WinLiveBackendHost.CreateAsync();
            WinLiveDiagnostics.Write("Host created");
            _host.ShellViewModel.SettingsRequested += OnSettingsRequested;
            _host.ShellViewModel.ExitRequested += OnExitRequested;

            var window = new MainWindow
            {
                DataContext = _host.ShellViewModel
            };
            WinLiveDiagnostics.Write($"MainWindow created visible={window.IsVisible}");

            MainWindow = window;
            window.Show();
            WinLiveDiagnostics.Write($"MainWindow shown visible={window.IsVisible}");
            await _host.StartAsync();
            WinLiveDiagnostics.Write("Host started");
        }
        catch (Exception ex)
        {
            WinLiveDiagnostics.Write($"Startup exception {ex}");
            MessageBox.Show(
                ex.ToString(),
                "WinLive startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        WinLiveDiagnostics.Write("OnExit begin");
        if (_host is not null)
        {
            _host.ShellViewModel.SettingsRequested -= OnSettingsRequested;
            _host.ShellViewModel.ExitRequested -= OnExitRequested;
            await _host.DisposeAsync();
        }

        base.OnExit(e);
        _singleInstanceMutex?.Dispose();
        WinLiveDiagnostics.Write("OnExit end");
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        if (_host is null)
        {
            return;
        }

        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow
        {
            DataContext = _host.ShellViewModel,
            Owner = MainWindow
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        Shutdown();
    }
}
