namespace WinLive.Core;

public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class LiveSessionStore : ILiveSessionStore
{
    private readonly ISystemClock _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, LiveSession> _sessions = new(StringComparer.Ordinal);

    public LiveSessionStore()
        : this(new SystemClock())
    {
    }

    public LiveSessionStore(ISystemClock clock)
    {
        _clock = clock;
    }

    public event EventHandler<LiveSessionChangedEventArgs>? SessionsChanged;

    public IReadOnlyList<LiveSession> Sessions
    {
        get
        {
            lock (_gate)
            {
                return Sort(_sessions.Values).ToArray();
            }
        }
    }

    public IReadOnlyList<LiveSession> VisibleSessions
    {
        get
        {
            lock (_gate)
            {
                return Sort(_sessions.Values.Where(item => item.IsVisible)).ToArray();
            }
        }
    }

    public LiveSession? PrimarySession
    {
        get
        {
            lock (_gate)
            {
                return Sort(_sessions.Values.Where(item => item.IsVisible)).FirstOrDefault();
            }
        }
    }

    public bool HasVisibleSessions => PrimarySession is not null;

    public bool TryGetSession(string id, out LiveSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (_gate)
        {
            return _sessions.TryGetValue(id, out session!);
        }
    }

    public LiveSession Upsert(LiveSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(session.Id);

        LiveSession normalized;
        lock (_gate)
        {
            var now = _clock.UtcNow;
            _sessions.TryGetValue(session.Id, out var existing);

            normalized = session with
            {
                Progress = NormalizeProgress(session.Progress),
                CreatedAt = existing?.CreatedAt ?? UseDefaultNow(session.CreatedAt, now),
                UpdatedAt = UseDefaultNow(session.UpdatedAt, now),
                Actions = session.Actions.ToArray(),
                Metadata = new Dictionary<string, string>(session.Metadata, StringComparer.Ordinal)
            };

            _sessions[normalized.Id] = normalized;
        }

        PublishChange();
        return normalized;
    }

    public bool Remove(string id, LiveSessionEndReason reason = LiveSessionEndReason.Deleted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        bool removed;
        lock (_gate)
        {
            removed = _sessions.Remove(id);
        }

        if (removed)
        {
            PublishChange();
        }

        return removed;
    }

    public int ClearExpiredEmphasis(DateTimeOffset now)
    {
        var changed = 0;
        lock (_gate)
        {
            foreach (var session in _sessions.Values.ToArray())
            {
                if (session.IsEmphasized && session.EmphasizedUntil <= now)
                {
                    _sessions[session.Id] = session with
                    {
                        IsEmphasized = false,
                        EmphasizedUntil = null
                    };
                    changed++;
                }
            }
        }

        if (changed > 0)
        {
            PublishChange();
        }

        return changed;
    }

    private void PublishChange()
    {
        SessionsChanged?.Invoke(this, new LiveSessionChangedEventArgs(Sessions, PrimarySession));
    }

    private static DateTimeOffset UseDefaultNow(DateTimeOffset value, DateTimeOffset now)
    {
        return value == default ? now : value;
    }

    private static double? NormalizeProgress(double? progress)
    {
        return progress is null ? null : Math.Clamp(progress.Value, 0d, 1d);
    }

    private static IOrderedEnumerable<LiveSession> Sort(IEnumerable<LiveSession> sessions)
    {
        return sessions
            .OrderByDescending(item => item.Priority)
            .ThenByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Title, StringComparer.CurrentCulture);
    }
}

public sealed class NoOpLiveSessionCommandRouter : ILiveSessionCommandRouter
{
    public bool CanExecute(string sessionId, LiveSessionActionKind action) => false;

    public Task ExecuteAsync(
        string sessionId,
        LiveSessionActionKind action,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
