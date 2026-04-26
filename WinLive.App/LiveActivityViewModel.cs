using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinLive.Core;

namespace WinLive.App;

public sealed class LiveActivityViewModel : ObservableObject
{
    private LiveActivity _activity;
    private bool _isSelected;

    public LiveActivityViewModel(LiveActivity activity)
    {
        _activity = activity;
    }

    public LiveActivity Activity => _activity;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Id => Activity.Id;

    public LiveActivityType Type => Activity.Type;

    public LiveActivityState State => Activity.State;

    public string Title => Activity.Title;

    public string Subtitle => Activity.Subtitle ?? Activity.SourceApp?.Name ?? Activity.Type.ToString();

    public double? Progress => Activity.Progress;

    public int Priority => Activity.Priority;

    public bool IsMedia => Activity.Type == LiveActivityType.Media;

    public bool IsEmphasized => Activity.IsEmphasized;

    public bool SupportsPlayPause => SupportsAction(LiveActivityActionKind.PlayPause);

    public bool SupportsPrevious => SupportsAction(LiveActivityActionKind.Previous);

    public bool SupportsNext => SupportsAction(LiveActivityActionKind.Next);

    public bool SupportsOpenSourceApp => SupportsAction(LiveActivityActionKind.OpenSourceApp);

    public bool SupportsDismiss => !IsMedia;

    public bool ShowSecondaryPlayPauseControl => IsMedia && SupportsPlayPause;

    public string PlayPauseGlyph => Activity.State == LiveActivityState.Paused
        ? "\uE768"
        : "\uE769";

    public string ActivityIconGlyph => Activity.Type switch
    {
        LiveActivityType.Media => "\uE8D6",
        LiveActivityType.Download => "\uE896",
        LiveActivityType.Upload => "\uE898",
        LiveActivityType.Encode => "\uE7F4",
        LiveActivityType.FileCopy => "\uE8C8",
        LiveActivityType.Timer => "\uE916",
        LiveActivityType.Install => "\uE778",
        LiveActivityType.GenericProgress => "\uE9D9",
        LiveActivityType.Experimental => "\uE9CA",
        _ => "\uE7C3"
    };

    public string TypeLabel => Activity.Type switch
    {
        LiveActivityType.Media => "MEDIA",
        LiveActivityType.Download => "DOWN",
        LiveActivityType.Upload => "UP",
        LiveActivityType.Encode => "ENC",
        LiveActivityType.FileCopy => "COPY",
        LiveActivityType.Timer => "TIME",
        LiveActivityType.Install => "INST",
        LiveActivityType.GenericProgress => "PROG",
        LiveActivityType.Experimental => "BETA",
        _ => "LIVE"
    };

    public string StateLabel => Activity.State.ToString().ToUpperInvariant();

    public ImageSource? AlbumArtSource => CreateImage(Activity.Media?.AlbumArtBytes);

    public void Update(LiveActivity activity)
    {
        if (!string.Equals(activity.Id, Id, StringComparison.Ordinal))
        {
            throw new ArgumentException("Activity id cannot change.", nameof(activity));
        }

        _activity = activity;
        OnPropertyChanged(nameof(Activity));
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(Priority));
        OnPropertyChanged(nameof(IsMedia));
        OnPropertyChanged(nameof(IsEmphasized));
        OnPropertyChanged(nameof(SupportsPlayPause));
        OnPropertyChanged(nameof(SupportsPrevious));
        OnPropertyChanged(nameof(SupportsNext));
        OnPropertyChanged(nameof(SupportsOpenSourceApp));
        OnPropertyChanged(nameof(SupportsDismiss));
        OnPropertyChanged(nameof(ShowSecondaryPlayPauseControl));
        OnPropertyChanged(nameof(PlayPauseGlyph));
        OnPropertyChanged(nameof(ActivityIconGlyph));
        OnPropertyChanged(nameof(TypeLabel));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(AlbumArtSource));
    }

    public bool SupportsAction(LiveActivityActionKind action)
    {
        return Activity.SupportsAction(action);
    }

    private static ImageSource? CreateImage(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.DecodePixelWidth = 128;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
