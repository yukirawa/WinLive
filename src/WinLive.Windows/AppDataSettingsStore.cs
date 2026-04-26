using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using WinLive.Core;

namespace WinLive.Windows;

public sealed class AppDataSettingsStore : IWinLiveSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    static AppDataSettingsStore()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public AppDataSettingsStore(string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinLive",
            "settings.json");
    }

    public string SettingsPath { get; }

    public async Task<WinLiveSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath))
        {
            var created = new WinLiveSettings();
            created.Normalize();
            await SaveAsync(created, cancellationToken).ConfigureAwait(false);
            return created;
        }

        await using var stream = File.OpenRead(SettingsPath);
        var settings = await JsonSerializer.DeserializeAsync<WinLiveSettings>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false) ?? new WinLiveSettings();

        settings.Normalize();
        return settings;
    }

    public async Task SaveAsync(WinLiveSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }
}
