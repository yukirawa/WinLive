using Windows.Media.Control;
using Windows.Storage.Streams;
using WinLive.Core;

namespace WinLive.Windows;

public sealed class WindowsMediaSessionSource : ILiveSessionCommandRouter, IAsyncDisposable
{
    private static readonly TimeSpan TrackChangeEmphasisDuration = TimeSpan.FromSeconds(3);

    private readonly ILiveSessionStore _store;
    private readonly WinLiveSettings _settings;
    private readonly ISourceAppLauncher _sourceAppLauncher;
    private readonly ISystemClock _clock;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> _mediaSessions =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _trackKeys = new(StringComparer.Ordinal);

    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    public WindowsMediaSessionSource(
        ILiveSessionStore store,
        WinLiveSettings? settings = null,
        ISourceAppLauncher? sourceAppLauncher = null,
        ISystemClock? clock = null)
    {
        _store = store;
        _settings = settings ?? new WinLiveSettings();
        _sourceAppLauncher = sourceAppLauncher ?? new WindowsSourceAppLauncher();
        _clock = clock ?? new SystemClock();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _settings.Normalize();
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += OnSessionsChanged;
        await RefreshSessionsAsync(cancellationToken).ConfigureAwait(false);
    }

    public bool CanExecute(string sessionId, LiveSessionActionKind action)
    {
        if (!_mediaSessions.TryGetValue(sessionId, out var session))
        {
            return false;
        }

        if (action == LiveSessionActionKind.OpenSourceApp)
        {
            return _sourceAppLauncher.CanLaunch(session.SourceAppUserModelId);
        }

        var controls = session.GetPlaybackInfo().Controls;
        return action switch
        {
            LiveSessionActionKind.Play => controls.IsPlayEnabled,
            LiveSessionActionKind.Pause => controls.IsPauseEnabled,
            LiveSessionActionKind.PlayPause => controls.IsPlayEnabled || controls.IsPauseEnabled,
            LiveSessionActionKind.Next => controls.IsNextEnabled,
            LiveSessionActionKind.Previous => controls.IsPreviousEnabled,
            _ => false
        };
    }

    public async Task ExecuteAsync(
        string sessionId,
        LiveSessionActionKind action,
        CancellationToken cancellationToken = default)
    {
        if (!_mediaSessions.TryGetValue(sessionId, out var session) || !CanExecute(sessionId, action))
        {
            return;
        }

        switch (action)
        {
            case LiveSessionActionKind.Play:
                await session.TryPlayAsync();
                break;
            case LiveSessionActionKind.Pause:
                await session.TryPauseAsync();
                break;
            case LiveSessionActionKind.PlayPause:
                await ExecutePlayPauseAsync(session).ConfigureAwait(false);
                break;
            case LiveSessionActionKind.Next:
                await session.TrySkipNextAsync();
                break;
            case LiveSessionActionKind.Previous:
                await session.TrySkipPreviousAsync();
                break;
            case LiveSessionActionKind.OpenSourceApp:
                await _sourceAppLauncher.LaunchAsync(session.SourceAppUserModelId, cancellationToken)
                    .ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_manager is not null)
        {
            _manager.SessionsChanged -= OnSessionsChanged;
        }

        await _refreshGate.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var id in _mediaSessions.Keys.ToArray())
            {
                _store.Remove(id, LiveSessionEndReason.SourceClosed);
            }

            _mediaSessions.Clear();
            _trackKeys.Clear();
        }
        finally
        {
            _refreshGate.Release();
            _refreshGate.Dispose();
        }
    }

    private async Task ExecutePlayPauseAsync(GlobalSystemMediaTransportControlsSession session)
    {
        var playbackInfo = session.GetPlaybackInfo();
        if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused &&
            playbackInfo.Controls.IsPlayEnabled)
        {
            await session.TryPlayAsync();
            return;
        }

        if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing &&
            playbackInfo.Controls.IsPauseEnabled)
        {
            await session.TryPauseAsync();
            return;
        }

        await session.TryTogglePlayPauseAsync();
    }

    private async void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args)
    {
        await RefreshSessionsAsync().ConfigureAwait(false);
    }

    private async Task RefreshSessionsAsync(CancellationToken cancellationToken = default)
    {
        if (_manager is null)
        {
            return;
        }

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sessions = _manager.GetSessions();
            var activeIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var session in sessions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = GetSessionId(session);
                activeIds.Add(id);

                if (!_mediaSessions.ContainsKey(id))
                {
                    _mediaSessions[id] = session;
                    AttachSessionEvents(session);
                }

                await UpsertSessionAsync(id, session).ConfigureAwait(false);
            }

            foreach (var staleId in _mediaSessions.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            {
                _mediaSessions.Remove(staleId);
                _trackKeys.Remove(staleId);
                _store.Remove(staleId, LiveSessionEndReason.SourceClosed);
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void AttachSessionEvents(GlobalSystemMediaTransportControlsSession session)
    {
        session.MediaPropertiesChanged += (_, _) => QueueSessionRefresh(session);
        session.PlaybackInfoChanged += (_, _) => QueueSessionRefresh(session);
        session.TimelinePropertiesChanged += (_, _) => QueueSessionRefresh(session);
    }

    private void QueueSessionRefresh(GlobalSystemMediaTransportControlsSession session)
    {
        _ = Task.Run(async () =>
        {
            var id = GetSessionId(session);
            await _refreshGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await UpsertSessionAsync(id, session).ConfigureAwait(false);
            }
            finally
            {
                _refreshGate.Release();
            }
        });
    }

    private async Task UpsertSessionAsync(
        string id,
        GlobalSystemMediaTransportControlsSession session)
    {
        var playbackInfo = session.GetPlaybackInfo();
        var state = MapPlaybackState(playbackInfo.PlaybackStatus);

        if (state == LiveSessionState.Stopped ||
            state == LiveSessionState.Hidden ||
            (state == LiveSessionState.Paused && !_settings.ShowPausedMusic))
        {
            _store.Remove(id, LiveSessionEndReason.Stopped);
            return;
        }

        var mediaProperties = await session.TryGetMediaPropertiesAsync();
        var timeline = session.GetTimelineProperties();
        var title = string.IsNullOrWhiteSpace(mediaProperties.Title)
            ? "Unknown track"
            : mediaProperties.Title;
        var artist = string.IsNullOrWhiteSpace(mediaProperties.Artist)
            ? null
            : mediaProperties.Artist;
        var album = string.IsNullOrWhiteSpace(mediaProperties.AlbumTitle)
            ? null
            : mediaProperties.AlbumTitle;

        var duration = timeline.EndTime > timeline.StartTime
            ? timeline.EndTime - timeline.StartTime
            : null as TimeSpan?;
        var trackKey = $"{title}\n{artist}\n{album}\n{duration}";
        var isTrackChange = _trackKeys.TryGetValue(id, out var previousKey) && previousKey != trackKey;
        _trackKeys[id] = trackKey;

        _store.TryGetSession(id, out var existing);
        var now = _clock.UtcNow;

        var liveSession = new LiveSession
        {
            Id = id,
            Type = LiveSessionType.Music,
            Title = title,
            Subtitle = artist,
            State = state,
            AppName = session.SourceAppUserModelId,
            SourceAppUserModelId = session.SourceAppUserModelId,
            Actions = BuildActions(session, state),
            Priority = 100,
            CreatedAt = existing?.CreatedAt ?? default,
            UpdatedAt = default,
            IsEmphasized = isTrackChange || existing?.IsEmphasized == true,
            EmphasizedUntil = isTrackChange
                ? now.Add(TrackChangeEmphasisDuration)
                : existing?.EmphasizedUntil,
            Media = new LiveSessionMediaInfo
            {
                Artist = artist,
                Album = album,
                AlbumArtBytes = _settings.ShowAlbumArt
                    ? await ReadAlbumArtAsync(mediaProperties.Thumbnail).ConfigureAwait(false)
                    : null,
                Position = timeline.Position,
                Duration = duration
            },
            Metadata = new Dictionary<string, string>
            {
                ["playbackStatus"] = playbackInfo.PlaybackStatus.ToString()
            }
        };

        _store.Upsert(liveSession);
    }

    private IReadOnlyList<LiveSessionActionDescriptor> BuildActions(
        GlobalSystemMediaTransportControlsSession session,
        LiveSessionState state)
    {
        var controls = session.GetPlaybackInfo().Controls;
        var actions = new List<LiveSessionActionDescriptor>();

        if (controls.IsPlayEnabled || controls.IsPauseEnabled)
        {
            actions.Add(new LiveSessionActionDescriptor
            {
                Kind = LiveSessionActionKind.PlayPause,
                DisplayName = state == LiveSessionState.Paused ? "Play" : "Pause",
                IsEnabled = true
            });
        }

        if (controls.IsNextEnabled)
        {
            actions.Add(new LiveSessionActionDescriptor
            {
                Kind = LiveSessionActionKind.Next,
                DisplayName = "Next",
                IsEnabled = true
            });
        }

        if (controls.IsPreviousEnabled)
        {
            actions.Add(new LiveSessionActionDescriptor
            {
                Kind = LiveSessionActionKind.Previous,
                DisplayName = "Previous",
                IsEnabled = true
            });
        }

        if (_sourceAppLauncher.CanLaunch(session.SourceAppUserModelId))
        {
            actions.Add(new LiveSessionActionDescriptor
            {
                Kind = LiveSessionActionKind.OpenSourceApp,
                DisplayName = "Open",
                IsEnabled = true
            });
        }

        return actions;
    }

    private static LiveSessionState MapPlaybackState(
        GlobalSystemMediaTransportControlsSessionPlaybackStatus status)
    {
        return status switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => LiveSessionState.Active,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => LiveSessionState.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => LiveSessionState.Stopped,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed => LiveSessionState.Stopped,
            _ => LiveSessionState.Hidden
        };
    }

    private static string GetSessionId(GlobalSystemMediaTransportControlsSession session)
    {
        var source = string.IsNullOrWhiteSpace(session.SourceAppUserModelId)
            ? session.GetHashCode().ToString("X")
            : session.SourceAppUserModelId;
        return $"music:{source}";
    }

    private static async Task<byte[]?> ReadAlbumArtAsync(IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null)
        {
            return null;
        }

        try
        {
            using var stream = await thumbnail.OpenReadAsync();
            if (stream.Size == 0 || stream.Size > 5 * 1024 * 1024)
            {
                return null;
            }

            var bytes = new byte[stream.Size];
            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch
        {
            return null;
        }
    }
}
