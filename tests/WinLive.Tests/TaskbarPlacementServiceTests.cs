using WinLive.Core;
using WinLive.Windows;

namespace WinLive.Tests;

public sealed class TaskbarPlacementServiceTests
{
    [Fact]
    public void CorrectToVisibleAreaKeepsBoundsPositive()
    {
        var service = new TaskbarPlacementService();

        var corrected = service.CorrectToVisibleArea(new IslandBounds(-100000, -100000, 360, 74));

        Assert.True(corrected.Width > 0);
        Assert.True(corrected.Height > 0);
    }
}
