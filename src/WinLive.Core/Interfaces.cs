namespace WinLive.Core;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}

public interface ILiveSessionStore
{
    event EventHandler<LiveSessionChangedEventArgs>? SessionsChanged;

    IReadOnlyList<LiveSession> Sessions { get; }

    IReadOnlyList<LiveSession> VisibleSessions { get; }

    LiveSession? PrimarySession { get; }

    bool HasVisibleSessions { get; }

    bool TryGetSession(string id, out LiveSession session);

    LiveSession Upsert(LiveSession session);

    bool Remove(string id, LiveSessionEndReason reason = LiveSessionEndReason.Deleted);

    int ClearExpiredEmphasis(DateTimeOffset now);
}

public interface ILiveSessionCommandRouter
{
    bool CanExecute(string sessionId, LiveSessionActionKind action);

    Task ExecuteAsync(string sessionId, LiveSessionActionKind action, CancellationToken cancellationToken = default);
}

public interface ISourceAppLauncher
{
    bool CanLaunch(string sourceAppUserModelId);

    Task<bool> LaunchAsync(string sourceAppUserModelId, CancellationToken cancellationToken = default);
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

public interface IScreenBoundsProvider
{
    IReadOnlyList<ScreenBounds> GetWorkingAreas();
}

public interface IIslandPositionService
{
    IslandBounds CorrectToVisibleArea(IslandBounds desired);
}

public interface IAutostartService
{
    bool IsEnabled(string appName);

    void SetEnabled(string appName, string executablePath, bool enabled);
}

public interface ITrayCommandService
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
