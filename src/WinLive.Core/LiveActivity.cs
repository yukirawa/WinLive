namespace WinLive.Core;

public sealed record LiveActivityActionDescriptor
{
    public required LiveActivityActionKind Kind { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public bool IsEnabled { get; init; } = true;
}

public sealed record LiveActivityMediaInfo
{
    public string? Artist { get; init; }

    public string? Album { get; init; }

    public byte[]? AlbumArtBytes { get; init; }

    public TimeSpan? Position { get; init; }

    public TimeSpan? Duration { get; init; }
}

public sealed record LiveActivitySourceApp
{
    public string? Name { get; init; }

    public string? AppUserModelId { get; init; }

    public int? ProcessId { get; init; }
}

public sealed record LiveActivity
{
    public required string Id { get; init; }

    public LiveActivityType Type { get; init; } = LiveActivityType.Custom;

    public LiveActivityState State { get; init; } = LiveActivityState.Active;

    public string Title { get; init; } = string.Empty;

    public string? Subtitle { get; init; }

    public double? Progress { get; init; }

    public int Priority { get; init; }

    public string? Icon { get; init; }

    public LiveActivitySourceApp? SourceApp { get; init; }

    public LiveActivityMediaInfo? Media { get; init; }

    public IReadOnlyList<LiveActivityActionDescriptor> Actions { get; init; } =
        Array.Empty<LiveActivityActionDescriptor>();

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public bool IsEmphasized { get; init; }

    public DateTimeOffset? EmphasizedUntil { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    public bool IsVisible => State is not LiveActivityState.Hidden and not LiveActivityState.Stopped;

    public bool SupportsAction(LiveActivityActionKind action)
    {
        return Actions.Any(item => item.Kind == action && item.IsEnabled);
    }
}

public sealed class LiveActivityChangedEventArgs : EventArgs
{
    public LiveActivityChangedEventArgs(
        IReadOnlyList<LiveActivity> activities,
        LiveActivity? primaryActivity)
    {
        Activities = activities;
        PrimaryActivity = primaryActivity;
    }

    public IReadOnlyList<LiveActivity> Activities { get; }

    public LiveActivity? PrimaryActivity { get; }
}
