using System.Security.Cryptography;

namespace WinLive.Core;

public sealed class WinLiveSettings
{
    public bool StartWithWindows { get; set; }

    public bool HideDuringFullScreen { get; set; } = true;

    public bool ShowPausedMusic { get; set; } = true;

    public bool ShowAlbumArt { get; set; } = true;

    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public IslandSizePreference IslandSize { get; set; } = IslandSizePreference.Medium;

    public IslandClickBehavior ClickBehavior { get; set; } = IslandClickBehavior.ToggleExpanded;

    public IslandBounds IslandBounds { get; set; } = IslandBounds.Default;

    public ExternalApiSettings ExternalApi { get; set; } = new();

    public void Normalize()
    {
        ExternalApi ??= new ExternalApiSettings();

        if (IslandBounds.Width <= 0 || IslandBounds.Height <= 0)
        {
            IslandBounds = IslandBounds.Default;
        }

        if (ExternalApi.Port is < 1024 or > 65535)
        {
            ExternalApi.Port = 8765;
        }

        if (string.IsNullOrWhiteSpace(ExternalApi.Host))
        {
            ExternalApi.Host = "127.0.0.1";
        }

        if (string.IsNullOrWhiteSpace(ExternalApi.AuthToken))
        {
            ExternalApi.AuthToken = TokenGenerator.CreateToken();
        }
    }
}

public sealed class ExternalApiSettings
{
    public bool Enabled { get; set; }

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 8765;

    public string AuthToken { get; set; } = string.Empty;
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
