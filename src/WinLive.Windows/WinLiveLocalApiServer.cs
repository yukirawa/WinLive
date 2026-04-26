using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using WinLive.Core;

namespace WinLive.Windows;

public sealed class WinLiveLocalApiServer : ILocalApiServer
{
    private const long MaxPayloadBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILiveActivityStore _store;
    private readonly WinLiveSettings _settings;
    private WebApplication? _app;

    public WinLiveLocalApiServer(ILiveActivityStore store, WinLiveSettings settings)
    {
        _store = store;
        _settings = settings;
    }

    public bool IsRunning => _app is not null;

    public Uri? BaseAddress { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        _settings.Normalize();
        if (!_settings.ExternalApi.Enabled)
        {
            return;
        }

        var host = _settings.ExternalApi.Host;
        var port = _settings.ExternalApi.Port;
        var token = _settings.ExternalApi.AuthToken;
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = "WinLive.LocalApi"
        });

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = MaxPayloadBytes;
            options.Listen(IPAddress.Parse(host), port);
        });

        var app = builder.Build();
        Configure(app, token);

        await app.StartAsync(cancellationToken).ConfigureAwait(false);

        _app = app;
        BaseAddress = new Uri($"http://{host}:{port}");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_app is null)
        {
            return;
        }

        await _app.StopAsync(cancellationToken).ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
        _app = null;
        BaseAddress = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private void Configure(WebApplication app, string token)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.ContentLength > MaxPayloadBytes)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }

            if (!IsAuthorized(context, token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next().ConfigureAwait(false);
        });

        app.MapGet("/api/v1/health", () => Results.Ok(new
        {
            status = "ok"
        }));

        app.MapGet("/api/v1/activities", () => Results.Ok(_store.Activities.Select(ToResponse)));

        app.MapPut("/api/v1/activities/{id}", async (string id, HttpContext context) =>
        {
            var dto = await ReadJsonAsync<ExternalActivityDto>(context).ConfigureAwait(false);
            if (dto is null)
            {
                return Results.BadRequest(new ApiError("Invalid or empty JSON body."));
            }

            if (!TryBuildActivity(id, dto, existing: null, requireCoreFields: true, out var activity, out var error))
            {
                return Results.BadRequest(new ApiError(error));
            }

            var saved = _store.Upsert(activity);
            return Results.Ok(ToResponse(saved));
        });

        app.MapPatch("/api/v1/activities/{id}", async (string id, HttpContext context) =>
        {
            if (!_store.TryGetActivity(id, out var existing))
            {
                return Results.NotFound(new ApiError($"Activity '{id}' was not found."));
            }

            var dto = await ReadJsonAsync<ExternalActivityDto>(context).ConfigureAwait(false);
            if (dto is null)
            {
                return Results.BadRequest(new ApiError("Invalid or empty JSON body."));
            }

            if (!TryBuildActivity(id, dto, existing, requireCoreFields: false, out var activity, out var error))
            {
                return Results.BadRequest(new ApiError(error));
            }

            var saved = _store.Upsert(activity);
            return Results.Ok(ToResponse(saved));
        });

        app.MapDelete("/api/v1/activities/{id}", (string id) =>
        {
            return _store.Remove(id)
                ? Results.NoContent()
                : Results.NotFound(new ApiError($"Activity '{id}' was not found."));
        });
    }

    private static bool IsAuthorized(HttpContext context, string expectedToken)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header) || string.IsNullOrWhiteSpace(expectedToken))
        {
            return false;
        }

        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                header["Bearer ".Length..].Trim(),
                expectedToken,
                StringComparison.Ordinal);
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpContext context)
    {
        try
        {
            return await context.Request.ReadFromJsonAsync<T>(JsonOptions, context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return default;
        }
        catch (BadHttpRequestException)
        {
            return default;
        }
    }

    private static bool TryBuildActivity(
        string id,
        ExternalActivityDto dto,
        LiveActivity? existing,
        bool requireCoreFields,
        out LiveActivity activity,
        out string error)
    {
        activity = default!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(id))
        {
            error = "Activity id is required.";
            return false;
        }

        if (!TryResolveEnum(dto.Type, existing?.Type, requireCoreFields, "type", out LiveActivityType type, out error))
        {
            return false;
        }

        if (!TryResolveEnum(dto.State, existing?.State, requireCoreFields, "state", out LiveActivityState state, out error))
        {
            return false;
        }

        var title = dto.Title ?? existing?.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            error = "title is required.";
            return false;
        }

        var actions = dto.Actions is null
            ? existing?.Actions ?? Array.Empty<LiveActivityActionDescriptor>()
            : BuildActions(dto.Actions, out error);
        if (!string.IsNullOrEmpty(error))
        {
            return false;
        }

        activity = new LiveActivity
        {
            Id = id.Trim(),
            Type = type,
            State = state,
            Title = title.Trim(),
            Subtitle = dto.Subtitle ?? existing?.Subtitle,
            Progress = dto.Progress ?? existing?.Progress,
            Priority = dto.Priority ?? existing?.Priority ?? 0,
            Icon = dto.Icon ?? existing?.Icon,
            SourceApp = BuildSourceApp(dto.SourceApp, existing?.SourceApp),
            Media = BuildMedia(dto.Media, existing?.Media),
            Actions = actions,
            CreatedAt = existing?.CreatedAt ?? default,
            UpdatedAt = default,
            IsEmphasized = dto.IsEmphasized ?? existing?.IsEmphasized ?? false,
            EmphasizedUntil = existing?.EmphasizedUntil,
            Metadata = dto.Metadata ?? existing?.Metadata ?? new Dictionary<string, string>()
        };

        return true;
    }

    private static bool TryResolveEnum<T>(
        string? rawValue,
        T? existing,
        bool isRequired,
        string fieldName,
        out T value,
        out string error)
        where T : struct, Enum
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            if (existing.HasValue)
            {
                value = existing.Value;
                return true;
            }

            if (!isRequired)
            {
                value = default;
                return true;
            }

            value = default;
            error = $"{fieldName} is required.";
            return false;
        }

        if (Enum.TryParse<T>(rawValue, ignoreCase: true, out var parsed) &&
            Enum.IsDefined(parsed))
        {
            value = parsed;
            return true;
        }

        value = default;
        error = $"{fieldName} has an unsupported value '{rawValue}'.";
        return false;
    }

    private static IReadOnlyList<LiveActivityActionDescriptor> BuildActions(
        IReadOnlyList<ExternalActionDto> actions,
        out string error)
    {
        error = string.Empty;
        var result = new List<LiveActivityActionDescriptor>();

        foreach (var action in actions)
        {
            if (!Enum.TryParse<LiveActivityActionKind>(action.Kind, ignoreCase: true, out var kind) ||
                !Enum.IsDefined(kind))
            {
                error = $"Action kind '{action.Kind}' is unsupported.";
                return Array.Empty<LiveActivityActionDescriptor>();
            }

            result.Add(new LiveActivityActionDescriptor
            {
                Kind = kind,
                DisplayName = string.IsNullOrWhiteSpace(action.DisplayName)
                    ? kind.ToString()
                    : action.DisplayName,
                IsEnabled = action.IsEnabled ?? true
            });
        }

        return result;
    }

    private static LiveActivitySourceApp? BuildSourceApp(
        ExternalSourceAppDto? sourceApp,
        LiveActivitySourceApp? existing)
    {
        if (sourceApp is null)
        {
            return existing;
        }

        return new LiveActivitySourceApp
        {
            Name = sourceApp.Name ?? existing?.Name,
            AppUserModelId = sourceApp.AppUserModelId ?? existing?.AppUserModelId,
            ProcessId = sourceApp.ProcessId ?? existing?.ProcessId
        };
    }

    private static LiveActivityMediaInfo? BuildMedia(
        ExternalMediaDto? media,
        LiveActivityMediaInfo? existing)
    {
        if (media is null)
        {
            return existing;
        }

        return new LiveActivityMediaInfo
        {
            Artist = media.Artist ?? existing?.Artist,
            Album = media.Album ?? existing?.Album,
            AlbumArtBytes = TryDecodeBase64(media.AlbumArtBase64) ?? existing?.AlbumArtBytes,
            Position = media.PositionSeconds is null
                ? existing?.Position
                : TimeSpan.FromSeconds(media.PositionSeconds.Value),
            Duration = media.DurationSeconds is null
                ? existing?.Duration
                : TimeSpan.FromSeconds(media.DurationSeconds.Value)
        };
    }

    private static byte[]? TryDecodeBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static object ToResponse(LiveActivity activity)
    {
        return new
        {
            activity.Id,
            type = activity.Type.ToString(),
            state = activity.State.ToString(),
            activity.Title,
            activity.Subtitle,
            activity.Progress,
            activity.Priority,
            sourceApp = activity.SourceApp,
            updatedAt = activity.UpdatedAt
        };
    }

    private sealed record ApiError(string Message);

    private sealed class ExternalActivityDto
    {
        public string? Type { get; set; }

        public string? State { get; set; }

        public string? Title { get; set; }

        public string? Subtitle { get; set; }

        public double? Progress { get; set; }

        public int? Priority { get; set; }

        public string? Icon { get; set; }

        public bool? IsEmphasized { get; set; }

        public ExternalSourceAppDto? SourceApp { get; set; }

        public ExternalMediaDto? Media { get; set; }

        public IReadOnlyList<ExternalActionDto>? Actions { get; set; }

        public Dictionary<string, string>? Metadata { get; set; }
    }

    private sealed class ExternalSourceAppDto
    {
        public string? Name { get; set; }

        public string? AppUserModelId { get; set; }

        public int? ProcessId { get; set; }
    }

    private sealed class ExternalActionDto
    {
        public string? Kind { get; set; }

        public string? DisplayName { get; set; }

        public bool? IsEnabled { get; set; }
    }

    private sealed class ExternalMediaDto
    {
        public string? Artist { get; set; }

        public string? Album { get; set; }

        public string? AlbumArtBase64 { get; set; }

        public double? PositionSeconds { get; set; }

        public double? DurationSeconds { get; set; }
    }
}
