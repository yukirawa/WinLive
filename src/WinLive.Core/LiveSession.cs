namespace WinLive.Core;

public sealed record LiveSessionActionDescriptor
{
    public required LiveSessionActionKind Kind { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public bool IsEnabled { get; init; } = true;
}

public sealed record LiveSessionMediaInfo
{
    public string? Artist { get; init; }

    public string? Album { get; init; }

    public byte[]? AlbumArtBytes { get; init; }

    public TimeSpan? Position { get; init; }

    public TimeSpan? Duration { get; init; }
}

public sealed record LiveSession
{
    public required string Id { get; init; }

    public LiveSessionType Type { get; init; } = LiveSessionType.Custom;

    public string Title { get; init; } = string.Empty;

    public string? Subtitle { get; init; }

    public LiveSessionState State { get; init; } = LiveSessionState.Active;

    public double? Progress { get; init; }

    public string? Icon { get; init; }

    public string? AppName { get; init; }

    public string? SourceAppUserModelId { get; init; }

    public IReadOnlyList<LiveSessionActionDescriptor> Actions { get; init; } =
        Array.Empty<LiveSessionActionDescriptor>();

    public int Priority { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public bool IsEmphasized { get; init; }

    public DateTimeOffset? EmphasizedUntil { get; init; }

    public LiveSessionMediaInfo? Media { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    public bool IsVisible => State is not LiveSessionState.Hidden and not LiveSessionState.Stopped;

    public bool SupportsAction(LiveSessionActionKind action)
    {
        return Actions.Any(item => item.Kind == action && item.IsEnabled);
    }
}

public sealed class LiveSessionChangedEventArgs : EventArgs
{
    public LiveSessionChangedEventArgs(IReadOnlyList<LiveSession> sessions, LiveSession? primarySession)
    {
        Sessions = sessions;
        PrimarySession = primarySession;
    }

    public IReadOnlyList<LiveSession> Sessions { get; }

    public LiveSession? PrimarySession { get; }
}
