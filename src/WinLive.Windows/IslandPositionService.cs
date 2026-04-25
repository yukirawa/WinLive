using System.Windows.Forms;
using WinLive.Core;

namespace WinLive.Windows;

public sealed class WindowsScreenBoundsProvider : IScreenBoundsProvider
{
    public IReadOnlyList<ScreenBounds> GetWorkingAreas()
    {
        return Screen.AllScreens
            .Select(screen => screen.WorkingArea)
            .Select(area => new ScreenBounds(area.Left, area.Top, area.Width, area.Height))
            .ToArray();
    }
}

public sealed class IslandPositionService : IIslandPositionService
{
    private readonly IScreenBoundsProvider _screenBoundsProvider;

    public IslandPositionService()
        : this(new WindowsScreenBoundsProvider())
    {
    }

    public IslandPositionService(IScreenBoundsProvider screenBoundsProvider)
    {
        _screenBoundsProvider = screenBoundsProvider;
    }

    public IslandBounds CorrectToVisibleArea(IslandBounds desired)
    {
        var screens = _screenBoundsProvider.GetWorkingAreas();
        if (screens.Count == 0)
        {
            return desired.Width <= 0 || desired.Height <= 0 ? IslandBounds.Default : desired;
        }

        var targetScreen = screens.FirstOrDefault(screen => screen.Intersects(desired));
        if (targetScreen.Width <= 0 || targetScreen.Height <= 0)
        {
            targetScreen = screens[0];
        }

        var width = Math.Clamp(
            desired.Width <= 0 ? IslandBounds.Default.Width : desired.Width,
            1,
            targetScreen.Width);
        var height = Math.Clamp(
            desired.Height <= 0 ? IslandBounds.Default.Height : desired.Height,
            1,
            targetScreen.Height);

        var left = Math.Clamp(desired.Left, targetScreen.Left, targetScreen.Right - width);
        var top = Math.Clamp(desired.Top, targetScreen.Top, targetScreen.Bottom - height);

        return new IslandBounds(left, top, width, height);
    }
}
