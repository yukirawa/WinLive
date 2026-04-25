using System.Text.Json;
using System.Text.Json.Serialization;
using WinLive.Core;

namespace WinLive.Windows;

public sealed class AppDataSettingsStore : IWinLiveSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public AppDataSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinLive",
            "settings.json"))
    {
    }

    public AppDataSettingsStore(string settingsPath)
    {
        SettingsPath = settingsPath;
    }

    public string SettingsPath { get; }

    public async Task<WinLiveSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        WinLiveSettings settings;

        if (!File.Exists(SettingsPath))
        {
            settings = new WinLiveSettings();
            settings.Normalize();
            await SaveAsync(settings, cancellationToken).ConfigureAwait(false);
            return settings;
        }

        await using (var stream = File.OpenRead(SettingsPath))
        {
            settings = await JsonSerializer.DeserializeAsync<WinLiveSettings>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? new WinLiveSettings();
        }

        settings.Normalize();
        await SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        return settings;
    }

    public async Task SaveAsync(WinLiveSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();

        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }
}
