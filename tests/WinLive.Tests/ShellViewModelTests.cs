using System.Windows;
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
        Assert.Equal(296, settingsStore.Current.IslandBounds.Left);
        Assert.Equal(236, settingsStore.Current.IslandBounds.Top);
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
        Assert.Equal(374, viewModel.ActivityStackWidth);
        Assert.Equal(120, viewModel.ActivityStackHeight);
        Assert.Equal(486, viewModel.WindowWidth);
        Assert.Equal(232, viewModel.WindowHeight);
        Assert.Equal(184, viewModel.WindowLeft);
        Assert.Equal(60, viewModel.WindowTop);
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
        Assert.Equal(320, viewModel.CompactWidth);
        Assert.Equal(50, viewModel.ActivityStackHeight);
        Assert.Equal(432, viewModel.WindowWidth);
        Assert.Equal(162, viewModel.WindowHeight);

        viewModel.IsIslandSizeLarge = true;

        Assert.True(viewModel.IsIslandSizeLarge);
        Assert.Equal(IslandSizePreset.Large, settings.IslandSize);
        Assert.Equal(430, viewModel.CompactWidth);
        Assert.Equal(66, viewModel.ActivityStackHeight);
        Assert.Equal(542, viewModel.WindowWidth);
        Assert.Equal(178, viewModel.WindowHeight);
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
    public void DemoActivityButtonsCanRunMultipleTestActivities()
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

        viewModel.StartDemoDownloadCommand.Execute(null);
        viewModel.StartDemoEncodeCommand.Execute(null);

        Assert.True(viewModel.DemoActivityEnabled);
        Assert.Contains(store.VisibleActivities, item => item.Id == "demo:download");
        Assert.Contains(store.VisibleActivities, item => item.Id == "demo:encode");
        Assert.Equal(2, store.VisibleActivities.Count(item => item.Id.StartsWith("demo:", StringComparison.Ordinal)));

        viewModel.DemoActivityEnabled = false;

        Assert.False(viewModel.DemoActivityEnabled);
        Assert.DoesNotContain(store.Activities, item => item.Id.StartsWith("demo:", StringComparison.Ordinal));
        viewModel.Dispose();
    }

    [Fact]
    public void DemoActivityToggleStartsSelectedTypeWithoutStoppingExistingDemos()
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

        viewModel.StartDemoDownloadCommand.Execute(null);
        viewModel.IsDemoTimer = true;
        viewModel.DemoActivityEnabled = true;

        Assert.Contains(store.VisibleActivities, item => item.Id == "demo:download");
        Assert.Contains(store.VisibleActivities, item => item.Id == "demo:timer");
        viewModel.Dispose();
    }

    [Fact]
    public void DismissDemoActivityRemovesDemoTimerState()
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

        viewModel.StartDemoDownloadCommand.Execute(null);

        Assert.True(viewModel.DismissActivityCommand.CanExecute("demo:download"));

        viewModel.DismissActivityCommand.Execute("demo:download");

        Assert.False(viewModel.DemoActivityEnabled);
        Assert.DoesNotContain(store.VisibleActivities, item => item.Id == "demo:download");
        viewModel.Dispose();
    }

    [Fact]
    public void MediaPrimaryShowsTransportControlsBeforeExpansion()
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

        Assert.False(viewModel.IsExpanded);
        Assert.True(viewModel.ShowMediaTransportControls);
        Assert.True(viewModel.ShowPreviousNextControls);
        Assert.True(viewModel.ShowOpenSourceAppControl);
        Assert.False(viewModel.PreviousCommand.CanExecute(null));
        Assert.False(viewModel.NextCommand.CanExecute(null));
        Assert.False(viewModel.OpenSourceAppCommand.CanExecute(null));
        Assert.False(viewModel.ShowExpandControl);
        Assert.False(viewModel.ToggleExpandCommand.CanExecute(null));
        Assert.Equal(new[] { "media:first" }, viewModel.DisplayedActivities.Select(item => item.Id));
        Assert.Equal(374, viewModel.ActivityStackWidth);
        Assert.Equal(56, viewModel.ActivityStackHeight);
        Assert.Equal(new Thickness(56), viewModel.ActivityStackMargin);
        Assert.Equal(486, viewModel.WindowWidth);
        Assert.Equal(168, viewModel.WindowHeight);
        viewModel.Dispose();
    }

    [Fact]
    public void ExpandedMediaAndDemoActivityStackShowsDemoAboveMediaWhenExpandingUp()
    {
        using var tray = new FakeTrayCommandService();
        var store = new LiveActivityStore();
        var settings = new WinLiveSettings
        {
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

        store.Upsert(MediaActivity("media:first", "First", 100));
        viewModel.StartDemoEncodeCommand.Execute(null);

        viewModel.ToggleExpandCommand.Execute(null);

        Assert.Equal("media:first", viewModel.PrimaryActivity?.Id);
        Assert.Equal(new[] { "demo:encode" }, viewModel.SecondaryActivities.Select(item => item.Id));
        Assert.Equal(new[] { "demo:encode", "media:first" }, viewModel.DisplayedActivities.Select(item => item.Id));
        Assert.True(viewModel.ShowUpSecondaryTiles);
        Assert.False(viewModel.ShowDownSecondaryTiles);
        Assert.Equal(120, viewModel.ActivityStackHeight);
        Assert.Equal(232, viewModel.WindowHeight);
        viewModel.Dispose();
    }

    [Fact]
    public void AddingSecondActivityUsesStorePrimaryAndWaitsForExpandButton()
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

        Assert.Equal("media:second", viewModel.PrimaryActivity?.Id);
        Assert.False(viewModel.IsExpanded);
        Assert.Equal(new[] { "media:second" }, viewModel.DisplayedActivities.Select(item => item.Id));
        Assert.True(viewModel.PrimaryActivity?.IsSelected);
        Assert.True(viewModel.HasSecondaryActivities);
        Assert.True(viewModel.ShowExpandControl);
        Assert.True(viewModel.ToggleExpandCommand.CanExecute(null));
        Assert.False(viewModel.ShowUpSecondaryTiles);
        Assert.False(viewModel.ShowDownSecondaryTiles);

        viewModel.ToggleExpandCommand.Execute(null);

        Assert.True(viewModel.IsExpanded);
        Assert.Contains(viewModel.SecondaryActivities, item => item.Id == "media:first");
        Assert.Equal(new[] { "media:first", "media:second" }, viewModel.DisplayedActivities.Select(item => item.Id));
        Assert.True(viewModel.ShowExpandControl);
        Assert.DoesNotContain(viewModel.SecondaryActivities, item => item.IsSelected);
        viewModel.Dispose();
    }

    [Fact]
    public void ExpandedListIncludesAllVisibleSecondaryActivities()
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

        store.Upsert(ProgressActivity("primary", "Primary", 100, 0.1));
        store.Upsert(ProgressActivity("secondary-a", "Secondary A", 40, 0.1));
        store.Upsert(ProgressActivity("secondary-b", "Secondary B", 30, 0.1));
        store.Upsert(ProgressActivity("secondary-c", "Secondary C", 20, 0.1));
        store.Upsert(ProgressActivity("secondary-d", "Secondary D", 10, 0.1));

        viewModel.ToggleExpandCommand.Execute(null);

        Assert.Equal(
            new[] { "secondary-a", "secondary-b", "secondary-c", "secondary-d" },
            viewModel.SecondaryActivities.Select(item => item.Id));
        Assert.Equal(
            new[] { "secondary-a", "secondary-b", "secondary-c", "secondary-d", "primary" },
            viewModel.DisplayedActivities.Select(item => item.Id));
        Assert.True(viewModel.IsExpanded);
        Assert.Equal(424, viewModel.WindowHeight);
        Assert.Equal(312, viewModel.ActivityStackHeight);
        viewModel.Dispose();
    }

    [Fact]
    public void DownExpansionDisplaysPrimaryBeforeSecondaryActivities()
    {
        using var tray = new FakeTrayCommandService();
        var store = new LiveActivityStore();
        var settings = new WinLiveSettings
        {
            ExpansionDirection = IslandExpansionDirection.Down
        };
        var viewModel = new WinLiveShellViewModel(
            store,
            new NoOpLiveActivityCommandRouter(),
            new InMemorySettingsStore(settings),
            new FakePlacementService(),
            new FakeFullScreenDetector(false),
            tray,
            settings);

        store.Upsert(ProgressActivity("primary", "Primary", 100, 0.1));
        store.Upsert(ProgressActivity("secondary", "Secondary", 10, 0.1));

        viewModel.ToggleExpandCommand.Execute(null);

        Assert.Equal(new[] { "primary", "secondary" }, viewModel.DisplayedActivities.Select(item => item.Id));
        Assert.False(viewModel.ShowUpSecondaryTiles);
        Assert.True(viewModel.ShowDownSecondaryTiles);
        Assert.Equal(120, viewModel.ActivityStackHeight);
        Assert.Equal(232, viewModel.WindowHeight);
        viewModel.Dispose();
    }

    [Fact]
    public async Task ExpandedActivityCommandsTargetTileWithoutSwitchingPrimary()
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

        viewModel.ToggleExpandCommand.Execute(null);

        Assert.Equal("media:first", viewModel.PrimaryActivity?.Id);
        Assert.True(viewModel.IsExpanded);
        Assert.True(viewModel.PrimaryActivity?.IsSelected);
        Assert.Contains(viewModel.SecondaryActivities, item => item.Id == "media:second" && !item.IsSelected);
        Assert.Equal(new[] { "media:second", "media:first" }, viewModel.DisplayedActivities.Select(item => item.Id));
        Assert.True(viewModel.PlayPauseActivityCommand.CanExecute("media:second"));

        viewModel.PlayPauseActivityCommand.Execute("media:second");
        await Task.Yield();

        Assert.Equal("media:first", viewModel.PrimaryActivity?.Id);
        Assert.Contains(
            router.ExecutedActions,
            item => item.Id == "media:second" && item.Action == LiveActivityActionKind.PlayPause);
        viewModel.Dispose();
    }

    [Fact]
    public async Task ActivityMediaCommandsTargetTileWithoutSwitchingPrimary()
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

        var actions = new[]
        {
            LiveActivityActionKind.Previous,
            LiveActivityActionKind.PlayPause,
            LiveActivityActionKind.Next,
            LiveActivityActionKind.OpenSourceApp
        };
        store.Upsert(MediaActivity("media:first", "First", 100, actions));
        store.Upsert(MediaActivity("media:second", "Second", 10, actions));
        foreach (var action in actions)
        {
            router.Allow("media:second", action);
        }

        Assert.Equal("media:first", viewModel.PrimaryActivity?.Id);
        Assert.True(viewModel.PreviousActivityCommand.CanExecute("media:second"));
        Assert.True(viewModel.PlayPauseActivityCommand.CanExecute("media:second"));
        Assert.True(viewModel.NextActivityCommand.CanExecute("media:second"));
        Assert.True(viewModel.OpenSourceAppActivityCommand.CanExecute("media:second"));

        viewModel.PreviousActivityCommand.Execute("media:second");
        viewModel.PlayPauseActivityCommand.Execute("media:second");
        viewModel.NextActivityCommand.Execute("media:second");
        viewModel.OpenSourceAppActivityCommand.Execute("media:second");
        await Task.Yield();

        Assert.Equal("media:first", viewModel.PrimaryActivity?.Id);
        foreach (var action in actions)
        {
            Assert.Contains(
                router.ExecutedActions,
                item => item.Id == "media:second" && item.Action == action);
        }
        viewModel.Dispose();
    }

    [Fact]
    public void ActivityMediaCommandsDisableUnsupportedActions()
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
        router.Allow("media:first", LiveActivityActionKind.PlayPause);

        Assert.True(viewModel.PlayPauseActivityCommand.CanExecute("media:first"));
        Assert.False(viewModel.PreviousActivityCommand.CanExecute("media:first"));
        Assert.False(viewModel.NextActivityCommand.CanExecute("media:first"));
        Assert.False(viewModel.OpenSourceAppActivityCommand.CanExecute("media:first"));
        viewModel.Dispose();
    }

    [Fact]
    public void RemovedSecondaryActivityKeepsPrimaryAndCollapsesOnlyWhenSingleActivityRemains()
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
        store.Upsert(MediaActivity("media:third", "Third", 5));
        viewModel.ToggleExpandCommand.Execute(null);

        store.Remove("media:second");

        Assert.Equal("media:first", viewModel.PrimaryActivity?.Id);
        Assert.True(viewModel.IsExpanded);

        store.Remove("media:third");

        Assert.Equal("media:first", viewModel.PrimaryActivity?.Id);
        Assert.False(viewModel.IsExpanded);

        store.Remove("media:first");

        Assert.Null(viewModel.PrimaryActivity);
        Assert.False(viewModel.IsExpanded);
        viewModel.Dispose();
    }

    [Fact]
    public void UpdatingSecondaryActivityReusesTileAndDoesNotStealPrimary()
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
            Id = "download:first",
            Type = LiveActivityType.Download,
            Title = "First",
            State = LiveActivityState.Active,
            Priority = 100,
            Progress = 0.1
        });
        store.Upsert(new LiveActivity
        {
            Id = "download:second",
            Type = LiveActivityType.Download,
            Title = "Second",
            State = LiveActivityState.Active,
            Priority = 10,
            Progress = 0.1
        });
        var primaryBefore = viewModel.PrimaryActivity;
        var secondaryBefore = viewModel.SecondaryActivities.Single(item => item.Id == "download:second");

        store.Upsert(new LiveActivity
        {
            Id = "download:second",
            Type = LiveActivityType.Download,
            Title = "Second updated",
            State = LiveActivityState.Active,
            Priority = 10,
            Progress = 0.8,
            IsEmphasized = true,
            EmphasizedUntil = DateTimeOffset.UtcNow.AddSeconds(2)
        });

        Assert.Same(primaryBefore, viewModel.PrimaryActivity);
        Assert.Equal("download:first", viewModel.PrimaryActivity?.Id);
        Assert.True(viewModel.PrimaryActivity?.IsSelected);
        Assert.Same(secondaryBefore, viewModel.SecondaryActivities.Single(item => item.Id == "download:second"));
        Assert.Equal("Second updated", secondaryBefore.Title);
        Assert.Equal(0.8, secondaryBefore.Progress);
        Assert.True(secondaryBefore.IsEmphasized);
        viewModel.Dispose();
    }

    [Fact]
    public void FrequentSecondaryUpdatesKeepSecondaryRelativeOrderStable()
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

        store.Upsert(ProgressActivity("primary", "Primary", 100, 0.1));
        store.Upsert(ProgressActivity("secondary-a", "Secondary A", 40, 0.1));
        store.Upsert(ProgressActivity("secondary-b", "Secondary B", 30, 0.1));
        var originalOrder = viewModel.SecondaryActivities.Select(item => item.Id).ToArray();

        for (var index = 0; index < 5; index++)
        {
            store.Upsert(ProgressActivity("secondary-b", $"Secondary B {index}", 30, 0.2 + index * 0.1));
        }

        Assert.Equal(originalOrder, viewModel.SecondaryActivities.Select(item => item.Id));
        Assert.Equal("primary", viewModel.PrimaryActivity?.Id);
        Assert.False(viewModel.IsExpanded);
        viewModel.Dispose();
    }

    private static LiveActivity MediaActivity(
        string id,
        string title,
        int priority,
        params LiveActivityActionKind[] actions)
    {
        var actionKinds = actions.Length == 0
            ? new[] { LiveActivityActionKind.PlayPause }
            : actions;
        return new LiveActivity
        {
            Id = id,
            Type = LiveActivityType.Media,
            Title = title,
            State = LiveActivityState.Active,
            Priority = priority,
            Actions = actionKinds
                .Select(action => new LiveActivityActionDescriptor
                {
                    Kind = action,
                    DisplayName = action.ToString()
                })
                .ToArray()
        };
    }

    private static LiveActivity ProgressActivity(string id, string title, int priority, double progress)
    {
        return new LiveActivity
        {
            Id = id,
            Type = LiveActivityType.Download,
            Title = title,
            State = LiveActivityState.Active,
            Priority = priority,
            Progress = progress
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
