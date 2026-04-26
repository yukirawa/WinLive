using WinLive.Windows;

namespace WinLive.Tests;

public sealed class MediaMetadataCacheTests
{
    [Fact]
    public void EmptyTitleWithoutCacheIsRejected()
    {
        var cache = new MediaMetadataCache();

        var resolved = cache.TryResolve(
            "media:test",
            null,
            null,
            null,
            null,
            hasUsableSession: true,
            out _);

        Assert.False(resolved);
    }

    [Fact]
    public void EmptyTitleWithUsableSessionReusesCachedMetadata()
    {
        var cache = new MediaMetadataCache();
        Assert.True(cache.TryResolve(
            "media:test",
            "Track",
            "Artist",
            "Album",
            null,
            hasUsableSession: true,
            out _));

        var resolved = cache.TryResolve(
            "media:test",
            " ",
            null,
            null,
            null,
            hasUsableSession: true,
            out var metadata);

        Assert.True(resolved);
        Assert.Equal("Track", metadata.Title);
        Assert.Equal("Artist", metadata.Artist);
        Assert.Equal("Album", metadata.Album);
    }

    [Fact]
    public void EmptyTitleWithUnusableSessionClearsCachedMetadata()
    {
        var cache = new MediaMetadataCache();
        Assert.True(cache.TryResolve(
            "media:test",
            "Track",
            null,
            null,
            null,
            hasUsableSession: true,
            out _));

        Assert.False(cache.TryResolve(
            "media:test",
            null,
            null,
            null,
            null,
            hasUsableSession: false,
            out _));
        Assert.False(cache.TryResolve(
            "media:test",
            null,
            null,
            null,
            null,
            hasUsableSession: true,
            out _));
    }

    [Fact]
    public void SameTrackWithoutFreshArtReusesCachedAlbumArt()
    {
        var cache = new MediaMetadataCache();
        var albumArt = new byte[] { 1, 2, 3 };
        Assert.True(cache.TryResolve(
            "media:test",
            "Track",
            "Artist",
            "Album",
            albumArt,
            hasUsableSession: true,
            out _));

        var resolved = cache.TryResolve(
            "media:test",
            "Track",
            "Artist",
            "Album",
            null,
            hasUsableSession: true,
            out var metadata);

        Assert.True(resolved);
        Assert.Same(albumArt, metadata.AlbumArtBytes);
    }
}
