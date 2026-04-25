using WinLive.Core;
using WinLive.Windows;

namespace WinLive.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public async Task LoadCreatesSettingsFileAndBearerToken()
    {
        var path = GetTempSettingsPath();
        var store = new AppDataSettingsStore(path);

        var settings = await store.LoadAsync();

        Assert.True(File.Exists(path));
        Assert.False(settings.ExternalApi.Enabled);
        Assert.Equal("127.0.0.1", settings.ExternalApi.Host);
        Assert.Equal(8765, settings.ExternalApi.Port);
        Assert.False(string.IsNullOrWhiteSpace(settings.ExternalApi.AuthToken));
    }

    [Fact]
    public async Task SaveAndLoadRoundTripsIslandSettings()
    {
        var path = GetTempSettingsPath();
        var store = new AppDataSettingsStore(path);
        var settings = await store.LoadAsync();
        settings.IslandBounds = new IslandBounds(12, 34, 456, 78);
        settings.Theme = ThemePreference.Dark;

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        Assert.Equal(settings.IslandBounds, loaded.IslandBounds);
        Assert.Equal(ThemePreference.Dark, loaded.Theme);
    }

    private static string GetTempSettingsPath()
    {
        return Path.Combine(Path.GetTempPath(), "WinLive.Tests", Guid.NewGuid().ToString("N"), "settings.json");
    }
}
