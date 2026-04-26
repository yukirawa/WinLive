using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinLive.Core;

namespace WinLive.App;

public sealed class LiveActivityViewModel
{
    public LiveActivityViewModel(LiveActivity activity)
    {
        Activity = activity;
    }

    public LiveActivity Activity { get; }

    public string Id => Activity.Id;

    public LiveActivityType Type => Activity.Type;

    public LiveActivityState State => Activity.State;

    public string Title => Activity.Title;

    public string Subtitle => Activity.Subtitle ?? Activity.SourceApp?.Name ?? Activity.Type.ToString();

    public double? Progress => Activity.Progress;

    public int Priority => Activity.Priority;

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
