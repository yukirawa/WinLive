using System.Collections.ObjectModel;
using System.Windows.Input;
using WinLive.Core;

namespace WinLive.Presentation;

public sealed class WinLiveShellViewModel : ObservableObject, IDisposable
{
    private readonly ILiveSessionStore _sessionStore;
    private readonly ILiveSessionCommandRouter _commandRouter;
    private readonly IWinLiveSettingsStore _settingsStore;
    private readonly IIslandPositionService _positionService;
    private readonly IFullScreenDetector _fullScreenDetector;
    private readonly ITrayCommandService? _trayCommandService;
    private readonly SynchronizationContext? _synchronizationContext;
    private WinLiveSettings _settings;
    private bool _isExpanded;
    private LiveSessionViewModel? _primarySession;
    private IslandBounds _islandBounds;
    private bool _isFullScreenSuppressed;
    private bool _isDisposed;

    private WinLiveShellViewModel(
        ILiveSessionStore sessionStore,
        ILiveSessionCommandRouter commandRouter,
        IWinLiveSettingsStore settingsStore,
        IIslandPositionService positionService,
        IFullScreenDetector fullScreenDetector,
        WinLiveSettings settings,
        ITrayCommandService? trayCommandService)
    {
        _sessionStore = sessionStore;
        _commandRouter = commandRouter;
        _settingsStore = settingsStore;
        _positionService = positionService;
        _fullScreenDetector = fullScreenDetector;
        _settings = settings;
        _trayCommandService = trayCommandService;
        _synchronizationContext = SynchronizationContext.Current;
        _islandBounds = _positionService.CorrectToVisibleArea(settings.IslandBounds);

        Sessions = new ObservableCollection<LiveSessionViewModel>();
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded, () => PrimarySession is not null);
        ResetPositionCommand = new AsyncRelayCommand(ResetPositionAsync);
        PlayPauseCommand = new AsyncRelayCommand(
            (_, token) => ExecutePrimaryActionAsync(LiveSessionActionKind.PlayPause, token),
            _ => CanExecutePrimaryAction(LiveSessionActionKind.PlayPause));
        NextCommand = new AsyncRelayCommand(
            (_, token) => ExecutePrimaryActionAsync(LiveSessionActionKind.Next, token),
            _ => CanExecutePrimaryAction(LiveSessionActionKind.Next));
        PreviousCommand = new AsyncRelayCommand(
            (_, token) => ExecutePrimaryActionAsync(LiveSessionActionKind.Previous, token),
            _ => CanExecutePrimaryAction(LiveSessionActionKind.Previous));
        OpenSourceAppCommand = new AsyncRelayCommand(
            (_, token) => ExecutePrimaryActionAsync(LiveSessionActionKind.OpenSourceApp, token),
            _ => CanExecutePrimaryAction(LiveSessionActionKind.OpenSourceApp));
        UpdateSettingsCommand = new AsyncRelayCommand(UpdateSettingsAsync);

        _sessionStore.SessionsChanged += OnSessionsChanged;
        _trayCommandService?.PublishIslandVisibility(IsIslandVisible);
        RefreshFromStore();
        RefreshFullScreenSuppression();
    }

    public static async Task<WinLiveShellViewModel> CreateAsync(
        ILiveSessionStore sessionStore,
        ILiveSessionCommandRouter commandRouter,
        IWinLiveSettingsStore settingsStore,
        IIslandPositionService positionService,
        IFullScreenDetector fullScreenDetector,
        ITrayCommandService? trayCommandService = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return Create(
            sessionStore,
            commandRouter,
            settingsStore,
            positionService,
            fullScreenDetector,
            settings,
            trayCommandService);
    }

    public static WinLiveShellViewModel Create(
        ILiveSessionStore sessionStore,
        ILiveSessionCommandRouter commandRouter,
        IWinLiveSettingsStore settingsStore,
        IIslandPositionService positionService,
        IFullScreenDetector fullScreenDetector,
        WinLiveSettings settings,
        ITrayCommandService? trayCommandService = null)
    {
        settings.Normalize();
        settings.IslandBounds = positionService.CorrectToVisibleArea(settings.IslandBounds);
        return new WinLiveShellViewModel(
            sessionStore,
            commandRouter,
            settingsStore,
            positionService,
            fullScreenDetector,
            settings,
            trayCommandService);
    }

    public ObservableCollection<LiveSessionViewModel> Sessions { get; }

    public LiveSessionViewModel? PrimarySession
    {
        get => _primarySession;
        private set
        {
            if (SetProperty(ref _primarySession, value))
            {
                OnPropertyChanged(nameof(IsIslandVisible));
                RaiseCommandCanExecuteChanged();
            }
        }
    }

    public bool IsIslandVisible => PrimarySession is not null && !IsFullScreenSuppressed;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public IslandBounds IslandBounds
    {
        get => _islandBounds;
        set
        {
            var corrected = _positionService.CorrectToVisibleArea(value);
            if (SetProperty(ref _islandBounds, corrected))
            {
                _settings.IslandBounds = corrected;
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
                _trayCommandService?.PublishIslandVisibility(IsIslandVisible);
            }
        }
    }

    public ICommand ToggleExpandCommand { get; }

    public ICommand ResetPositionCommand { get; }

    public ICommand PlayPauseCommand { get; }

    public ICommand NextCommand { get; }

    public ICommand PreviousCommand { get; }

    public ICommand OpenSourceAppCommand { get; }

    public ICommand UpdateSettingsCommand { get; }

    public void RefreshFullScreenSuppression()
    {
        IsFullScreenSuppressed = _settings.HideDuringFullScreen &&
            _fullScreenDetector.IsForegroundFullScreen();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _sessionStore.SessionsChanged -= OnSessionsChanged;
        _isDisposed = true;
    }

    private void OnSessionsChanged(object? sender, LiveSessionChangedEventArgs args)
    {
        RunOnUiContext(RefreshFromStore);
    }

    private void RefreshFromStore()
    {
        Sessions.Clear();
        foreach (var session in _sessionStore.VisibleSessions)
        {
            Sessions.Add(new LiveSessionViewModel(session));
        }

        PrimarySession = _sessionStore.PrimarySession is null
            ? null
            : new LiveSessionViewModel(_sessionStore.PrimarySession);

        if (PrimarySession is null)
        {
            IsExpanded = false;
        }

        OnPropertyChanged(nameof(IsIslandVisible));
        _trayCommandService?.PublishIslandVisibility(IsIslandVisible);
        RaiseCommandCanExecuteChanged();
    }

    private async Task ResetPositionAsync(CancellationToken cancellationToken)
    {
        IslandBounds = _positionService.CorrectToVisibleArea(IslandBounds.Default);
        await _settingsStore.SaveAsync(_settings, cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateSettingsAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (parameter is WinLiveSettings incoming)
        {
            incoming.Normalize();
            _settings = incoming;
            IslandBounds = incoming.IslandBounds;
        }

        _settings.IslandBounds = IslandBounds;
        await _settingsStore.SaveAsync(_settings, cancellationToken).ConfigureAwait(false);
        RefreshFullScreenSuppression();
    }

    private bool CanExecutePrimaryAction(LiveSessionActionKind action)
    {
        return PrimarySession is not null &&
            PrimarySession.SupportsAction(action) &&
            _commandRouter.CanExecute(PrimarySession.Id, action);
    }

    private async Task ExecutePrimaryActionAsync(
        LiveSessionActionKind action,
        CancellationToken cancellationToken)
    {
        if (PrimarySession is null || !CanExecutePrimaryAction(action))
        {
            return;
        }

        await _commandRouter.ExecuteAsync(PrimarySession.Id, action, cancellationToken)
            .ConfigureAwait(false);
    }

    private void RaiseCommandCanExecuteChanged()
    {
        foreach (var command in new[]
        {
            ToggleExpandCommand,
            PlayPauseCommand,
            NextCommand,
            PreviousCommand,
            OpenSourceAppCommand,
            ResetPositionCommand,
            UpdateSettingsCommand
        }.OfType<IRaiseCanExecuteChanged>())
        {
            command.RaiseCanExecuteChanged();
        }
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
}
