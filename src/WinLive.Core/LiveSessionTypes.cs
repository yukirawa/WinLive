namespace WinLive.Core;

public enum LiveSessionType
{
    Music,
    Download,
    Upload,
    Encode,
    FileCopy,
    Timer,
    GenericProgress,
    Custom
}

public enum LiveSessionState
{
    Active,
    Paused,
    Completed,
    Stopped,
    Error,
    Hidden
}

public enum LiveSessionActionKind
{
    Play,
    Pause,
    PlayPause,
    Next,
    Previous,
    OpenSourceApp
}

public enum LiveSessionEndReason
{
    Completed,
    Stopped,
    SourceClosed,
    Deleted,
    Error
}

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public enum IslandSizePreference
{
    Compact,
    Medium,
    Large
}

public enum IslandClickBehavior
{
    ToggleExpanded,
    CyclePrimarySession,
    OpenSourceApp
}
