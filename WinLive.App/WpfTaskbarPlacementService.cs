using System.Windows;
using WinLive.Core;

namespace WinLive.App;

public sealed class WpfTaskbarPlacementService : ITaskbarPlacementService
{
    private const double Margin = 14;

    public IslandBounds GetDefaultIslandBounds(IslandBounds preferred)
    {
        var desired = preferred.Width <= 0 || preferred.Height <= 0
            ? IslandBounds.Default
            : preferred;
        var area = SystemParameters.WorkArea;

        return CorrectToVisibleArea(new IslandBounds(
            area.Right - desired.Width - Margin,
            area.Bottom - desired.Height - Margin,
            desired.Width,
            desired.Height));
    }

    public IslandBounds CorrectToVisibleArea(IslandBounds desired)
    {
        var area = SystemParameters.WorkArea;
        var width = Math.Min(Math.Max(desired.Width, 260), Math.Max(260, area.Width - Margin * 2));
        var height = Math.Min(Math.Max(desired.Height, 48), Math.Max(48, area.Height - Margin * 2));
        var left = Math.Clamp(desired.Left, area.Left + Margin, area.Right - width - Margin);
        var top = Math.Clamp(desired.Top, area.Top + Margin, area.Bottom - height - Margin);
        return new IslandBounds(left, top, width, height);
    }
}
