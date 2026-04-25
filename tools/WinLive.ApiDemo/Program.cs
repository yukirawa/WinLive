using System.Net.Http.Headers;
using System.Net.Http.Json;

var options = DemoOptions.Parse(args);
if (options is null)
{
    DemoOptions.PrintUsage();
    return 1;
}

using var client = new HttpClient
{
    BaseAddress = new Uri($"http://127.0.0.1:{options.Port}")
};
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);

return options.Command switch
{
    "create" => await CreateAsync(client, options),
    "update" => await UpdateAsync(client, options),
    "end" => await EndAsync(client, options),
    "demo" => await RunDemoAsync(client, options),
    _ => PrintUnknownCommand(options.Command)
};

static async Task<int> CreateAsync(HttpClient client, DemoOptions options)
{
    var payload = new
    {
        type = "genericProgress",
        title = options.Title,
        subtitle = "WinLive.ApiDemo",
        state = "active",
        progress = options.Progress,
        appName = "WinLive.ApiDemo",
        priority = 10,
        metadata = new Dictionary<string, string>
        {
            ["source"] = "WinLive.ApiDemo"
        }
    };

    using var response = await client.PutAsJsonAsync($"/api/v1/sessions/{options.Id}", payload);
    await PrintResponseAsync(response);
    return response.IsSuccessStatusCode ? 0 : 2;
}

static async Task<int> UpdateAsync(HttpClient client, DemoOptions options)
{
    var payload = new
    {
        title = options.Title,
        state = options.Progress >= 1 ? "completed" : "active",
        progress = options.Progress
    };

    using var response = await client.PatchAsJsonAsync($"/api/v1/sessions/{options.Id}", payload);
    await PrintResponseAsync(response);
    return response.IsSuccessStatusCode ? 0 : 2;
}

static async Task<int> EndAsync(HttpClient client, DemoOptions options)
{
    using var response = await client.DeleteAsync($"/api/v1/sessions/{options.Id}");
    await PrintResponseAsync(response);
    return response.IsSuccessStatusCode ? 0 : 2;
}

static async Task<int> RunDemoAsync(HttpClient client, DemoOptions options)
{
    var createResult = await CreateAsync(client, options);
    if (createResult != 0)
    {
        return createResult;
    }

    for (var step = 1; step <= 10; step++)
    {
        await Task.Delay(500);
        options.Progress = step / 10d;
        var updateResult = await UpdateAsync(client, options);
        if (updateResult != 0)
        {
            return updateResult;
        }
    }

    await Task.Delay(700);
    return await EndAsync(client, options);
}

static async Task PrintResponseAsync(HttpResponseMessage response)
{
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"{(int)response.StatusCode} {response.ReasonPhrase}");
    if (!string.IsNullOrWhiteSpace(body))
    {
        Console.WriteLine(body);
    }
}

static int PrintUnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    DemoOptions.PrintUsage();
    return 1;
}

internal sealed record DemoOptions
{
    public string Command { get; private init; } = "demo";

    public string Id { get; private init; } = "demo-progress";

    public string Title { get; private init; } = "Demo progress";

    public double Progress { get; set; }

    public int Port { get; private init; } = 8765;

    public string Token { get; private init; } = string.Empty;

    public static DemoOptions? Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return null;
        }

        var command = args[0].ToLowerInvariant();
        var options = new DemoOptions
        {
            Command = command
        };

        for (var i = 1; i < args.Length; i++)
        {
            if (i + 1 >= args.Length)
            {
                return null;
            }

            var value = args[++i];
            options = args[i - 1] switch
            {
                "--id" => options with { Id = value },
                "--title" => options with { Title = value },
                "--progress" when double.TryParse(value, out var progress) => options with { Progress = Math.Clamp(progress, 0, 1) },
                "--port" when int.TryParse(value, out var port) => options with { Port = port },
                "--token" => options with { Token = value },
                _ => null!
            };

            if (options is null)
            {
                return null;
            }
        }

        return string.IsNullOrWhiteSpace(options.Token) ? null : options;
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  WinLive.ApiDemo demo --token <token> [--port 8765] [--id demo-progress]");
        Console.WriteLine("  WinLive.ApiDemo create --token <token> [--progress 0.25]");
        Console.WriteLine("  WinLive.ApiDemo update --token <token> --progress 0.75");
        Console.WriteLine("  WinLive.ApiDemo end --token <token>");
    }
}
