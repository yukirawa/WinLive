using WinLive.Core;

namespace WinLive.Tests;

public sealed class LiveActivityStoreTests
{
    [Fact]
    public void UpsertNormalizesProgressAndSortsPrimaryByPriority()
    {
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-26T00:00:00Z"));
        var store = new LiveActivityStore(clock);

        store.Upsert(new LiveActivity
        {
            Id = "low",
            Type = LiveActivityType.Download,
            Title = "Low",
            Progress = -1,
            Priority = 1
        });
        store.Upsert(new LiveActivity
        {
            Id = "high",
            Type = LiveActivityType.Encode,
            Title = "High",
            Progress = 1.5,
            Priority = 10
        });

        Assert.Equal("high", store.PrimaryActivity?.Id);
        Assert.Equal(1, store.PrimaryActivity?.Progress);
        Assert.Equal(0, store.Activities.Single(item => item.Id == "low").Progress);
    }

    [Fact]
    public void HiddenAndStoppedActivitiesAreNotVisible()
    {
        var store = new LiveActivityStore();

        store.Upsert(new LiveActivity
        {
            Id = "hidden",
            Title = "Hidden",
            State = LiveActivityState.Hidden
        });
        store.Upsert(new LiveActivity
        {
            Id = "stopped",
            Title = "Stopped",
            State = LiveActivityState.Stopped
        });
        store.Upsert(new LiveActivity
        {
            Id = "active",
            Title = "Active",
            State = LiveActivityState.Active
        });

        Assert.Equal("active", store.PrimaryActivity?.Id);
        Assert.Single(store.VisibleActivities);
    }

    [Fact]
    public void ClearExpiredEmphasisUpdatesMatchingActivities()
    {
        var now = DateTimeOffset.Parse("2026-04-26T00:00:00Z");
        var store = new LiveActivityStore(new FakeClock(now));
        store.Upsert(new LiveActivity
        {
            Id = "activity",
            Title = "Activity",
            IsEmphasized = true,
            EmphasizedUntil = now.AddSeconds(-1)
        });

        var changed = store.ClearExpiredEmphasis(now);

        Assert.Equal(1, changed);
        Assert.False(store.PrimaryActivity?.IsEmphasized);
    }

    private sealed class FakeClock : ISystemClock
    {
        public FakeClock(DateTimeOffset now)
        {
            UtcNow = now;
        }

        public DateTimeOffset UtcNow { get; }
    }
}
