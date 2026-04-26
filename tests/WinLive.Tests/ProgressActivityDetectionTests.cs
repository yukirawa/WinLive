using WinLive.Core;
using WinLive.Windows;

namespace WinLive.Tests;

public sealed class ProgressActivityDetectionTests
{
    [Fact]
    public void DetectionIdIsStableWhenWindowTitleChanges()
    {
        var first = Snapshot(windowTitle: "Copying 10% complete", value: 10);
        var second = Snapshot(windowTitle: "Copying 70% complete", value: 70);

        Assert.True(ProgressActivityDetection.TryCreateActivity(first, "explorer", out var firstActivity));
        Assert.True(ProgressActivityDetection.TryCreateActivity(second, "explorer", out var secondActivity));

        Assert.Equal(firstActivity.Id, secondActivity.Id);
        Assert.Equal(LiveActivityType.FileCopy, firstActivity.Type);
        Assert.Equal(0.1, firstActivity.Progress!.Value, 3);
        Assert.Equal(0.7, secondActivity.Progress!.Value, 3);
    }

    [Theory]
    [InlineData(false, false, 0, 100, 10)]
    [InlineData(true, true, 0, 100, 10)]
    [InlineData(true, false, 100, 100, 10)]
    [InlineData(true, false, 100, 0, 10)]
    [InlineData(true, false, 0, 100, -1)]
    public void InvalidProgressBarSnapshotsAreRejected(
        bool isEnabled,
        bool isOffscreen,
        double minimum,
        double maximum,
        double value)
    {
        var snapshot = Snapshot(
            isEnabled: isEnabled,
            isOffscreen: isOffscreen,
            minimum: minimum,
            maximum: maximum,
            value: value);

        Assert.False(ProgressActivityDetection.TryCreateActivity(snapshot, "app", out _));
    }

    [Fact]
    public void RemoveStaleActivitiesOnlyRemovesDetectedProgress()
    {
        var store = new LiveActivityStore();
        Assert.True(ProgressActivityDetection.TryCreateActivity(Snapshot(value: 40), "app", out var detected));
        store.Upsert(detected);
        store.Upsert(new LiveActivity
        {
            Id = "manual",
            Type = LiveActivityType.Download,
            Title = "Manual",
            State = LiveActivityState.Active
        });

        var removed = ProgressActivityDetection.RemoveStaleActivities(store, new HashSet<string>());

        Assert.Equal(1, removed);
        Assert.False(store.TryGetActivity(detected.Id, out _));
        Assert.True(store.TryGetActivity("manual", out _));
    }

    private static ProgressBarSnapshot Snapshot(
        string windowTitle = "Downloading file",
        string automationId = "ProgressBar",
        string name = "",
        string className = "msctls_progress32",
        bool isEnabled = true,
        bool isOffscreen = false,
        double minimum = 0,
        double maximum = 100,
        double value = 50)
    {
        return new ProgressBarSnapshot(
            1234,
            windowTitle,
            automationId,
            name,
            className,
            isEnabled,
            isOffscreen,
            minimum,
            maximum,
            value);
    }
}
