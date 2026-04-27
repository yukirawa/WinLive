using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using WinLive.Core;
using WinLive.Windows;

namespace WinLive.Tests;

public sealed class LocalApiServerTests
{
    [Fact]
    public async Task ServerRequiresBearerToken()
    {
        var settings = SettingsWithApiEnabled();
        await using var server = new WinLiveLocalApiServer(new LiveActivityStore(), settings);
        await server.StartAsync();
        using var client = Client(server.BaseAddress!);

        using var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutPatchGetDeleteActivityFlowUpdatesStore()
    {
        var store = new LiveActivityStore();
        var settings = SettingsWithApiEnabled();
        await using var server = new WinLiveLocalApiServer(store, settings);
        await server.StartAsync();
        using var client = AuthorizedClient(server.BaseAddress!, settings.ExternalApi.AuthToken);

        using var put = await client.PutAsJsonAsync("/api/v1/activities/demo", new
        {
            type = "genericProgress",
            title = "Download",
            state = "active",
            progress = 1.5,
            priority = 5,
            sourceApp = new
            {
                name = "test"
            }
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(1, store.PrimaryActivity?.Progress);

        using var patch = await client.PatchAsJsonAsync("/api/v1/activities/demo", new
        {
            title = "Download done",
            state = "completed",
            progress = 1
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal("Download done", store.PrimaryActivity?.Title);

        using var get = await client.GetAsync("/api/v1/activities");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        using var delete = await client.DeleteAsync("/api/v1/activities/demo");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.False(store.HasVisibleActivities);
    }

    [Fact]
    public async Task PutRejectsMissingRequiredFields()
    {
        var settings = SettingsWithApiEnabled();
        await using var server = new WinLiveLocalApiServer(new LiveActivityStore(), settings);
        await server.StartAsync();
        using var client = AuthorizedClient(server.BaseAddress!, settings.ExternalApi.AuthToken);

        using var response = await client.PutAsJsonAsync("/api/v1/activities/bad", new
        {
            title = "Missing type and state"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("download", LiveActivityType.Download)]
    [InlineData("upload", LiveActivityType.Upload)]
    [InlineData("encode", LiveActivityType.Encode)]
    [InlineData("fileCopy", LiveActivityType.FileCopy)]
    [InlineData("timer", LiveActivityType.Timer)]
    [InlineData("install", LiveActivityType.Install)]
    [InlineData("genericProgress", LiveActivityType.GenericProgress)]
    public async Task PutAcceptsNonMediaActivityTypes(string type, LiveActivityType expected)
    {
        var store = new LiveActivityStore();
        var settings = SettingsWithApiEnabled();
        await using var server = new WinLiveLocalApiServer(store, settings);
        await server.StartAsync();
        using var client = AuthorizedClient(server.BaseAddress!, settings.ExternalApi.AuthToken);
        var id = $"demo-{type}";

        using var put = await client.PutAsJsonAsync($"/api/v1/activities/{id}", new
        {
            type,
            title = $"Demo {type}",
            state = "active",
            progress = 0.25
        });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.True(store.TryGetActivity(id, out var activity));
        Assert.Equal(expected, activity.Type);
    }

    private static WinLiveSettings SettingsWithApiEnabled()
    {
        return new WinLiveSettings
        {
            ExternalApi = new ExternalApiSettings
            {
                Enabled = true,
                Port = GetFreePort(),
                AuthToken = "test-token"
            }
        };
    }

    private static HttpClient AuthorizedClient(Uri baseAddress, string token)
    {
        var client = Client(baseAddress);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static HttpClient Client(Uri baseAddress)
    {
        return new HttpClient(new SocketsHttpHandler
        {
            UseProxy = false
        })
        {
            BaseAddress = baseAddress
        };
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
