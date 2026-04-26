using WinLive.Windows;

namespace WinLive.Tests;

public sealed class MediaActivityIdentityTests
{
    [Fact]
    public void SingleSourceUsesStableAppActivityId()
    {
        var ids = MediaActivityIdentity.BuildActivityIds(
            [
                new MediaSessionIdentityInput(101, "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify")
            ],
            new Dictionary<int, string>());

        Assert.Equal("media:SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", ids[101]);
    }

    [Fact]
    public void SameSourceWithNewSessionWrapperKeepsSameActivityId()
    {
        var first = MediaActivityIdentity.BuildActivityIds(
            [
                new MediaSessionIdentityInput(101, "Spotify")
            ],
            new Dictionary<int, string>());
        var second = MediaActivityIdentity.BuildActivityIds(
            [
                new MediaSessionIdentityInput(202, "Spotify")
            ],
            first);

        Assert.Equal("media:Spotify", first[101]);
        Assert.Equal("media:Spotify", second[202]);
    }

    [Fact]
    public void DifferentSourcesBecomeDifferentTiles()
    {
        var ids = MediaActivityIdentity.BuildActivityIds(
            [
                new MediaSessionIdentityInput(101, "Spotify"),
                new MediaSessionIdentityInput(202, "Microsoft.ZuneMusic")
            ],
            new Dictionary<int, string>());

        Assert.Equal("media:Spotify", ids[101]);
        Assert.Equal("media:Microsoft.ZuneMusic", ids[202]);
    }

    [Fact]
    public void DuplicateSourcesGetStableBranchIds()
    {
        var first = MediaActivityIdentity.BuildActivityIds(
            [
                new MediaSessionIdentityInput(101, "Player"),
                new MediaSessionIdentityInput(202, "Player")
            ],
            new Dictionary<int, string>());
        var second = MediaActivityIdentity.BuildActivityIds(
            [
                new MediaSessionIdentityInput(101, "Player"),
                new MediaSessionIdentityInput(303, "Player")
            ],
            first);

        Assert.Equal("media:Player", first[101]);
        Assert.Equal("media:Player:1", first[202]);
        Assert.Equal("media:Player", second[101]);
        Assert.Equal("media:Player:1", second[303]);
    }
}
