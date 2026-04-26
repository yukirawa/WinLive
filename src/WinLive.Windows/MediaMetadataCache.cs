namespace WinLive.Windows;

internal sealed class MediaMetadataCache
{
    private readonly Dictionary<string, CachedMediaMetadata> _items = new(StringComparer.Ordinal);

    public bool TryResolve(
        string sessionId,
        string? title,
        string? artist,
        string? album,
        byte[]? albumArtBytes,
        bool hasUsableSession,
        out CachedMediaMetadata metadata)
    {
        title = Normalize(title);
        artist = Normalize(artist);
        album = Normalize(album);

        if (title is null)
        {
            if (!hasUsableSession)
            {
                _items.Remove(sessionId);
                metadata = default!;
                return false;
            }

            return _items.TryGetValue(sessionId, out metadata!);
        }

        _items.TryGetValue(sessionId, out var existing);
        var resolved = new CachedMediaMetadata(
            title,
            artist,
            album,
            albumArtBytes ?? (existing?.IsSameTrack(title, artist, album) == true
                ? existing.AlbumArtBytes
                : null));

        _items[sessionId] = resolved;
        metadata = resolved;
        return true;
    }

    public void Remove(string sessionId)
    {
        _items.Remove(sessionId);
    }

    public void Clear()
    {
        _items.Clear();
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

internal sealed record CachedMediaMetadata(
    string Title,
    string? Artist,
    string? Album,
    byte[]? AlbumArtBytes)
{
    public bool IsSameTrack(string title, string? artist, string? album)
    {
        return string.Equals(Title, title, StringComparison.Ordinal) &&
            string.Equals(Artist, artist, StringComparison.Ordinal) &&
            string.Equals(Album, album, StringComparison.Ordinal);
    }
}
