using WinLive.Core;

namespace WinLive.Windows;

public sealed class TrayCommandService : ITrayCommandService
{
    public event EventHandler? OpenSettingsRequested;

    public event EventHandler? ResetPositionRequested;

    public event EventHandler? ExitRequested;

    public bool LastPublishedIslandVisibility { get; private set; }

    public void PublishIslandVisibility(bool isVisible)
    {
        LastPublishedIslandVisibility = isVisible;
    }

    public void RequestOpenSettings() => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);

    public void RequestResetPosition() => ResetPositionRequested?.Invoke(this, EventArgs.Empty);

    public void RequestExit() => ExitRequested?.Invoke(this, EventArgs.Empty);
}
