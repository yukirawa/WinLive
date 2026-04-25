using WinLive.Core;

namespace WinLive.Tests;

public sealed class LiveSessionStoreTests
{
    [Fact]
    public void PrimarySessionUsesPriorityAndVisibleSessionsOnly()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 4, 25, 10, 0, 0, TimeSpan.Zero));
        var store = new LiveSessionStore(clock);

        store.Upsert(Session("low", priority: 1, state: LiveSessionState.Active));
        clock.Advance(TimeSpan.FromSeconds(1));
        store.Upsert(Session("hidden", priority: 999, state: LiveSessionState.Hidden));
        clock.Advance(TimeSpan.FromSeconds(1));
        store.Upsert(Session("high", priority: 10, state: LiveSessionState.Active));

        Assert.Equal("high", store.PrimarySession?.Id);
        Assert.Equal(["high", "low"], store.VisibleSessions.Select(item => item.Id).ToArray());
    }

    [Fact]
    public void PausedMusicStaysVisibleAndStoppedSessionDoesNot()
    {
        var store = new LiveSessionStore();

        store.Upsert(Session("paused", priority: 1, state: LiveSessionState.Paused));
        store.Upsert(Session("stopped", priority: 99, state: LiveSessionState.Stopped));

        Assert.True(store.HasVisibleSessions);
        Assert.Equal("paused", store.PrimarySession?.Id);
        Assert.DoesNotContain(store.VisibleSessions, item => item.Id == "stopped");
    }

    [Fact]
    public void UpsertClampsProgress()
    {
        var store = new LiveSessionStore();

        var saved = store.Upsert(Session("progress", progress: 2.5));

        Assert.Equal(1, saved.Progress);
    }

    [Fact]
    public void ClearExpiredEmphasisTurnsOffExpiredSessions()
    {
        var now = new DateTimeOffset(2026, 4, 25, 10, 0, 0, TimeSpan.Zero);
        var store = new LiveSessionStore(new FakeClock(now));
        store.Upsert(Session("music", emphasized: true, emphasizedUntil: now.AddSeconds(-1)));

        var changed = store.ClearExpiredEmphasis(now);

        Assert.Equal(1, changed);
        Assert.False(store.PrimarySession?.IsEmphasized);
    }

    private static LiveSession Session(
        string id,
        int priority = 0,
        LiveSessionState state = LiveSessionState.Active,
        double? progress = null,
        bool emphasized = false,
        DateTimeOffset? emphasizedUntil = null)
    {
        return new LiveSession
        {
            Id = id,
            Type = LiveSessionType.Music,
            Title = id,
            State = state,
            Progress = progress,
            Priority = priority,
            IsEmphasized = emphasized,
            EmphasizedUntil = emphasizedUntil
        };
    }
}

internal sealed class FakeClock : ISystemClock
{
    public FakeClock(DateTimeOffset now)
    {
        UtcNow = now;
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(TimeSpan amount)
    {
        UtcNow = UtcNow.Add(amount);
    }
}
