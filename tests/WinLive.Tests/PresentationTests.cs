using WinLive.Core;
using WinLive.Presentation;

namespace WinLive.Tests;

public sealed class PresentationTests
{
    [Fact]
    public async Task ShellViewModelReflectsStoreVisibilityAndPrimaryCommands()
    {
        var store = new LiveSessionStore();
        var commandRouter = new FakeCommandRouter();
        var settingsStore = new InMemorySettingsStore(new WinLiveSettings());
        var viewModel = await WinLiveShellViewModel.CreateAsync(
            store,
            commandRouter,
            settingsStore,
            new FakePositionService(),
            new FakeFullScreenDetector(false));

        store.Upsert(new LiveSession
        {
            Id = "music:test",
            Type = LiveSessionType.Music,
            Title = "Test track",
            State = LiveSessionState.Active,
            Actions =
            [
                new LiveSessionActionDescriptor
                {
                    Kind = LiveSessionActionKind.PlayPause,
                    DisplayName = "Pause",
                    IsEnabled = true
                }
            ]
        });
        commandRouter.Allow("music:test", LiveSessionActionKind.PlayPause);

        Assert.True(viewModel.IsIslandVisible);
        Assert.Equal("Test track", viewModel.PrimarySession?.Title);
        Assert.True(viewModel.PlayPauseCommand.CanExecute(null));

        viewModel.PlayPauseCommand.Execute(null);
        await commandRouter.WaitForExecutionAsync();

        Assert.Equal(("music:test", LiveSessionActionKind.PlayPause), commandRouter.LastExecution);
    }

    [Fact]
    public async Task FullScreenSuppressionHidesIslandWithoutRemovingSession()
    {
        var store = new LiveSessionStore();
        var viewModel = await WinLiveShellViewModel.CreateAsync(
            store,
            new FakeCommandRouter(),
            new InMemorySettingsStore(new WinLiveSettings { HideDuringFullScreen = true }),
            new FakePositionService(),
            new FakeFullScreenDetector(true));

        store.Upsert(new LiveSession
        {
            Id = "music:test",
            Type = LiveSessionType.Music,
            Title = "Suppressed track",
            State = LiveSessionState.Active
        });
        viewModel.RefreshFullScreenSuppression();

        Assert.NotNull(viewModel.PrimarySession);
        Assert.True(viewModel.IsFullScreenSuppressed);
        Assert.False(viewModel.IsIslandVisible);
    }
}

internal sealed class FakeCommandRouter : ILiveSessionCommandRouter
{
    private readonly HashSet<(string Id, LiveSessionActionKind Action)> _allowed = new();
    private readonly TaskCompletionSource _executionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public (string Id, LiveSessionActionKind Action)? LastExecution { get; private set; }

    public void Allow(string id, LiveSessionActionKind action)
    {
        _allowed.Add((id, action));
    }

    public bool CanExecute(string sessionId, LiveSessionActionKind action)
    {
        return _allowed.Contains((sessionId, action));
    }

    public Task ExecuteAsync(
        string sessionId,
        LiveSessionActionKind action,
        CancellationToken cancellationToken = default)
    {
        LastExecution = (sessionId, action);
        _executionSource.TrySetResult();
        return Task.CompletedTask;
    }

    public Task WaitForExecutionAsync()
    {
        return _executionSource.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }
}

internal sealed class InMemorySettingsStore : IWinLiveSettingsStore
{
    private WinLiveSettings _settings;

    public InMemorySettingsStore(WinLiveSettings settings)
    {
        _settings = settings;
    }

    public string SettingsPath => "memory";

    public Task<WinLiveSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        _settings.Normalize();
        return Task.FromResult(_settings);
    }

    public Task SaveAsync(WinLiveSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Normalize();
        _settings = settings;
        return Task.CompletedTask;
    }
}

internal sealed class FakePositionService : IIslandPositionService
{
    public IslandBounds CorrectToVisibleArea(IslandBounds desired) => desired;
}

internal sealed class FakeFullScreenDetector : IFullScreenDetector
{
    private readonly bool _isFullScreen;

    public FakeFullScreenDetector(bool isFullScreen)
    {
        _isFullScreen = isFullScreen;
    }

    public bool IsForegroundFullScreen() => _isFullScreen;
}
