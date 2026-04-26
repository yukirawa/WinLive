using WinLive.App;
using WinLive.Core;

namespace WinLive.Tests;

public sealed class ShellViewModelTests
{
    [Fact]
    public void FullScreenSuppressionHidesIslandWithoutRemovingActivity()
    {
        using var tray = new FakeTrayCommandService();
        var store = new LiveActivityStore();
        var viewModel = new WinLiveShellViewModel(
            store,
            new NoOpLiveActivityCommandRouter(),
            new InMemorySettingsStore(new WinLiveSettings { HideDuringFullScreen = true }),
            new FakePlacementService(),
            new FakeFullScreenDetector(true),
            tray,
            new WinLiveSettings { HideDuringFullScreen = true });

        store.Upsert(new LiveActivity
        {
            Id = "activity",
            Title = "Activity",
            State = LiveActivityState.Active
        });

        Assert.NotNull(viewModel.PrimaryActivity);
        Assert.True(viewModel.IsFullScreenSuppressed);
        Assert.False(viewModel.IsIslandVisible);
        viewModel.Dispose();
    }

    [Fact]
    public void CommandsReflectPrimaryActionAvailability()
    {
        using var tray = new FakeTrayCommandService();
        var store = new LiveActivityStore();
        var router = new FakeCommandRouter();
        var viewModel = new WinLiveShellViewModel(
            store,
            router,
            new InMemorySettingsStore(new WinLiveSettings()),
            new FakePlacementService(),
            new FakeFullScreenDetector(false),
            tray,
            new WinLiveSettings());

        store.Upsert(new LiveActivity
        {
            Id = "media:test",
            Title = "Track",
            State = LiveActivityState.Active,
            Actions =
            [
                new LiveActivityActionDescriptor
                {
                    Kind = LiveActivityActionKind.PlayPause,
                    DisplayName = "Pause"
                }
            ]
        });
        router.Allow("media:test", LiveActivityActionKind.PlayPause);

        Assert.True(viewModel.IsIslandVisible);
        Assert.True(viewModel.PlayPauseCommand.CanExecute(null));
        viewModel.Dispose();
    }

    [Fact]
    public async Task CommitDraggedPositionPersistsCustomIslandPosition()
    {
        using var tray = new FakeTrayCommandService();
        var settings = new WinLiveSettings();
        var settingsStore = new InMemorySettingsStore(settings);
        var viewModel = new WinLiveShellViewModel(
            new LiveActivityStore(),
            new NoOpLiveActivityCommandRouter(),
            settingsStore,
            new FakePlacementService(),
            new FakeFullScreenDetector(false),
            tray,
            settings);

        await viewModel.CommitDraggedPositionAsync(240, 180);

        Assert.True(settingsStore.Current.HasCustomIslandPosition);
        Assert.Equal(240, settingsStore.Current.IslandBounds.Left);
        Assert.Equal(180, settingsStore.Current.IslandBounds.Top);
        viewModel.Dispose();
    }

    [Fact]
    public void UpExpansionKeepsCompactTileAnchoredAtBottom()
    {
        using var tray = new FakeTrayCommandService();
        var store = new LiveActivityStore();
        var settings = new WinLiveSettings
        {
            HasCustomIslandPosition = true,
            IslandBounds = new IslandBounds(240, 180, 374, 56),
            ExpansionDirection = IslandExpansionDirection.Up
        };
        var viewModel = new WinLiveShellViewModel(
            store,
            new NoOpLiveActivityCommandRouter(),
            new InMemorySettingsStore(settings),
            new FakePlacementService(),
            new FakeFullScreenDetector(false),
            tray,
            settings);

        store.Upsert(new LiveActivity
        {
            Id = "primary",
            Title = "Primary",
            State = LiveActivityState.Active,
            Priority = 100
        });
        store.Upsert(new LiveActivity
        {
            Id = "secondary",
            Title = "Secondary",
            State = LiveActivityState.Active,
            Priority = 10
        });

        viewModel.IsExpanded = true;

        Assert.True(viewModel.ShowUpSecondaryTiles);
        Assert.False(viewModel.ShowDownSecondaryTiles);
        Assert.Equal(374, viewModel.WindowWidth);
        Assert.Equal(120, viewModel.WindowHeight);
        Assert.Equal(240, viewModel.WindowLeft);
        Assert.Equal(116, viewModel.WindowTop);
        viewModel.Dispose();
    }

    [Fact]
    public void IslandSizePresetControlsCompactBounds()
    {
        using var tray = new FakeTrayCommandService();
        var settings = new WinLiveSettings
        {
            IslandSize = IslandSizePreset.Small
        };
        var viewModel = new WinLiveShellViewModel(
            new LiveActivityStore(),
            new NoOpLiveActivityCommandRouter(),
            new InMemorySettingsStore(settings),
            new FakePlacementService(),
            new FakeFullScreenDetector(false),
            tray,
            settings);

        Assert.True(viewModel.IsIslandSizeSmall);
        Assert.Equal(320, viewModel.WindowWidth);
        Assert.Equal(50, viewModel.WindowHeight);

        viewModel.IsIslandSizeLarge = true;

        Assert.True(viewModel.IsIslandSizeLarge);
        Assert.Equal(IslandSizePreset.Large, settings.IslandSize);
        Assert.Equal(430, viewModel.WindowWidth);
        Assert.Equal(66, viewModel.WindowHeight);
        viewModel.Dispose();
    }

    [Fact]
    public void NonMediaPrimaryUsesContextualControls()
    {
        using var tray = new FakeTrayCommandService();
        var store = new LiveActivityStore();
        var viewModel = new WinLiveShellViewModel(
            store,
            new NoOpLiveActivityCommandRouter(),
            new InMemorySettingsStore(new WinLiveSettings()),
            new FakePlacementService(),
            new FakeFullScreenDetector(false),
            tray,
            new WinLiveSettings());

        store.Upsert(new LiveActivity
        {
            Id = "download",
            Type = LiveActivityType.Download,
            Title = "Download",
            State = LiveActivityState.Active,
            Progress = 0.5
        });

        Assert.False(viewModel.ShowMediaTransportControls);
        Assert.False(viewModel.ShowPreviousNextControls);
        Assert.True(viewModel.ShowDismissControl);
        Assert.False(viewModel.PlayPauseCommand.CanExecute(null));
        viewModel.Dispose();
    }

    [Fact]
    public void AddingSecondActivityKeepsPrimaryAndAutoExpands()
    {
        using var tray = new FakeTrayCommandService();
        var store = new LiveActivityStore();
        var viewModel = new WinLiveShellViewModel(
            store,
            new NoOpLiveActivityCommandRouter(),
            new InMemorySettingsStore(new WinLiveSettings()),
            new FakePlacementService(),
            new FakeFullScreenDetector(false),
            tray,
            new WinLiveSettings());

        store.Upsert(new LiveActivity
        {
            Id = "media:first",
            Title = "First",
            State = LiveActivityState.Active,
            Priority = 10
        });
        store.Upsert(new LiveActivity
        {
            Id = "media:second",
            Title = "Second",
            State = LiveActivityState.Active,
            Priority = 100
        });

        Assert.Equal("media:first", viewModel.PrimaryActivity?.Id);
        Assert.True(viewModel.IsExpanded);
        Assert.True(viewModel.HasSecondaryActivities);
        Assert.Contains(viewModel.SecondaryActivities, item => item.Id == "media:second");
        viewModel.Dispose();
    }

    [Fact]
    public async Task SelectActivitySwitchesPrimaryAndCommandTarget()
    {
        using var tray = new FakeTrayCommandService();
        var store = new LiveActivityStore();
        var router = new FakeCommandRouter();
        var viewModel = new WinLiveShellViewModel(
            store,
            router,
            new InMemorySettingsStore(new WinLiveSettings()),
            new FakePlacementService(),
            new FakeFullScreenDetector(false),
            tray,
            new WinLiveSettings());

        store.Upsert(MediaActivity("media:first", "First", 100));
        store.Upsert(MediaActivity("media:second", "Second", 10));
        router.Allow("media:second", LiveActivityActionKind.PlayPause);

        viewModel.SelectActivityCommand.Execute("media:second");

        Assert.Equal("media:second", viewModel.PrimaryActivity?.Id);
        Assert.True(viewModel.PlayPauseCommand.CanExecute(null));

        viewModel.PlayPauseCommand.Execute(null);
        await Task.Yield();

        Assert.Contains(
            router.ExecutedActions,
            item => item.Id == "media:second" && item.Action == LiveActivityActionKind.PlayPause);
        viewModel.Dispose();
    }

    [Fact]
    public void RemovedSelectedActivityFallsBackAndClearsWhenEmpty()
    {
        using var tray = new FakeTrayCommandService();
        var store = new LiveActivityStore();
        var viewModel = new WinLiveShellViewModel(
            store,
            new NoOpLiveActivityCommandRouter(),
            new InMemorySettingsStore(new WinLiveSettings()),
            new FakePlacementService(),
            new FakeFullScreenDetector(false),
            tray,
            new WinLiveSettings());

        store.Upsert(MediaActivity("media:first", "First", 100));
        store.Upsert(MediaActivity("media:second", "Second", 10));
        viewModel.SelectActivityCommand.Execute("media:second");

        store.Remove("media:second");

        Assert.Equal("media:first", viewModel.PrimaryActivity?.Id);

        store.Remove("media:first");

        Assert.Null(viewModel.PrimaryActivity);
        Assert.False(viewModel.IsExpanded);
        viewModel.Dispose();
    }

    private static LiveActivity MediaActivity(string id, string title, int priority)
    {
        return new LiveActivity
        {
            Id = id,
            Title = title,
            State = LiveActivityState.Active,
            Priority = priority,
            Actions =
            [
                new LiveActivityActionDescriptor
                {
                    Kind = LiveActivityActionKind.PlayPause,
                    DisplayName = "Play / pause"
                }
            ]
        };
    }


    private sealed class FakeCommandRouter : ILiveActivityCommandRouter
    {
        private readonly HashSet<(string Id, LiveActivityActionKind Action)> _allowed = new();

        public List<(string Id, LiveActivityActionKind Action)> ExecutedActions { get; } = new();

        public void Allow(string id, LiveActivityActionKind action)
        {
            _allowed.Add((id, action));
        }

        public bool CanExecute(string activityId, LiveActivityActionKind action)
        {
            return _allowed.Contains((activityId, action));
        }

        public Task ExecuteAsync(
            string activityId,
            LiveActivityActionKind action,
            CancellationToken cancellationToken = default)
        {
            ExecutedActions.Add((activityId, action));
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySettingsStore : IWinLiveSettingsStore
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

        public WinLiveSettings Current => _settings;
    }

    private sealed class FakePlacementService : ITaskbarPlacementService
    {
        public IslandBounds GetDefaultIslandBounds(IslandBounds preferred)
        {
            return new IslandBounds(100, 100, preferred.Width, preferred.Height);
        }

        public IslandBounds CorrectToVisibleArea(IslandBounds desired) => desired;
    }

    private sealed class FakeFullScreenDetector : IFullScreenDetector
    {
        private readonly bool _isFullScreen;

        public FakeFullScreenDetector(bool isFullScreen)
        {
            _isFullScreen = isFullScreen;
        }

        public bool IsForegroundFullScreen() => _isFullScreen;
    }

    private sealed class FakeTrayCommandService : ITrayCommandService
    {
        public event EventHandler? OpenSettingsRequested
        {
            add { }
            remove { }
        }

        public event EventHandler? ResetPositionRequested
        {
            add { }
            remove { }
        }

        public event EventHandler? ExitRequested
        {
            add { }
            remove { }
        }

        public bool IsVisible { get; private set; }

        public void PublishIslandVisibility(bool isVisible)
        {
            IsVisible = isVisible;
        }

        public void Dispose()
        {
        }
    }
}
