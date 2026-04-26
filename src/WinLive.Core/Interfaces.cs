namespace WinLive.Core;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}

public interface ILiveActivityStore
{
    event EventHandler<LiveActivityChangedEventArgs>? ActivitiesChanged;

    IReadOnlyList<LiveActivity> Activities { get; }

    IReadOnlyList<LiveActivity> VisibleActivities { get; }

    LiveActivity? PrimaryActivity { get; }

    bool HasVisibleActivities { get; }

    bool TryGetActivity(string id, out LiveActivity activity);

    LiveActivity Upsert(LiveActivity activity);

    bool Remove(string id, LiveActivityEndReason reason = LiveActivityEndReason.Deleted);

    int ClearExpiredEmphasis(DateTimeOffset now);
}

public interface ILiveActivityCommandRouter
{
    bool CanExecute(string activityId, LiveActivityActionKind action);

    Task ExecuteAsync(
        string activityId,
        LiveActivityActionKind action,
        CancellationToken cancellationToken = default);
}

public interface ISourceAppLauncher
{
    bool CanLaunch(string? appUserModelId);

    Task<bool> LaunchAsync(string? appUserModelId, CancellationToken cancellationToken = default);
}

public interface IWinLiveSettingsStore
{
    string SettingsPath { get; }

    Task<WinLiveSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(WinLiveSettings settings, CancellationToken cancellationToken = default);
}

public interface IFullScreenDetector
{
    bool IsForegroundFullScreen();
}

public interface ITaskbarPlacementService
{
    IslandBounds GetDefaultIslandBounds(IslandBounds preferred);

    IslandBounds CorrectToVisibleArea(IslandBounds desired);
}

public interface ITrayCommandService : IDisposable
{
    event EventHandler? OpenSettingsRequested;

    event EventHandler? ResetPositionRequested;

    event EventHandler? ExitRequested;

    void PublishIslandVisibility(bool isVisible);
}

public interface ILocalApiServer : IAsyncDisposable
{
    bool IsRunning { get; }

    Uri? BaseAddress { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

public interface ILiveActivitySource : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
}
