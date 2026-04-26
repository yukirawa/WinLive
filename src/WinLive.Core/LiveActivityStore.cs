namespace WinLive.Core;

public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class LiveActivityStore : ILiveActivityStore
{
    private readonly ISystemClock _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, LiveActivity> _activities = new(StringComparer.Ordinal);

    public LiveActivityStore()
        : this(new SystemClock())
    {
    }

    public LiveActivityStore(ISystemClock clock)
    {
        _clock = clock;
    }

    public event EventHandler<LiveActivityChangedEventArgs>? ActivitiesChanged;

    public IReadOnlyList<LiveActivity> Activities
    {
        get
        {
            lock (_gate)
            {
                return Sort(_activities.Values).ToArray();
            }
        }
    }

    public IReadOnlyList<LiveActivity> VisibleActivities
    {
        get
        {
            lock (_gate)
            {
                return Sort(_activities.Values.Where(item => item.IsVisible)).ToArray();
            }
        }
    }

    public LiveActivity? PrimaryActivity
    {
        get
        {
            lock (_gate)
            {
                return Sort(_activities.Values.Where(item => item.IsVisible)).FirstOrDefault();
            }
        }
    }

    public bool HasVisibleActivities => PrimaryActivity is not null;

    public bool TryGetActivity(string id, out LiveActivity activity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (_gate)
        {
            return _activities.TryGetValue(id, out activity!);
        }
    }

    public LiveActivity Upsert(LiveActivity activity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activity.Id);

        LiveActivity normalized;
        lock (_gate)
        {
            var now = _clock.UtcNow;
            _activities.TryGetValue(activity.Id, out var existing);

            normalized = activity with
            {
                Progress = NormalizeProgress(activity.Progress),
                CreatedAt = existing?.CreatedAt ?? UseDefaultNow(activity.CreatedAt, now),
                UpdatedAt = UseDefaultNow(activity.UpdatedAt, now),
                Actions = activity.Actions.ToArray(),
                Metadata = new Dictionary<string, string>(activity.Metadata, StringComparer.Ordinal)
            };

            _activities[normalized.Id] = normalized;
        }

        PublishChange();
        return normalized;
    }

    public bool Remove(string id, LiveActivityEndReason reason = LiveActivityEndReason.Deleted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        bool removed;
        lock (_gate)
        {
            removed = _activities.Remove(id);
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
            foreach (var activity in _activities.Values.ToArray())
            {
                if (activity.IsEmphasized && activity.EmphasizedUntil <= now)
                {
                    _activities[activity.Id] = activity with
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
        ActivitiesChanged?.Invoke(this, new LiveActivityChangedEventArgs(Activities, PrimaryActivity));
    }

    private static DateTimeOffset UseDefaultNow(DateTimeOffset value, DateTimeOffset now)
    {
        return value == default ? now : value;
    }

    private static double? NormalizeProgress(double? progress)
    {
        return progress is null ? null : Math.Clamp(progress.Value, 0d, 1d);
    }

    private static IOrderedEnumerable<LiveActivity> Sort(IEnumerable<LiveActivity> activities)
    {
        return activities
            .OrderByDescending(item => item.Priority)
            .ThenByDescending(item => item.IsEmphasized)
            .ThenByDescending(item => StateWeight(item.State))
            .ThenByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Title, StringComparer.CurrentCulture);
    }

    private static int StateWeight(LiveActivityState state)
    {
        return state switch
        {
            LiveActivityState.Active => 3,
            LiveActivityState.Paused => 2,
            LiveActivityState.Error => 2,
            LiveActivityState.Completed => 1,
            _ => 0
        };
    }
}

public sealed class NoOpLiveActivityCommandRouter : ILiveActivityCommandRouter
{
    public bool CanExecute(string activityId, LiveActivityActionKind action) => false;

    public Task ExecuteAsync(
        string activityId,
        LiveActivityActionKind action,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
