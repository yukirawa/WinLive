using System.Security.Cryptography;

namespace WinLive.Core;

public sealed class WinLiveSettings
{
    public bool StartWithWindows { get; set; }

    public bool HideDuringFullScreen { get; set; } = true;

    public bool ShowPausedMedia { get; set; } = true;

    public bool ShowAlbumArt { get; set; } = true;

    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public IslandSizePreset IslandSize { get; set; } = IslandSizePreset.Medium;

    public bool HasCustomIslandPosition { get; set; }

    public IslandBounds IslandBounds { get; set; } = IslandBounds.Default;

    public IslandExpansionDirection ExpansionDirection { get; set; } = IslandExpansionDirection.Up;

    public ExternalApiSettings ExternalApi { get; set; } = new();

    public ExperimentalProgressSettings ExperimentalProgress { get; set; } = new();

    public void Normalize()
    {
        if (IslandBounds.Width <= 0 || IslandBounds.Height <= 0)
        {
            IslandBounds = IslandBounds.Default;
        }

        if (!Enum.IsDefined(IslandSize))
        {
            IslandSize = IslandSizePreset.Medium;
        }

        ExternalApi ??= new ExternalApiSettings();
        ExternalApi.Normalize();

        ExperimentalProgress ??= new ExperimentalProgressSettings();
        ExperimentalProgress.Normalize();
    }
}

public enum IslandExpansionDirection
{
    Up,
    Down
}

public sealed class ExternalApiSettings
{
    public bool Enabled { get; set; } = true;

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 8765;

    public string AuthToken { get; set; } = string.Empty;

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            Host = "127.0.0.1";
        }

        if (Port is < 1024 or > 65535)
        {
            Port = 8765;
        }

        if (string.IsNullOrWhiteSpace(AuthToken))
        {
            AuthToken = TokenGenerator.CreateToken();
        }
    }
}

public sealed class ExperimentalProgressSettings
{
    public bool Enabled { get; set; }

    public int PollIntervalMilliseconds { get; set; } = 2500;

    public void Normalize()
    {
        if (PollIntervalMilliseconds < 500)
        {
            PollIntervalMilliseconds = 500;
        }
    }
}

public static class TokenGenerator
{
    public static string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
