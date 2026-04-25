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
    public async Task ServerDoesNotStartWhenApiIsDisabled()
    {
        await using var server = new WinLiveLocalApiServer(new LiveSessionStore(), new WinLiveSettings());

        await server.StartAsync();

        Assert.False(server.IsRunning);
        Assert.Null(server.BaseAddress);
    }

    [Fact]
    public async Task ServerRequiresBearerToken()
    {
        var settings = SettingsWithApiEnabled();
        await using var server = new WinLiveLocalApiServer(new LiveSessionStore(), settings);
        await server.StartAsync();
        using var client = new HttpClient
        {
            BaseAddress = server.BaseAddress
        };

        using var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutPatchDeleteSessionFlowUpdatesStore()
    {
        var store = new LiveSessionStore();
        var settings = SettingsWithApiEnabled();
        await using var server = new WinLiveLocalApiServer(store, settings);
        await server.StartAsync();
        using var client = AuthorizedClient(server.BaseAddress!, settings.ExternalApi.AuthToken);

        using var put = await client.PutAsJsonAsync("/api/v1/sessions/demo", new
        {
            type = "genericProgress",
            title = "Download",
            state = "active",
            progress = 1.5,
            priority = 5
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(1, store.PrimarySession?.Progress);

        using var patch = await client.PatchAsJsonAsync("/api/v1/sessions/demo", new
        {
            title = "Download done",
            state = "completed",
            progress = 1
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal("Download done", store.PrimarySession?.Title);

        using var delete = await client.DeleteAsync("/api/v1/sessions/demo");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.False(store.HasVisibleSessions);
    }

    [Fact]
    public async Task PutRejectsMissingRequiredFields()
    {
        var settings = SettingsWithApiEnabled();
        await using var server = new WinLiveLocalApiServer(new LiveSessionStore(), settings);
        await server.StartAsync();
        using var client = AuthorizedClient(server.BaseAddress!, settings.ExternalApi.AuthToken);

        using var response = await client.PutAsJsonAsync("/api/v1/sessions/bad", new
        {
            title = "Missing type and state"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
        var client = new HttpClient
        {
            BaseAddress = baseAddress
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
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
