using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WinLive.Core;

namespace WinLive.App;

public sealed class WinLiveShellViewModel : ObservableObject, IDisposable
{
    private const double TileGap = 8;
    private const int MaxSecondaryTiles = 3;
    private static readonly LiveActivityType[] DemoActivityTypes =
    [
        LiveActivityType.Download,
        LiveActivityType.Encode,
        LiveActivityType.Timer
    ];
    private static readonly IslandSizeMetrics SmallMetrics = new(320, 50, 20, 8, 32, 12, 12, 10, 8);
    private static readonly IslandSizeMetrics MediumMetrics = new(374, 56, 22, 9, 34, 12, 13, 11, 9);
    private static readonly IslandSizeMetrics LargeMetrics = new(430, 66, 26, 11, 42, 15, 15, 12, 10);

    private readonly ILiveActivityStore _activityStore;
    private readonly ILiveActivityCommandRouter _commandRouter;
    private readonly IWinLiveSettingsStore _settingsStore;
    private readonly ITaskbarPlacementService _placementService;
    private readonly IFullScreenDetector _fullScreenDetector;
    private readonly ITrayCommandService _trayCommandService;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly DispatcherTimer _heartbeatTimer;
    private WinLiveSettings _settings;
    private LiveActivityViewModel? _primaryActivity;
    private string? _selectedActivityId;
    private bool _isExpanded;
    private bool _isFullScreenSuppressed;
    private bool? _lastLoggedSuppression;
    private IslandBounds _windowBounds;
    private string _settingsStatus = string.Empty;
    private readonly DispatcherTimer _demoActivityTimer;
    private string? _demoActivityId;
    private LiveActivityType _demoActivityType;
    private DateTimeOffset _demoActivityStartedAt;
    private TimeSpan _demoActivityDuration;
    private bool _isDisposed;

    public WinLiveShellViewModel(
        ILiveActivityStore activityStore,
        ILiveActivityCommandRouter commandRouter,
        IWinLiveSettingsStore settingsStore,
        ITaskbarPlacementService placementService,
        IFullScreenDetector fullScreenDetector,
        ITrayCommandService trayCommandService,
        WinLiveSettings settings)
    {
        _activityStore = activityStore;
        _commandRouter = commandRouter;
        _settingsStore = settingsStore;
        _placementService = placementService;
        _fullScreenDetector = fullScreenDetector;
        _trayCommandService = trayCommandService;
        _settings = settings;
        _settings.Normalize();
        _synchronizationContext = SynchronizationContext.Current;
        _windowBounds = _placementService.GetDefaultIslandBounds(
            new IslandBounds(0, 0, CompactWidth, CompactHeight));

        Activities = new ObservableCollection<LiveActivityViewModel>();
        SecondaryActivities = new ObservableCollection<LiveActivityViewModel>();
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded, () => PrimaryActivity is not null);
        SelectActivityCommand = new RelayCommand(SelectActivity, CanSelectActivity);
        PlayPauseCommand = new AsyncRelayCommand(
            (_, token) => ExecutePrimaryActionAsync(LiveActivityActionKind.PlayPause, token),
            _ => CanExecutePrimaryAction(LiveActivityActionKind.PlayPause));
        PreviousCommand = new AsyncRelayCommand(
            (_, token) => ExecutePrimaryActionAsync(LiveActivityActionKind.Previous, token),
            _ => CanExecutePrimaryAction(LiveActivityActionKind.Previous));
        NextCommand = new AsyncRelayCommand(
            (_, token) => ExecutePrimaryActionAsync(LiveActivityActionKind.Next, token),
            _ => CanExecutePrimaryAction(LiveActivityActionKind.Next));
        OpenSourceAppCommand = new AsyncRelayCommand(
            (_, token) => ExecutePrimaryActionAsync(LiveActivityActionKind.OpenSourceApp, token),
            _ => CanExecutePrimaryAction(LiveActivityActionKind.OpenSourceApp));
        DismissPrimaryCommand = new RelayCommand(DismissPrimary, () => PrimaryActivity is not null);
        OpenSettingsCommand = new RelayCommand(() => SettingsRequested?.Invoke(this, EventArgs.Empty));
        ResetPositionCommand = new AsyncRelayCommand(ResetPositionAsync);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        RegenerateTokenCommand = new RelayCommand(RegenerateToken);
        StartDemoDownloadCommand = new RelayCommand(() => StartDemoActivity(LiveActivityType.Download));
        StartDemoEncodeCommand = new RelayCommand(() => StartDemoActivity(LiveActivityType.Encode));
        StartDemoTimerCommand = new RelayCommand(() => StartDemoActivity(LiveActivityType.Timer));
        StopDemoActivityCommand = new RelayCommand(StopDemoActivity, () => _demoActivityId is not null);

        _activityStore.ActivitiesChanged += OnActivitiesChanged;
        _trayCommandService.OpenSettingsRequested += OnOpenSettingsRequested;
        _trayCommandService.ResetPositionRequested += OnResetPositionRequested;
        _trayCommandService.ExitRequested += OnExitRequested;

        _heartbeatTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _heartbeatTimer.Tick += OnHeartbeat;
        _heartbeatTimer.Start();

        _demoActivityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _demoActivityTimer.Tick += OnDemoActivityTick;

        RefreshFromStore();
        RefreshSuppression();
        RefreshWindowBounds();
    }

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public ObservableCollection<LiveActivityViewModel> Activities { get; }

    public ObservableCollection<LiveActivityViewModel> SecondaryActivities { get; }

    public LiveActivityViewModel? PrimaryActivity
    {
        get => _primaryActivity;
        private set
        {
            if (SetProperty(ref _primaryActivity, value))
            {
                OnPropertyChanged(nameof(IsIslandVisible));
                OnPropertyChanged(nameof(PlayPauseGlyph));
                OnPropertyChanged(nameof(HasSecondaryActivities));
                OnPropertyChanged(nameof(ShowUpSecondaryTiles));
                OnPropertyChanged(nameof(ShowDownSecondaryTiles));
                OnPropertyChanged(nameof(ShowMediaTransportControls));
                OnPropertyChanged(nameof(ShowPreviousNextControls));
                OnPropertyChanged(nameof(ShowOpenSourceAppControl));
                OnPropertyChanged(nameof(ShowDismissControl));
                RaiseCommandCanExecuteChanged();
            }
        }
    }

    public bool IsIslandVisible => PrimaryActivity is not null && !IsFullScreenSuppressed;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(ExpandGlyph));
                OnPropertyChanged(nameof(ShowUpSecondaryTiles));
                OnPropertyChanged(nameof(ShowDownSecondaryTiles));
                OnPropertyChanged(nameof(ShowPreviousNextControls));
                RefreshWindowBounds();
            }
        }
    }

    public bool IsFullScreenSuppressed
    {
        get => _isFullScreenSuppressed;
        private set
        {
            if (SetProperty(ref _isFullScreenSuppressed, value))
            {
                OnPropertyChanged(nameof(IsIslandVisible));
                _trayCommandService.PublishIslandVisibility(IsIslandVisible);
            }
        }
    }

    public double WindowLeft
    {
        get => _windowBounds.Left;
        private set => SetWindowBounds(_windowBounds with { Left = value });
    }

    public double WindowTop
    {
        get => _windowBounds.Top;
        private set => SetWindowBounds(_windowBounds with { Top = value });
    }

    public double WindowWidth
    {
        get => _windowBounds.Width;
        private set => SetWindowBounds(_windowBounds with { Width = value });
    }

    public double WindowHeight
    {
        get => _windowBounds.Height;
        private set => SetWindowBounds(_windowBounds with { Height = value });
    }

    public double CompactWidth => CurrentMetrics.Width;

    public double CompactHeight => CurrentMetrics.Height;

    public double IslandTileHeight => CurrentMetrics.Height;

    public CornerRadius IslandTileCornerRadius => new(CurrentMetrics.CornerRadius);

    public double IslandTilePadding => CurrentMetrics.Padding;

    public double ActivityIconSize => CurrentMetrics.IconSize;

    public CornerRadius ActivityIconCornerRadius => new(CurrentMetrics.IconCornerRadius);

    public double ActivityTitleFontSize => CurrentMetrics.TitleFontSize;

    public double ActivitySubtitleFontSize => CurrentMetrics.SubtitleFontSize;

    public double ActivityTypeFontSize => CurrentMetrics.TypeFontSize;

    public string PlayPauseGlyph => PrimaryActivity?.State == LiveActivityState.Paused
        ? "\uE768"
        : "\uE769";

    public string ExpandGlyph => IsExpanded ? "\uE70E" : "\uE70D";

    public bool HasSecondaryActivities => SecondaryActivities.Count > 0;

    public bool ShowUpSecondaryTiles =>
        IsExpanded &&
        HasSecondaryActivities &&
        _settings.ExpansionDirection == IslandExpansionDirection.Up;

    public bool ShowDownSecondaryTiles =>
        IsExpanded &&
        HasSecondaryActivities &&
        _settings.ExpansionDirection == IslandExpansionDirection.Down;

    public bool ShowMediaTransportControls => PrimaryActivity?.Type == LiveActivityType.Media;

    public bool ShowPreviousNextControls => IsExpanded && ShowMediaTransportControls;

    public bool ShowOpenSourceAppControl =>
        PrimaryActivity?.SupportsAction(LiveActivityActionKind.OpenSourceApp) == true &&
        _commandRouter.CanExecute(PrimaryActivity.Id, LiveActivityActionKind.OpenSourceApp);

    public bool ShowDismissControl => PrimaryActivity is not null && !ShowMediaTransportControls;

    public bool IsExpansionUp
    {
        get => _settings.ExpansionDirection == IslandExpansionDirection.Up;
        set
        {
            if (value)
            {
                SetExpansionDirection(IslandExpansionDirection.Up);
            }
        }
    }

    public bool IsExpansionDown
    {
        get => _settings.ExpansionDirection == IslandExpansionDirection.Down;
        set
        {
            if (value)
            {
                SetExpansionDirection(IslandExpansionDirection.Down);
            }
        }
    }

    public bool IsIslandSizeSmall
    {
        get => _settings.IslandSize == IslandSizePreset.Small;
        set
        {
            if (value)
            {
                SetIslandSize(IslandSizePreset.Small);
            }
        }
    }

    public bool IsIslandSizeMedium
    {
        get => _settings.IslandSize == IslandSizePreset.Medium;
        set
        {
            if (value)
            {
                SetIslandSize(IslandSizePreset.Medium);
            }
        }
    }

    public bool IsIslandSizeLarge
    {
        get => _settings.IslandSize == IslandSizePreset.Large;
        set
        {
            if (value)
            {
                SetIslandSize(IslandSizePreset.Large);
            }
        }
    }

    public string ApiBaseAddress => $"http://{_settings.ExternalApi.Host}:{_settings.ExternalApi.Port}";

    public string ApiToken
    {
        get => _settings.ExternalApi.AuthToken;
        set
        {
            if (_settings.ExternalApi.AuthToken != value)
            {
                _settings.ExternalApi.AuthToken = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ExternalApiEnabled
    {
        get => _settings.ExternalApi.Enabled;
        set
        {
            if (_settings.ExternalApi.Enabled != value)
            {
                _settings.ExternalApi.Enabled = value;
                OnPropertyChanged();
            }
        }
    }

    public int ApiPort
    {
        get => _settings.ExternalApi.Port;
        set
        {
            if (_settings.ExternalApi.Port != value)
            {
                _settings.ExternalApi.Port = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ApiBaseAddress));
            }
        }
    }

    public bool HideDuringFullScreen
    {
        get => _settings.HideDuringFullScreen;
        set
        {
            if (_settings.HideDuringFullScreen != value)
            {
                _settings.HideDuringFullScreen = value;
                OnPropertyChanged();
                RefreshSuppression();
            }
        }
    }

    public bool ShowPausedMedia
    {
        get => _settings.ShowPausedMedia;
        set
        {
            if (_settings.ShowPausedMedia != value)
            {
                _settings.ShowPausedMedia = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowAlbumArt
    {
        get => _settings.ShowAlbumArt;
        set
        {
            if (_settings.ShowAlbumArt != value)
            {
                _settings.ShowAlbumArt = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ExperimentalProgressEnabled
    {
        get => _settings.ExperimentalProgress.Enabled;
        set
        {
            if (_settings.ExperimentalProgress.Enabled != value)
            {
                _settings.ExperimentalProgress.Enabled = value;
                OnPropertyChanged();
            }
        }
    }

    public string SettingsPath => _settingsStore.SettingsPath;

    public string SettingsStatus
    {
        get => _settingsStatus;
        private set => SetProperty(ref _settingsStatus, value);
    }

    public ICommand ToggleExpandCommand { get; }

    public ICommand SelectActivityCommand { get; }

    public ICommand PlayPauseCommand { get; }

    public ICommand PreviousCommand { get; }

    public ICommand NextCommand { get; }

    public ICommand OpenSourceAppCommand { get; }

    public ICommand DismissPrimaryCommand { get; }

    public ICommand OpenSettingsCommand { get; }

    public ICommand ResetPositionCommand { get; }

    public ICommand SaveSettingsCommand { get; }

    public ICommand RegenerateTokenCommand { get; }

    public ICommand StartDemoDownloadCommand { get; }

    public ICommand StartDemoEncodeCommand { get; }

    public ICommand StartDemoTimerCommand { get; }

    public ICommand StopDemoActivityCommand { get; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _heartbeatTimer.Stop();
        _heartbeatTimer.Tick -= OnHeartbeat;
        _demoActivityTimer.Stop();
        _demoActivityTimer.Tick -= OnDemoActivityTick;
        _activityStore.ActivitiesChanged -= OnActivitiesChanged;
        _trayCommandService.OpenSettingsRequested -= OnOpenSettingsRequested;
        _trayCommandService.ResetPositionRequested -= OnResetPositionRequested;
        _trayCommandService.ExitRequested -= OnExitRequested;
        _isDisposed = true;
    }

    private void OnActivitiesChanged(object? sender, LiveActivityChangedEventArgs args)
    {
        RunOnUiContext(RefreshFromStore);
    }

    private void RefreshFromStore()
    {
        var visibleActivities = _activityStore.VisibleActivities.ToList();
        var previousVisibleCount = Activities.Count;
        Activities.Clear();
        foreach (var activity in visibleActivities)
        {
            Activities.Add(new LiveActivityViewModel(activity));
        }

        if (visibleActivities.Count == 0)
        {
            _selectedActivityId = null;
            PrimaryActivity = null;
        }
        else
        {
            var selectedActivity = visibleActivities.FirstOrDefault(activity => activity.Id == _selectedActivityId) ??
                _activityStore.PrimaryActivity ??
                visibleActivities[0];
            _selectedActivityId = selectedActivity.Id;
            PrimaryActivity = new LiveActivityViewModel(selectedActivity);
        }

        SecondaryActivities.Clear();
        foreach (var activity in visibleActivities
            .Where(activity => activity.Id != PrimaryActivity?.Id)
            .Take(MaxSecondaryTiles))
        {
            SecondaryActivities.Add(new LiveActivityViewModel(activity));
        }

        if (PrimaryActivity is null)
        {
            IsExpanded = false;
        }
        else if (previousVisibleCount <= 1 && visibleActivities.Count > 1)
        {
            IsExpanded = true;
        }

        OnPropertyChanged(nameof(HasSecondaryActivities));
        OnPropertyChanged(nameof(ShowUpSecondaryTiles));
        OnPropertyChanged(nameof(ShowDownSecondaryTiles));
        WinLiveDiagnostics.Write(
            $"RefreshFromStore activities={Activities.Count} primary='{PrimaryActivity?.Title}' visible={IsIslandVisible} suppressed={IsFullScreenSuppressed}");
        RefreshWindowBounds();
        _trayCommandService.PublishIslandVisibility(IsIslandVisible);
        RaiseCommandCanExecuteChanged();
    }

    private void RefreshWindowBounds()
    {
        var secondaryCount = IsExpanded ? Math.Min(SecondaryActivities.Count, MaxSecondaryTiles) : 0;
        var width = CompactWidth;
        var height = CompactHeight + secondaryCount * (CompactHeight + TileGap);
        var compactAnchor = _settings.HasCustomIslandPosition
            ? _placementService.CorrectToVisibleArea(
                new IslandBounds(
                    _settings.IslandBounds.Left,
                    _settings.IslandBounds.Top,
                    CompactWidth,
                    CompactHeight))
            : _placementService.GetDefaultIslandBounds(new IslandBounds(0, 0, CompactWidth, CompactHeight));
        var top = compactAnchor.Top;
        if (IsExpanded && _settings.ExpansionDirection == IslandExpansionDirection.Up)
        {
            top -= height - CompactHeight;
        }

        var next = _placementService.CorrectToVisibleArea(
            new IslandBounds(compactAnchor.Left, top, width, height));
        SetWindowBounds(next);
    }

    private void SetWindowBounds(IslandBounds next)
    {
        if (_windowBounds.Equals(next))
        {
            return;
        }

        _windowBounds = next;
        WinLiveDiagnostics.Write(
            $"WindowBounds left={next.Left:0.##} top={next.Top:0.##} width={next.Width:0.##} height={next.Height:0.##}");
        OnPropertyChanged(nameof(WindowLeft));
        OnPropertyChanged(nameof(WindowTop));
        OnPropertyChanged(nameof(WindowWidth));
        OnPropertyChanged(nameof(WindowHeight));
    }

    private void OnHeartbeat(object? sender, EventArgs e)
    {
        RefreshSuppression();
        if (_activityStore.ClearExpiredEmphasis(DateTimeOffset.UtcNow) > 0)
        {
            RefreshFromStore();
        }
    }

    private void RefreshSuppression()
    {
        IsFullScreenSuppressed = _settings.HideDuringFullScreen &&
            _fullScreenDetector.IsForegroundFullScreen();
        if (_lastLoggedSuppression != IsFullScreenSuppressed)
        {
            _lastLoggedSuppression = IsFullScreenSuppressed;
            WinLiveDiagnostics.Write($"RefreshSuppression suppressed={IsFullScreenSuppressed}");
        }
    }

    private bool CanExecutePrimaryAction(LiveActivityActionKind action)
    {
        return PrimaryActivity is not null &&
            PrimaryActivity.SupportsAction(action) &&
            _commandRouter.CanExecute(PrimaryActivity.Id, action);
    }

    private async Task ExecutePrimaryActionAsync(
        LiveActivityActionKind action,
        CancellationToken cancellationToken)
    {
        if (PrimaryActivity is null || !CanExecutePrimaryAction(action))
        {
            return;
        }

        await _commandRouter.ExecuteAsync(PrimaryActivity.Id, action, cancellationToken)
            .ConfigureAwait(false);
    }

    private void DismissPrimary()
    {
        if (PrimaryActivity is not null)
        {
            _activityStore.Remove(PrimaryActivity.Id, LiveActivityEndReason.Deleted);
        }
    }

    private bool CanSelectActivity(object? parameter)
    {
        var id = GetActivityIdParameter(parameter);
        return id is not null &&
            Activities.Any(activity => activity.Id == id);
    }

    private void SelectActivity(object? parameter)
    {
        var id = GetActivityIdParameter(parameter);
        if (id is null || id == _selectedActivityId || !Activities.Any(activity => activity.Id == id))
        {
            return;
        }

        _selectedActivityId = id;
        RefreshFromStore();
    }

    private async Task ResetPositionAsync(CancellationToken cancellationToken)
    {
        _settings.HasCustomIslandPosition = false;
        _settings.IslandBounds = IslandBounds.Default;
        RefreshWindowBounds();
        await _settingsStore.SaveAsync(_settings, cancellationToken).ConfigureAwait(false);
    }

    public async Task CommitDraggedPositionAsync(
        double left,
        double top,
        CancellationToken cancellationToken = default)
    {
        var anchorTop = IsExpanded && _settings.ExpansionDirection == IslandExpansionDirection.Up
            ? top + Math.Max(0, _windowBounds.Height - CompactHeight)
            : top;
        var corrected = _placementService.CorrectToVisibleArea(
            new IslandBounds(left, anchorTop, CompactWidth, CompactHeight));
        _settings.HasCustomIslandPosition = true;
        _settings.IslandBounds = corrected;
        RefreshWindowBounds();
        await _settingsStore.SaveAsync(_settings, cancellationToken).ConfigureAwait(false);
    }

    private void SetExpansionDirection(IslandExpansionDirection direction)
    {
        if (_settings.ExpansionDirection == direction)
        {
            return;
        }

        _settings.ExpansionDirection = direction;
        OnPropertyChanged(nameof(IsExpansionUp));
        OnPropertyChanged(nameof(IsExpansionDown));
        OnPropertyChanged(nameof(ShowUpSecondaryTiles));
        OnPropertyChanged(nameof(ShowDownSecondaryTiles));
        RefreshWindowBounds();
    }

    private void SetIslandSize(IslandSizePreset size)
    {
        if (_settings.IslandSize == size)
        {
            return;
        }

        _settings.IslandSize = size;
        NotifyIslandSizeChanged();
        RefreshWindowBounds();
    }

    private void NotifyIslandSizeChanged()
    {
        OnPropertyChanged(nameof(IsIslandSizeSmall));
        OnPropertyChanged(nameof(IsIslandSizeMedium));
        OnPropertyChanged(nameof(IsIslandSizeLarge));
        OnPropertyChanged(nameof(CompactWidth));
        OnPropertyChanged(nameof(CompactHeight));
        OnPropertyChanged(nameof(IslandTileHeight));
        OnPropertyChanged(nameof(IslandTileCornerRadius));
        OnPropertyChanged(nameof(IslandTilePadding));
        OnPropertyChanged(nameof(ActivityIconSize));
        OnPropertyChanged(nameof(ActivityIconCornerRadius));
        OnPropertyChanged(nameof(ActivityTitleFontSize));
        OnPropertyChanged(nameof(ActivitySubtitleFontSize));
        OnPropertyChanged(nameof(ActivityTypeFontSize));
    }

    private IslandSizeMetrics CurrentMetrics => _settings.IslandSize switch
    {
        IslandSizePreset.Small => SmallMetrics,
        IslandSizePreset.Large => LargeMetrics,
        _ => MediumMetrics
    };

    private void StartDemoActivity(LiveActivityType type)
    {
        StopDemoActivity();

        _demoActivityType = type;
        _demoActivityId = DemoActivityId(type);
        _demoActivityStartedAt = DateTimeOffset.UtcNow;
        _demoActivityDuration = type == LiveActivityType.Timer
            ? TimeSpan.FromSeconds(30)
            : TimeSpan.FromSeconds(18);

        UpsertDemoActivity(0);
        _demoActivityTimer.Start();
        RaiseCommandCanExecuteChanged();
        SettingsStatus = "デモ activity を開始しました。島で音楽以外の進行状況を確認できます。";
    }

    private void StopDemoActivity()
    {
        _demoActivityTimer.Stop();
        foreach (var id in DemoActivityTypes.Select(DemoActivityId))
        {
            _activityStore.Remove(id, LiveActivityEndReason.Deleted);
        }

        _demoActivityId = null;
        RaiseCommandCanExecuteChanged();
    }

    private void OnDemoActivityTick(object? sender, EventArgs e)
    {
        if (_demoActivityId is null)
        {
            return;
        }

        var elapsed = DateTimeOffset.UtcNow - _demoActivityStartedAt;
        var progress = Math.Clamp(elapsed.TotalSeconds / _demoActivityDuration.TotalSeconds, 0d, 1d);
        UpsertDemoActivity(progress);

        if (progress >= 1)
        {
            _demoActivityTimer.Stop();
            RaiseCommandCanExecuteChanged();
        }
    }

    private void UpsertDemoActivity(double progress)
    {
        if (_demoActivityId is null)
        {
            return;
        }

        var isDone = progress >= 1;
        var percent = Math.Round(progress * 100);
        var (title, subtitle) = _demoActivityType switch
        {
            LiveActivityType.Encode => (
                isDone ? "Demo encode complete" : "Demo encode",
                isDone ? "Ready to review" : $"{percent:0}% rendered"),
            LiveActivityType.Timer => (
                isDone ? "Demo timer finished" : "Demo timer",
                isDone ? "Done" : $"{Math.Max(0, (_demoActivityDuration - (DateTimeOffset.UtcNow - _demoActivityStartedAt)).TotalSeconds):0}s left"),
            _ => (
                isDone ? "Demo download complete" : "Demo download",
                isDone ? "Saved locally" : $"{percent:0}% downloaded")
        };

        _activityStore.Upsert(new LiveActivity
        {
            Id = _demoActivityId,
            Type = _demoActivityType,
            State = isDone ? LiveActivityState.Completed : LiveActivityState.Active,
            Title = title,
            Subtitle = subtitle,
            Progress = progress,
            Priority = 60,
            SourceApp = new LiveActivitySourceApp
            {
                Name = "WinLive demo"
            },
            Metadata = new Dictionary<string, string>
            {
                ["demo"] = "settingsWindow"
            }
        });
    }

    private static string DemoActivityId(LiveActivityType type)
    {
        return $"demo:{type.ToString().ToLowerInvariant()}";
    }

    private async Task SaveSettingsAsync(CancellationToken cancellationToken)
    {
        _settings.Normalize();
        await _settingsStore.SaveAsync(_settings, cancellationToken).ConfigureAwait(false);
        OnPropertyChanged(nameof(ApiToken));
        OnPropertyChanged(nameof(ApiPort));
        OnPropertyChanged(nameof(ApiBaseAddress));
        SettingsStatus = "保存しました。API サーバーまたは実験的検出の変更を反映するには WinLive を再起動してください。";
    }

    private void RegenerateToken()
    {
        ApiToken = TokenGenerator.CreateToken();
        SettingsStatus = "新しいトークンを生成しました。";
    }

    private void OnOpenSettingsRequested(object? sender, EventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void OnResetPositionRequested(object? sender, EventArgs e)
    {
        await ResetPositionAsync(CancellationToken.None).ConfigureAwait(true);
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseCommandCanExecuteChanged()
    {
        foreach (var command in new[]
        {
            ToggleExpandCommand,
            SelectActivityCommand,
            PlayPauseCommand,
            PreviousCommand,
            NextCommand,
            OpenSourceAppCommand,
            DismissPrimaryCommand,
            ResetPositionCommand,
            SaveSettingsCommand,
            StopDemoActivityCommand
        }.OfType<IRaiseCanExecuteChanged>())
        {
            command.RaiseCanExecuteChanged();
        }
    }

    private static string? GetActivityIdParameter(object? parameter)
    {
        return parameter switch
        {
            LiveActivityViewModel viewModel => viewModel.Id,
            LiveActivity activity => activity.Id,
            string id when !string.IsNullOrWhiteSpace(id) => id,
            _ => null
        };
    }

    private void RunOnUiContext(Action action)
    {
        if (_synchronizationContext is null || SynchronizationContext.Current == _synchronizationContext)
        {
            action();
            return;
        }

        _synchronizationContext.Post(_ => action(), null);
    }

    private sealed record IslandSizeMetrics(
        double Width,
        double Height,
        double CornerRadius,
        double Padding,
        double IconSize,
        double IconCornerRadius,
        double TitleFontSize,
        double SubtitleFontSize,
        double TypeFontSize);
}
