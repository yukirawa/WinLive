using WinLive.Core;
using WinLive.Windows;

namespace WinLive.App;

public sealed class WinLiveBackendHost : IAsyncDisposable
{
    private static readonly TimeSpan StartupHintDuration = TimeSpan.FromSeconds(6);

    private readonly CancellationTokenSource _shutdown = new();
    private bool _started;
    private bool _disposed;

    private WinLiveBackendHost(
        IWinLiveSettingsStore settingsStore,
        WinLiveSettings settings,
        LiveActivityStore activityStore,
        WindowsMediaActivitySource mediaSource,
        ExperimentalProgressActivitySource experimentalProgressSource,
        WinLiveLocalApiServer localApiServer,
        TrayCommandService trayCommandService,
        WinLiveShellViewModel shellViewModel)
    {
        SettingsStore = settingsStore;
        Settings = settings;
        ActivityStore = activityStore;
        MediaSource = mediaSource;
        ExperimentalProgressSource = experimentalProgressSource;
        LocalApiServer = localApiServer;
        TrayCommandService = trayCommandService;
        ShellViewModel = shellViewModel;
    }

    public IWinLiveSettingsStore SettingsStore { get; }

    public WinLiveSettings Settings { get; }

    public LiveActivityStore ActivityStore { get; }

    public WindowsMediaActivitySource MediaSource { get; }

    public ExperimentalProgressActivitySource ExperimentalProgressSource { get; }

    public WinLiveLocalApiServer LocalApiServer { get; }

    public TrayCommandService TrayCommandService { get; }

    public WinLiveShellViewModel ShellViewModel { get; }

    public static async Task<WinLiveBackendHost> CreateAsync(
        IWinLiveSettingsStore? settingsStore = null,
        CancellationToken cancellationToken = default)
    {
        settingsStore ??= new AppDataSettingsStore();
        var settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(true);
        var activityStore = new LiveActivityStore();
        var sourceAppLauncher = new WindowsSourceAppLauncher();
        var mediaSource = new WindowsMediaActivitySource(activityStore, settings, sourceAppLauncher);
        var experimentalSource = new ExperimentalProgressActivitySource(activityStore, settings);
        var localApiServer = new WinLiveLocalApiServer(activityStore, settings);
        var trayCommandService = new TrayCommandService();
        var commandRouter = new CompositeLiveActivityCommandRouter(new[] { mediaSource });
        var shellViewModel = new WinLiveShellViewModel(
            activityStore,
            commandRouter,
            settingsStore,
            new WpfTaskbarPlacementService(),
            new FullScreenDetector(),
            trayCommandService,
            settings);

        return new WinLiveBackendHost(
            settingsStore,
            settings,
            activityStore,
            mediaSource,
            experimentalSource,
            localApiServer,
            trayCommandService,
            shellViewModel);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return;
        }

        WinLiveDiagnostics.Write("Host StartAsync begin");
        ShowStartupHintIfIdle();
        await TryStartAsync("media", () => MediaSource.StartAsync(cancellationToken)).ConfigureAwait(false);
        await TryStartAsync("api", () => LocalApiServer.StartAsync(cancellationToken)).ConfigureAwait(false);
        await TryStartAsync("experimental", () => ExperimentalProgressSource.StartAsync(cancellationToken))
            .ConfigureAwait(false);
        _started = true;
        WinLiveDiagnostics.Write("Host StartAsync end");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        ShellViewModel.Dispose();
        await _shutdown.CancelAsync().ConfigureAwait(false);
        await LocalApiServer.DisposeAsync().ConfigureAwait(false);
        await ExperimentalProgressSource.DisposeAsync().ConfigureAwait(false);
        await MediaSource.DisposeAsync().ConfigureAwait(false);
        TrayCommandService.Dispose();
        _shutdown.Dispose();
        _disposed = true;
    }

    private void ShowStartupHintIfIdle()
    {
        if (ActivityStore.HasVisibleActivities)
        {
            return;
        }

        const string id = "system:startup";
        ActivityStore.Upsert(new LiveActivity
        {
            Id = id,
            Type = LiveActivityType.Custom,
            State = LiveActivityState.Active,
            Title = "WinLive is running",
            Subtitle = "Use the tray icon for settings. Live activities appear when media or progress is active.",
            Priority = -1000,
            Metadata = new Dictionary<string, string>
            {
                ["system"] = "startupHint"
            }
        });
        WinLiveDiagnostics.Write("Startup hint activity shown");

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(StartupHintDuration, _shutdown.Token).ConfigureAwait(false);
                ActivityStore.Remove(id, LiveActivityEndReason.Completed);
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private async Task TryStartAsync(string source, Func<Task> start)
    {
        try
        {
            WinLiveDiagnostics.Write($"Starting source {source}");
            await start().ConfigureAwait(false);
            WinLiveDiagnostics.Write($"Source {source} started");
        }
        catch (Exception ex)
        {
            WinLiveDiagnostics.Write($"Source {source} failed {ex}");
            ActivityStore.Upsert(new LiveActivity
            {
                Id = $"system:{source}",
                Type = LiveActivityType.Custom,
                State = LiveActivityState.Error,
                Title = $"WinLive {source} source failed",
                Subtitle = ex.Message,
                Priority = -100,
                Metadata = new Dictionary<string, string>
                {
                    ["exception"] = ex.GetType().FullName ?? ex.GetType().Name
                }
            });
        }
    }
}
