namespace WinLive.Core;

public enum LiveActivityType
{
    Media,
    Download,
    Upload,
    Encode,
    FileCopy,
    Timer,
    Install,
    GenericProgress,
    Experimental,
    Custom
}

public enum LiveActivityState
{
    Active,
    Paused,
    Completed,
    Stopped,
    Error,
    Hidden
}

public enum LiveActivityActionKind
{
    Play,
    Pause,
    PlayPause,
    Next,
    Previous,
    OpenSourceApp,
    Dismiss
}

public enum LiveActivityEndReason
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

public enum IslandSizePreset
{
    Medium,
    Small,
    Large
}
