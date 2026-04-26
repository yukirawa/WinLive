using System.Runtime.CompilerServices;
using Windows.Media.Control;
using Windows.Storage.Streams;
using WinLive.Core;

namespace WinLive.Windows;

public sealed class WindowsMediaActivitySource : ILiveActivitySource, ILiveActivityCommandRouter
{
    private static readonly TimeSpan TrackChangeEmphasisDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MediaPropertiesTimeout = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan AlbumArtTimeout = TimeSpan.FromMilliseconds(750);

    private readonly ILiveActivityStore _store;
    private readonly WinLiveSettings _settings;
    private readonly ISourceAppLauncher _sourceAppLauncher;
    private readonly ISystemClock _clock;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> _mediaSessions =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _sessionActivityIds = new();
    private readonly HashSet<int> _attachedSessionKeys = new();
    private readonly Dictionary<string, string> _trackKeys = new(StringComparer.Ordinal);
    private readonly MediaMetadataCache _metadataCache = new();

    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    public WindowsMediaActivitySource(
        ILiveActivityStore store,
        WinLiveSettings settings,
        ISourceAppLauncher? sourceAppLauncher = null,
        ISystemClock? clock = null)
    {
        _store = store;
        _settings = settings;
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

    public bool CanExecute(string activityId, LiveActivityActionKind action)
    {
        if (!_mediaSessions.TryGetValue(activityId, out var session))
        {
            return false;
        }

        if (action == LiveActivityActionKind.OpenSourceApp)
        {
            return _sourceAppLauncher.CanLaunch(session.SourceAppUserModelId);
        }

        GlobalSystemMediaTransportControlsSessionPlaybackControls controls;
        try
        {
            controls = session.GetPlaybackInfo().Controls;
        }
        catch
        {
            return false;
        }

        return action switch
        {
            LiveActivityActionKind.Play => controls.IsPlayEnabled,
            LiveActivityActionKind.Pause => controls.IsPauseEnabled,
            LiveActivityActionKind.PlayPause => controls.IsPlayEnabled || controls.IsPauseEnabled,
            LiveActivityActionKind.Next => controls.IsNextEnabled,
            LiveActivityActionKind.Previous => controls.IsPreviousEnabled,
            _ => false
        };
    }

    public async Task ExecuteAsync(
        string activityId,
        LiveActivityActionKind action,
        CancellationToken cancellationToken = default)
    {
        if (!_mediaSessions.TryGetValue(activityId, out var session) || !CanExecute(activityId, action))
        {
            return;
        }

        switch (action)
        {
            case LiveActivityActionKind.Play:
                await session.TryPlayAsync();
                break;
            case LiveActivityActionKind.Pause:
                await session.TryPauseAsync();
                break;
            case LiveActivityActionKind.PlayPause:
                await ExecutePlayPauseAsync(session).ConfigureAwait(false);
                break;
            case LiveActivityActionKind.Next:
                await session.TrySkipNextAsync();
                break;
            case LiveActivityActionKind.Previous:
                await session.TrySkipPreviousAsync();
                break;
            case LiveActivityActionKind.OpenSourceApp:
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
                _store.Remove(id, LiveActivityEndReason.SourceClosed);
            }

            _mediaSessions.Clear();
            _sessionActivityIds.Clear();
            _attachedSessionKeys.Clear();
            _trackKeys.Clear();
            _metadataCache.Clear();
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
            var sessions = _manager.GetSessions().ToList();
            var identityInputs = sessions
                .Select(session =>
                {
                    var sessionKey = GetSessionInstanceKey(session);
                    return new MediaSessionIdentityInput(
                        sessionKey,
                        MediaActivityIdentity.SourceKey(session.SourceAppUserModelId, sessionKey));
                })
                .ToArray();
            var sessionActivityIds = MediaActivityIdentity.BuildActivityIds(
                identityInputs,
                _sessionActivityIds);
            var activeIds = new HashSet<string>(StringComparer.Ordinal);

            _sessionActivityIds.Clear();

            foreach (var session in sessions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sessionKey = GetSessionInstanceKey(session);
                var id = sessionActivityIds[sessionKey];
                activeIds.Add(id);
                _sessionActivityIds[sessionKey] = id;

                _mediaSessions[id] = session;
                if (_attachedSessionKeys.Add(sessionKey))
                {
                    AttachSessionEvents(session);
                }

                await UpsertSessionAsync(id, session).ConfigureAwait(false);
            }

            foreach (var staleId in _mediaSessions.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            {
                _mediaSessions.Remove(staleId);
                _trackKeys.Remove(staleId);
                _metadataCache.Remove(staleId);
                _store.Remove(staleId, LiveActivityEndReason.SourceClosed);
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
            var sessionKey = GetSessionInstanceKey(session);
            await _refreshGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_sessionActivityIds.TryGetValue(sessionKey, out var id) ||
                    !_mediaSessions.ContainsKey(id))
                {
                    return;
                }

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
        GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo;
        try
        {
            playbackInfo = session.GetPlaybackInfo();
        }
        catch
        {
            RemoveSessionActivity(id, LiveActivityEndReason.SourceClosed);
            return;
        }

        var state = MapPlaybackState(playbackInfo.PlaybackStatus);

        if (state == LiveActivityState.Stopped ||
            state == LiveActivityState.Hidden ||
            (state == LiveActivityState.Paused && !_settings.ShowPausedMedia))
        {
            RemoveSessionActivity(id, LiveActivityEndReason.Stopped);
            return;
        }

        var mediaProperties = await TryReadMediaPropertiesAsync(session).ConfigureAwait(false);
        var freshAlbumArtBytes = _settings.ShowAlbumArt && !string.IsNullOrWhiteSpace(mediaProperties?.Title)
            ? await ReadAlbumArtAsync(mediaProperties.Thumbnail).ConfigureAwait(false)
            : null;

        if (!_metadataCache.TryResolve(
            id,
            mediaProperties?.Title,
            mediaProperties?.Artist,
            mediaProperties?.AlbumTitle,
            freshAlbumArtBytes,
            HasUsableTransportControls(playbackInfo),
            out var metadata))
        {
            RemoveSessionActivity(id, LiveActivityEndReason.Stopped);
            return;
        }

        var timeline = TryGetTimelineProperties(session);
        var duration = timeline is not null && timeline.EndTime > timeline.StartTime
            ? timeline.EndTime - timeline.StartTime
            : null as TimeSpan?;
        var position = duration is not null && timeline is not null
            ? timeline.Position - timeline.StartTime
            : null as TimeSpan?;
        var progress = position is null || duration is null || duration.Value.TotalSeconds <= 0
            ? null as double?
            : position.Value.TotalSeconds / duration.Value.TotalSeconds;
        var trackKey = $"{metadata.Title}\n{metadata.Artist}\n{metadata.Album}\n{duration}";
        var isTrackChange = _trackKeys.TryGetValue(id, out var previousKey) && previousKey != trackKey;
        _trackKeys[id] = trackKey;

        _store.TryGetActivity(id, out var existing);
        var now = _clock.UtcNow;

        _store.Upsert(new LiveActivity
        {
            Id = id,
            Type = LiveActivityType.Media,
            Title = metadata.Title,
            Subtitle = metadata.Artist,
            State = state,
            Progress = progress,
            SourceApp = new LiveActivitySourceApp
            {
                Name = session.SourceAppUserModelId,
                AppUserModelId = session.SourceAppUserModelId
            },
            Actions = BuildActions(session, playbackInfo, state),
            Priority = 100,
            CreatedAt = existing?.CreatedAt ?? default,
            UpdatedAt = default,
            IsEmphasized = isTrackChange || existing?.IsEmphasized == true,
            EmphasizedUntil = isTrackChange
                ? now.Add(TrackChangeEmphasisDuration)
                : existing?.EmphasizedUntil,
            Media = new LiveActivityMediaInfo
            {
                Artist = metadata.Artist,
                Album = metadata.Album,
                AlbumArtBytes = _settings.ShowAlbumArt ? metadata.AlbumArtBytes : null,
                Position = position,
                Duration = duration
            },
            Metadata = new Dictionary<string, string>
            {
                ["playbackStatus"] = playbackInfo.PlaybackStatus.ToString()
            }
        });
    }

    private void RemoveSessionActivity(string id, LiveActivityEndReason reason)
    {
        _trackKeys.Remove(id);
        _metadataCache.Remove(id);
        _store.Remove(id, reason);
    }

    private IReadOnlyList<LiveActivityActionDescriptor> BuildActions(
        GlobalSystemMediaTransportControlsSession session,
        GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo,
        LiveActivityState state)
    {
        var controls = playbackInfo.Controls;
        var actions = new List<LiveActivityActionDescriptor>();

        if (controls.IsPlayEnabled || controls.IsPauseEnabled)
        {
            actions.Add(new LiveActivityActionDescriptor
            {
                Kind = LiveActivityActionKind.PlayPause,
                DisplayName = state == LiveActivityState.Paused ? "Play" : "Pause"
            });
        }

        if (controls.IsPreviousEnabled)
        {
            actions.Add(new LiveActivityActionDescriptor
            {
                Kind = LiveActivityActionKind.Previous,
                DisplayName = "Previous"
            });
        }

        if (controls.IsNextEnabled)
        {
            actions.Add(new LiveActivityActionDescriptor
            {
                Kind = LiveActivityActionKind.Next,
                DisplayName = "Next"
            });
        }

        if (_sourceAppLauncher.CanLaunch(session.SourceAppUserModelId))
        {
            actions.Add(new LiveActivityActionDescriptor
            {
                Kind = LiveActivityActionKind.OpenSourceApp,
                DisplayName = "Open"
            });
        }

        return actions;
    }

    private static LiveActivityState MapPlaybackState(
        GlobalSystemMediaTransportControlsSessionPlaybackStatus status)
    {
        return status switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => LiveActivityState.Active,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => LiveActivityState.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => LiveActivityState.Stopped,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed => LiveActivityState.Stopped,
            _ => LiveActivityState.Hidden
        };
    }

    private static int GetSessionInstanceKey(GlobalSystemMediaTransportControlsSession session)
    {
        return RuntimeHelpers.GetHashCode(session);
    }

    private static async Task<GlobalSystemMediaTransportControlsSessionMediaProperties?> TryReadMediaPropertiesAsync(
        GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            return await session.TryGetMediaPropertiesAsync()
                .AsTask()
                .WaitAsync(MediaPropertiesTimeout)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static GlobalSystemMediaTransportControlsSessionTimelineProperties? TryGetTimelineProperties(
        GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            return session.GetTimelineProperties();
        }
        catch
        {
            return null;
        }
    }

    private static bool HasUsableTransportControls(GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo)
    {
        var controls = playbackInfo.Controls;
        return controls.IsPlayEnabled ||
            controls.IsPauseEnabled ||
            controls.IsNextEnabled ||
            controls.IsPreviousEnabled;
    }

    private static async Task<byte[]?> ReadAlbumArtAsync(IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null)
        {
            return null;
        }

        try
        {
            using var stream = await thumbnail.OpenReadAsync()
                .AsTask()
                .WaitAsync(AlbumArtTimeout)
                .ConfigureAwait(false);
            if (stream.Size == 0 || stream.Size > 5 * 1024 * 1024)
            {
                return null;
            }

            var bytes = new byte[stream.Size];
            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)stream.Size)
                .AsTask()
                .WaitAsync(AlbumArtTimeout)
                .ConfigureAwait(false);
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch
        {
            return null;
        }
    }
}
