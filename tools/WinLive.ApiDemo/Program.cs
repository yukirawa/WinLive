using System.Net.Http.Headers;
using System.Net.Http.Json;

var baseAddress = GetOption(args, "--base") ??
    Environment.GetEnvironmentVariable("WINLIVE_API_BASE") ??
    "http://127.0.0.1:8765";
var token = GetOption(args, "--token") ??
    Environment.GetEnvironmentVariable("WINLIVE_API_TOKEN");
var scenario = GetOption(args, "--scenario") ?? "download";
var idPrefix = GetOption(args, "--id") ?? "demo";

if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("Provide --token or WINLIVE_API_TOKEN.");
    return 2;
}

using var client = new HttpClient
{
    BaseAddress = new Uri(baseAddress)
};
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

var scenarios = string.Equals(scenario, "all", StringComparison.OrdinalIgnoreCase)
    ? Scenario.All
    : [Scenario.FromName(scenario)];

Console.WriteLine($"Sending {string.Join(", ", scenarios.Select(item => item.Name))} to {baseAddress}/api/v1/activities");

foreach (var item in scenarios)
{
    await SendScenarioAsync(client, idPrefix, item);
}

Console.WriteLine("Done.");
return 0;

static async Task SendScenarioAsync(HttpClient client, string idPrefix, Scenario scenario)
{
    var id = $"{idPrefix}:{scenario.Name}";
    var path = $"/api/v1/activities/{Uri.EscapeDataString(id)}";

    for (var i = 0; i <= 100; i += 10)
    {
        var progress = i / 100d;
        var payload = new
        {
            type = scenario.Type,
            state = i >= 100 ? "completed" : "active",
            title = i >= 100 ? scenario.DoneTitle : scenario.ActiveTitle,
            subtitle = scenario.Subtitle(i),
            progress,
            priority = scenario.Priority,
            sourceApp = new
            {
                name = "WinLive.ApiDemo"
            },
            metadata = new Dictionary<string, string>
            {
                ["demo"] = "true",
                ["scenario"] = scenario.Name
            }
        };

        using var response = i == 0
            ? await client.PutAsJsonAsync(path, payload)
            : await client.PatchAsJsonAsync(path, payload);
        response.EnsureSuccessStatusCode();
        await Task.Delay(140);
    }

    await Task.Delay(700);
    using var delete = await client.DeleteAsync(path);
    delete.EnsureSuccessStatusCode();
}

static string? GetOption(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

sealed record Scenario(
    string Name,
    string Type,
    string ActiveTitle,
    string DoneTitle,
    int Priority,
    Func<int, string> Subtitle)
{
    public static readonly Scenario[] All =
    [
        new("download", "download", "Demo download", "Demo download complete", 50, value => $"{value}% downloaded"),
        new("upload", "upload", "Demo upload", "Demo upload complete", 45, value => $"{value}% uploaded"),
        new("encode", "encode", "Demo encode", "Demo encode complete", 55, value => $"{value}% rendered"),
        new("copy", "fileCopy", "Demo file copy", "Demo file copy complete", 48, value => $"{value}% copied"),
        new("timer", "timer", "Demo timer", "Demo timer complete", 40, value => $"{Math.Max(0, 100 - value)}% remaining"),
        new("install", "install", "Demo install", "Demo install complete", 60, value => $"{value}% installed")
    ];

    public static Scenario FromName(string name)
    {
        return All.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) ??
            throw new ArgumentException($"Unsupported scenario '{name}'. Use download, upload, encode, copy, timer, install, or all.");
    }
}
