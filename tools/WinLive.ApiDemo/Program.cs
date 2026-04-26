using System.Net.Http.Headers;
using System.Net.Http.Json;

var baseAddress = GetOption(args, "--base") ??
    Environment.GetEnvironmentVariable("WINLIVE_API_BASE") ??
    "http://127.0.0.1:8765";
var token = GetOption(args, "--token") ??
    Environment.GetEnvironmentVariable("WINLIVE_API_TOKEN");
var id = GetOption(args, "--id") ?? "demo:download";

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

Console.WriteLine($"Sending demo activity to {baseAddress}/api/v1/activities/{id}");

for (var i = 0; i <= 100; i += 5)
{
    var response = await client.PutAsJsonAsync($"/api/v1/activities/{Uri.EscapeDataString(id)}", new
    {
        type = "download",
        state = i >= 100 ? "completed" : "active",
        title = i >= 100 ? "Demo download complete" : "Demo download",
        subtitle = $"{i}%",
        progress = i / 100d,
        priority = 40,
        sourceApp = new
        {
            name = "WinLive.ApiDemo"
        },
        metadata = new Dictionary<string, string>
        {
            ["demo"] = "true"
        }
    });

    response.EnsureSuccessStatusCode();
    await Task.Delay(160);
}

await Task.Delay(1200);
using var delete = await client.DeleteAsync($"/api/v1/activities/{Uri.EscapeDataString(id)}");
delete.EnsureSuccessStatusCode();
Console.WriteLine("Done.");
return 0;

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
