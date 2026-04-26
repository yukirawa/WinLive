using WinLive.Core;
using WinLive.Windows;

namespace WinLive.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public async Task LoadCreatesSettingsWithToken()
    {
        var path = Path.Combine(Path.GetTempPath(), $"winlive-{Guid.NewGuid():N}", "settings.json");
        var store = new AppDataSettingsStore(path);

        var settings = await store.LoadAsync();

        Assert.True(File.Exists(path));
        Assert.False(string.IsNullOrWhiteSpace(settings.ExternalApi.AuthToken));
        Assert.Equal(path, store.SettingsPath);
    }

    [Fact]
    public async Task LoadAcceptsLegacyStringEnumSettings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"winlive-{Guid.NewGuid():N}", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, """
            {
              "startWithWindows": false,
              "hideDuringFullScreen": true,
              "showPausedMusic": true,
              "showAlbumArt": true,
              "theme": "system",
              "islandSize": "medium",
              "clickBehavior": "toggleExpanded",
              "islandBounds": {
                "left": 80,
                "top": 80,
                "width": 390,
                "height": 108
              },
              "externalApi": {
                "enabled": false,
                "host": "127.0.0.1",
                "port": 8765,
                "authToken": "test-token"
              }
            }
            """);

        var store = new AppDataSettingsStore(path);

        var settings = await store.LoadAsync();

        Assert.Equal("test-token", settings.ExternalApi.AuthToken);
        Assert.Equal(ThemePreference.System, settings.Theme);
        Assert.False(settings.ExternalApi.Enabled);
    }
}
