using WinLive.Core;

namespace WinLive.Presentation;

public sealed class LiveSessionViewModel
{
    public LiveSessionViewModel(LiveSession session)
    {
        Session = session;
    }

    public LiveSession Session { get; }

    public string Id => Session.Id;

    public LiveSessionType Type => Session.Type;

    public string Title => Session.Title;

    public string? Subtitle => Session.Subtitle;

    public LiveSessionState State => Session.State;

    public double? Progress => Session.Progress;

    public string? AppName => Session.AppName;

    public bool IsEmphasized => Session.IsEmphasized;

    public IReadOnlyList<LiveSessionActionDescriptor> Actions => Session.Actions;

    public string? Artist => Session.Media?.Artist;

    public string? Album => Session.Media?.Album;

    public byte[]? AlbumArtBytes => Session.Media?.AlbumArtBytes;

    public TimeSpan? Position => Session.Media?.Position;

    public TimeSpan? Duration => Session.Media?.Duration;

    public bool SupportsAction(LiveSessionActionKind action) => Session.SupportsAction(action);
}
