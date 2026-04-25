using WinLive.Core;
using WinLive.Windows;

namespace WinLive.Presentation;

public sealed class WinLiveBackendHost : IAsyncDisposable
{
    private bool _started;
    private bool _disposed;

    private WinLiveBackendHost(
        IWinLiveSettingsStore settingsStore,
        WinLiveSettings settings,
        LiveSessionStore sessionStore,
        WindowsMediaSessionSource musicSource,
        WinLiveLocalApiServer localApiServer,
        WinLiveShellViewModel shellViewModel)
    {
        SettingsStore = settingsStore;
        Settings = settings;
        SessionStore = sessionStore;
        MusicSource = musicSource;
        LocalApiServer = localApiServer;
        ShellViewModel = shellViewModel;
    }

    public IWinLiveSettingsStore SettingsStore { get; }

    public WinLiveSettings Settings { get; }

    public LiveSessionStore SessionStore { get; }

    public WindowsMediaSessionSource MusicSource { get; }

    public WinLiveLocalApiServer LocalApiServer { get; }

    public WinLiveShellViewModel ShellViewModel { get; }

    public static async Task<WinLiveBackendHost> CreateAsync(
        IWinLiveSettingsStore? settingsStore = null,
        ITrayCommandService? trayCommandService = null,
        CancellationToken cancellationToken = default)
    {
        settingsStore ??= new AppDataSettingsStore();
        var settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var sessionStore = new LiveSessionStore();
        var sourceAppLauncher = new WindowsSourceAppLauncher();
        var musicSource = new WindowsMediaSessionSource(sessionStore, settings, sourceAppLauncher);
        var localApiServer = new WinLiveLocalApiServer(sessionStore, settings);
        var shellViewModel = WinLiveShellViewModel.Create(
            sessionStore,
            musicSource,
            settingsStore,
            new IslandPositionService(),
            new FullScreenDetector(),
            settings,
            trayCommandService ?? new TrayCommandService());

        return new WinLiveBackendHost(
            settingsStore,
            settings,
            sessionStore,
            musicSource,
            localApiServer,
            shellViewModel);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return;
        }

        await MusicSource.StartAsync(cancellationToken).ConfigureAwait(false);
        await LocalApiServer.StartAsync(cancellationToken).ConfigureAwait(false);
        _started = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        ShellViewModel.Dispose();
        await LocalApiServer.DisposeAsync().ConfigureAwait(false);
        await MusicSource.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }
}
