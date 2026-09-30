using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Virexaone.FMS.Backend.Hubs;
using Virexaone.FMS.Backend.Services;
using Virexaone.FMS.Backend.Utils;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
{
    try
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "backend-error.log");
        File.AppendAllText(logPath, $"{DateTimeOffset.Now:O} {eventArgs.ExceptionObject}{Environment.NewLine}");
        Console.Error.WriteLine($"Startup error. Details: {logPath}");
    }
    catch (Exception logError)
    {
        Console.Error.WriteLine($"Could not write backend-error.log: {logError.Message}");
    }
};

// 1. Configure PostgreSQL Connection Pool with NpgsqlDataSource
var config = builder.Configuration.GetSection("FmsSettings");
GeoTransform.Configure(config);
foreach (var key in new[] { "DbHost", "DbName", "DbUser", "DbPassword" })
{
    if (string.IsNullOrWhiteSpace(config[key]))
        throw new InvalidOperationException($"Missing FmsSettings:{key}. Configure it with environment variables or an ignored local settings file.");
}

var connStr = new NpgsqlConnectionStringBuilder
{
    Host = config["DbHost"],
    Port = config.GetValue("DbPort", 5432),
    Database = config["DbName"],
    Username = config["DbUser"],
    Password = config["DbPassword"],
    Pooling = true,
    MinPoolSize = 2,
    MaxPoolSize = 25,
    Timeout = 5,
    CommandTimeout = 30
}.ConnectionString;

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connStr);
var dataSource = dataSourceBuilder.Build();

builder.Services.AddSingleton(dataSource);
builder.Services.AddSingleton<EquipmentCategoryResolver>();
builder.Services.AddSingleton<UnitDimensionResolver>();
builder.Services.AddSingleton<FmsDataService>();
builder.Services.AddSingleton<FmsCatalogService>();
builder.Services.AddSingleton<FmsMapDraftService>();
builder.Services.AddSingleton<FmsRoadAuditService>();
builder.Services.AddSingleton<CabinCommsService>();
builder.Services.AddSingleton<LiveCabinCommsService>();
builder.Services.AddSingleton<CabinHexagonService>();
builder.Services.AddHttpClient<OpenMeteoWeatherService>();

// 2. Real-Time Streaming with SignalR & Background Service
builder.Services.AddSignalR();
builder.Services.AddHostedService<TelemetryStreamService>();

// A deployment serves one isolated site database until tenant-aware queries are available.
string authMode = builder.Configuration["Auth:Mode"] ?? "Google";
bool localProxyMode = string.Equals(authMode, "LocalProxy", StringComparison.OrdinalIgnoreCase);
bool requireAuth = string.Equals(authMode, "Google", StringComparison.OrdinalIgnoreCase) ||
    config.GetValue("RequireAuth", false);
if (!localProxyMode && !requireAuth)
    throw new InvalidOperationException("Auth:Mode must be Google or LocalProxy.");
if (!config.GetValue("DedicatedSiteDatabase", false))
    throw new InvalidOperationException("A dedicated site database is required until tenant isolation is implemented.");
if (requireAuth)
{
    string clientId = builder.Configuration["Google:ClientId"] ?? "";
    string[] subjects = builder.Configuration.GetSection("Access:AllowedGoogleSubjects").Get<string[]>() ?? [];
    if (string.IsNullOrWhiteSpace(clientId) || subjects.Length == 0)
        throw new InvalidOperationException("Google:ClientId and Access:AllowedGoogleSubjects are required for authenticated access.");

    var allowedSubjects = new HashSet<string>(subjects, StringComparer.Ordinal);
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = "https://accounts.google.com";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
                ValidateAudience = true,
                ValidAudience = clientId,
                ValidateLifetime = true
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    string? subject = context.Principal?.FindFirst("sub")?.Value;
                    if (subject == null || !allowedSubjects.Contains(subject))
                        context.Fail("Account is not assigned to this site.");
                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddAuthorization();
}

// Browser origins must be explicitly listed for each deployment.
builder.Services.AddCors(options =>
{
    options.AddPolicy("ConfiguredOrigins", policy =>
    {
        string[] origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length > 0) policy.WithOrigins(origins);
        policy.AllowAnyMethod().AllowAnyHeader();
    });
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    if (localProxyMode)
        serverOptions.Listen(System.Net.IPAddress.Any, builder.Configuration.GetValue("Backend:LocalPort", 8000));
    serverOptions.Limits.MaxConcurrentConnections = 10000;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = 10000;
});

var app = builder.Build();

if (localProxyMode)
{
    string[] trustedClients = builder.Configuration.GetSection("Backend:TrustedClientIps").Get<string[]>() ?? [];
    app.Logger.LogWarning("LocalProxy mode accepts requests without an application login. Restrict the reverse proxy or VPN.");
    app.Use(async (context, next) =>
    {
        if (!NetworkClientPolicy.IsTrusted(context.Connection.RemoteIpAddress, trustedClients))
        {
            app.Logger.LogWarning("Rejected request from untrusted peer {RemoteIp} for {Path}",
                context.Connection.RemoteIpAddress, context.Request.Path);
            context.Response.Headers["X-Virexa-Denied-By"] = "backend-ip-policy";
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "untrusted_network" });
            return;
        }
        await next();
    });
}
app.UseCors("ConfiguredOrigins");
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
if (requireAuth)
{
    app.UseAuthentication();
    app.UseAuthorization();
}
app.UseDefaultFiles();
app.UseStaticFiles();

// --- API ENDPOINTS ---

// Root & Health
app.MapGet("/", () => Results.Ok(new
{
    app = "Virexa One",
    version = "1.0.0",
    status = "ONLINE"
}));

app.MapGet("/health", async (FmsDataService fms) =>
{
    bool isAlive = await fms.CheckHealthAsync();
    int feedAgeSeconds = fms.FleetFeedAgeSeconds;
    bool feedStale = feedAgeSeconds > 120;
    return Results.Ok(new { status = isAlive && !feedStale ? "healthy" : "degraded" });
});

// Fleet APIs
var api = app.MapGroup("/api/v1");
if (requireAuth) api.RequireAuthorization();

api.MapGet("/ops/health", async (FmsDataService fms) =>
{
    bool databaseConnected = await fms.CheckHealthAsync();
    int feedAgeSeconds = fms.FleetFeedAgeSeconds;
    return Results.Ok(new
    {
        status = databaseConnected && feedAgeSeconds <= 120 ? "healthy" : "degraded",
        database_connected = databaseConnected,
        gps_feed_stale = feedAgeSeconds > 120,
        gps_feed_age_seconds = feedAgeSeconds
    });
});

api.MapGet("/site/profile", () => Results.Ok(new
{
    site_id = builder.Configuration["Site:Id"],
    display_name = builder.Configuration["Site:DisplayName"],
    utm_epsg = GeoTransform.UtmEpsg,
    site_latitude = GeoTransform.SiteLat,
    site_longitude = GeoTransform.SiteLon
}));

api.MapGet("/comms/status", (CabinCommsService comms) =>
    Results.Ok(new { enabled = comms.Enabled, mode = "live_pcm16_ws", sample_rate = 16000 }));

api.MapPost("/comms/live-ticket", async (HttpContext context, CabinCommsService comms,
    LiveCabinCommsService live, CabinPairInput input, CancellationToken token) =>
{
    if (!comms.Enabled) return Results.Json(new { error = "comms_disabled" }, statusCode: 503);
    bool dispatcher = comms.DispatcherAuthorized(context.Request.Headers["X-FMS-Dispatcher-Key"].ToString());
    if (!dispatcher)
    {
        if (!CabinCommsService.ValidUnit(input.unit_name) || input.unit_name == "ALL")
            return Results.BadRequest(new { error = "invalid_unit" });
        if (!await comms.UnitAuthorizedAsync(input.unit_name, context.Request.Headers["X-FMS-Unit-Key"].ToString(), token))
            return Results.Json(new { error = "forbidden" }, statusCode: 403);
    }
    string unitName = string.IsNullOrWhiteSpace(input.unit_name) ? "ALL" : input.unit_name;
    string ticket = live.IssueTicket(unitName, dispatcher ? "dispatcher" : "cabin");
    return Results.Ok(new { ticket, expires_in_seconds = 30 });
});

api.MapGet("/comms/live", async (HttpContext context, LiveCabinCommsService live,
    CabinCommsService comms, string? ticket) =>
{
    if (!comms.Enabled) { context.Response.StatusCode = 503; return; }
    await live.HandleAsync(context, ticket);
});

api.MapPost("/comms/pair", async (HttpContext context, CabinCommsService comms,
    CabinPairInput input, CancellationToken token) =>
{
    if (!comms.Enabled) return Results.Json(new { error = "comms_disabled" }, statusCode: 503);
    if (!comms.DispatcherAuthorized(context.Request.Headers["X-FMS-Dispatcher-Key"].ToString()))
        return Results.Json(new { error = "forbidden" }, statusCode: 403);
    if (!CabinCommsService.ValidUnit(input.unit_name) || input.unit_name == "ALL")
        return Results.BadRequest(new { error = "invalid_unit" });
    string? unitToken = await comms.PairUnitAsync(input.unit_name, token);
    return unitToken == null ? Results.NotFound(new { error = "unit_not_found" }) :
        Results.Ok(new { unit_name = input.unit_name, token = unitToken });
});

api.MapGet("/comms/messages", async (HttpContext context, CabinCommsService comms,
    string unit_name, CancellationToken token) =>
{
    if (!comms.Enabled) return Results.Json(new { error = "comms_disabled" }, statusCode: 503);
    if (!CabinCommsService.ValidUnit(unit_name))
        return Results.BadRequest(new { error = "invalid_unit" });
    bool dispatcher = comms.DispatcherAuthorized(context.Request.Headers["X-FMS-Dispatcher-Key"].ToString());
    if (!dispatcher && (unit_name == "ALL" || !await comms.UnitAuthorizedAsync(
        unit_name, context.Request.Headers["X-FMS-Unit-Key"].ToString(), token)))
        return Results.Json(new { error = "forbidden" }, statusCode: 403);
    return Results.Ok(new { status = "success", data = await comms.ListAsync(unit_name, dispatcher, token) });
});

api.MapPost("/comms/messages", async (HttpContext context, CabinCommsService comms,
    LiveCabinCommsService live,
    CabinTextInput input, CancellationToken token) =>
{
    if (!comms.Enabled) return Results.Json(new { error = "comms_disabled" }, statusCode: 503);
    if (!CabinCommsService.ValidUnit(input.unit_name))
        return Results.BadRequest(new { error = "invalid_unit" });
    bool dispatcher = comms.DispatcherAuthorized(context.Request.Headers["X-FMS-Dispatcher-Key"].ToString());
    if (!dispatcher && (input.unit_name == "ALL" || !await comms.UnitAuthorizedAsync(
        input.unit_name, context.Request.Headers["X-FMS-Unit-Key"].ToString(), token)))
        return Results.Json(new { error = "forbidden" }, statusCode: 403);
    var message = await comms.SendTextAsync(input.unit_name,
        dispatcher ? "dispatcher" : "cabin", input.body, input.priority, token);
    if (message != null) await live.PublishMessageAsync(message, token);
    return message == null ? Results.BadRequest(new { error = "invalid_message" }) :
        Results.Ok(new { status = "success", data = message });
});

api.MapGet("/fms/catalog", async (FmsCatalogService catalog, CancellationToken cancellationToken) =>
{
    var menus = await catalog.GetMenusAsync(cancellationToken);
    return Results.Ok(new { status = "success", count = menus.Count, data = menus });
});

api.MapGet("/fms/roads/audit", async (FmsRoadAuditService audit, string? search, string? scope,
    int? offset, int? limit, CancellationToken cancellationToken) =>
    Results.Ok(await audit.GetAsync(search, scope, offset ?? 0, limit ?? 50, cancellationToken)));

bool CanEditMap(HttpContext context)
{
    string? expected = builder.Configuration["FmsMapEditor:Key"];
    if (!builder.Configuration.GetValue("FmsMapEditor:Enabled", false) ||
        string.IsNullOrEmpty(expected) || expected.Length < 32 ||
        !context.Request.Headers.TryGetValue("X-FMS-Editor-Key", out var supplied))
        return false;
    byte[] left = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
    byte[] right = SHA256.HashData(Encoding.UTF8.GetBytes(supplied.ToString()));
    return CryptographicOperations.FixedTimeEquals(left, right);
}

api.MapGet("/fms/map-drafts", async (FmsMapDraftService drafts, CancellationToken cancellationToken) =>
    Results.Ok(new { status = "success", data = await drafts.ListAsync(cancellationToken) }));

api.MapGet("/fms/map-drafts/{id:guid}", async (Guid id, FmsMapDraftService drafts,
    CancellationToken cancellationToken) =>
{
    var item = await drafts.GetAsync(id, cancellationToken);
    return item == null ? Results.NotFound(new { error = "draft_not_found" }) : Results.Ok(item);
});

api.MapPost("/fms/map-drafts", async (HttpContext context, FmsMapDraftService drafts,
    MapDraftInput input, CancellationToken cancellationToken) =>
{
    if (!CanEditMap(context)) return Results.Json(new { error = "editor_access_denied" }, statusCode: 403);
    var (item, error) = await drafts.SaveAsync(null, input, cancellationToken);
    return error == null ? Results.Created($"/api/v1/fms/map-drafts/{item!.id}", item)
        : error == "code_already_exists" ? Results.Conflict(new { error })
        : Results.BadRequest(new { error });
});

api.MapPut("/fms/map-drafts/{id:guid}", async (Guid id, HttpContext context,
    FmsMapDraftService drafts, MapDraftInput input, CancellationToken cancellationToken) =>
{
    if (!CanEditMap(context)) return Results.Json(new { error = "editor_access_denied" }, statusCode: 403);
    var (item, error) = await drafts.SaveAsync(id, input, cancellationToken);
    return error == null ? Results.Ok(item)
        : error is "draft_changed_or_missing" or "code_already_exists" ? Results.Conflict(new { error })
        : Results.BadRequest(new { error });
});

api.MapDelete("/fms/map-drafts/{id:guid}", async (Guid id, DateTimeOffset revision,
    HttpContext context, FmsMapDraftService drafts, CancellationToken cancellationToken) =>
{
    if (!CanEditMap(context)) return Results.Json(new { error = "editor_access_denied" }, statusCode: 403);
    return await drafts.RetireAsync(id, revision, cancellationToken)
        ? Results.NoContent() : Results.Conflict(new { error = "draft_changed_or_missing" });
});

// Movement & Online Status Diagnostic Audit API
api.MapGet("/audit/movement", async (FmsDataService fms) =>
{
    var audit = await fms.GetMovementAuditAsync();
    return Results.Ok(audit);
});

api.MapGet("/audit/unit/{equipmentId:long}/history", async (long equipmentId, FmsDataService fms) =>
{
    var history = await fms.GetUnitGpsHistoryAsync(equipmentId);
    return Results.Ok(new { success = true, count = history.Count, history });
});

api.MapGet("/fleet/live", async (FmsDataService fms) =>
{
    var list = await fms.GetLiveFleetAsync();
    int feedAgeSeconds = fms.FleetFeedAgeSeconds;
    return Results.Ok(new
    {
        status = fms.FleetDataAvailable ? "success" : "degraded",
        server_time = DateTime.UtcNow.ToString("o"),
        feed_age_seconds = feedAgeSeconds,
        feed_stale = feedAgeSeconds > 120,
        count = list.Count,
        data = list
    });
});

api.MapGet("/fleet/summary", async (FmsDataService fms) =>
{
    var summary = await fms.GetFleetSummaryAsync();
    return Results.Ok(new { status = fms.FleetDataAvailable ? "success" : "degraded", data = summary });
});

api.MapGet("/fleet/types", async (FmsDataService fms) =>
{
    var fleet = await fms.GetLiveFleetAsync();
    var types = fleet.GroupBy(unit => new { unit.UnitType, unit.Category, unit.EquipmentTypeId })
        .Select(group => new
        {
            unit_type = group.Key.UnitType,
            category = group.Key.Category,
            equipment_type_id = group.Key.EquipmentTypeId,
            count = group.Count()
        })
        .OrderBy(item => item.unit_type)
        .ToList();
    return Results.Ok(new { status = fms.FleetDataAvailable ? "success" : "degraded", data = types });
});

api.MapGet("/fleet/inventory", async (FmsDataService fms) =>
{
    var units = await fms.GetFleetInventoryAsync();
    return Results.Ok(new { status = "success", count = units.Count, data = units });
});

api.MapGet("/cabin/hexagon/{unitName}", async (string unitName,
    CabinHexagonService hexagon, CancellationToken token) =>
{
    if (!CabinCommsService.ValidUnit(unitName) || unitName == "ALL")
        return Results.BadRequest(new { error = "invalid_unit" });
    var data = await hexagon.GetUnitAsync(unitName, token);
    return data == null ? Results.NotFound(new { error = "unit_not_found" }) :
        Results.Ok(new { status = "success", source = "hexagon_read_only", data });
});

// Dispatch APIs
api.MapGet("/mtc/live", async (FmsDataService fms) =>
{
    var assignments = await fms.GetActiveDispatchAsync(allowFallback: false);
    var fleet = await fms.GetLiveFleetAsync();
    bool available = fms.FleetDataAvailable;
    return Results.Ok(new
    {
        status = available && assignments.Count > 0 && fms.FleetFeedAgeSeconds <= 120 ? "success" : "degraded",
        generated_at = DateTimeOffset.UtcNow,
        gps_fresh_seconds = 120,
        data = new
        {
            assignments = available ? assignments : [],
            units = available ? fleet.Select(unit => new
            {
                unit_id = unit.UnitId,
                unit_name = unit.UnitName,
                category = unit.Category,
                easting = unit.Easting,
                northing = unit.Northing,
                gps_valid = unit.Latitude.HasValue && unit.Longitude.HasValue,
                gps_age_seconds = unit.LastHeardSecondsAgo,
                speed_kmh = unit.SpeedKmh,
                activity_name = unit.ActivityName
            }).ToArray() : []
        }
    });
});

api.MapGet("/dispatch/active", async (FmsDataService fms) =>
{
    var dispatches = await fms.GetActiveDispatchAsync();
    return Results.Ok(new { status = "success", count = dispatches.Count, data = dispatches });
});

// Production APIs
api.MapGet("/production/summary", async (FmsDataService fms) =>
{
    var prod = await fms.GetProductionSummaryAsync();
    return Results.Ok(new { status = prod.DataAvailable ? "success" : "degraded", data = prod });
});

api.MapGet("/weather/current", async (double? latitude, double? longitude, OpenMeteoWeatherService weather) =>
{
    if (!latitude.HasValue || !longitude.HasValue ||
        !double.IsFinite(latitude.Value) || !double.IsFinite(longitude.Value) ||
        Math.Abs(latitude.Value) > 90 || Math.Abs(longitude.Value) > 180 ||
        !GeoTransform.IsWithinSiteRadius(latitude.Value, longitude.Value, config.GetValue("WeatherRadiusKm", 30.0)))
        return Results.BadRequest(new { status = "invalid_location", data = (WeatherReportDto?)null });
    var report = await weather.GetCurrentAsync(latitude.Value, longitude.Value);
    return Results.Ok(new { status = report == null ? "unavailable" : "success", data = report });
});

// Locations APIs (Disposals, Fronts, CallPoints, All)
api.MapGet("/locations/all", async (FmsDataService fms) =>
{
    var locs = await fms.GetMiningLocationsAsync();
    return Results.Ok(new { status = "success", count = locs.Count, data = locs });
});

api.MapGet("/locations/actual", async (FmsDataService fms) =>
{
    var locs = await fms.GetMiningLocationsAsync(allowFallback: false);
    return Results.Ok(new { status = locs.Count > 0 ? "success" : "unavailable", count = locs.Count, data = locs });
});

api.MapGet("/locations/disposals", async (FmsDataService fms) =>
{
    var locs = await fms.GetMiningLocationsAsync("disposals");
    return Results.Ok(new { status = "success", count = locs.Count, data = locs });
});

api.MapGet("/locations/fronts", async (FmsDataService fms) =>
{
    var locs = await fms.GetMiningLocationsAsync("fronts");
    return Results.Ok(new { status = "success", count = locs.Count, data = locs });
});

api.MapGet("/locations/callpoints", async (FmsDataService fms) =>
{
    var locs = await fms.GetMiningLocationsAsync("callpoints");
    return Results.Ok(new { status = "success", count = locs.Count, data = locs });
});

// Roads / Hauling Network APIs
api.MapGet("/roads/network", async (FmsDataService fms) =>
{
    var roads = await fms.GetRoadNetworkAsync();
    return Results.Ok(new { status = "success", count = roads.Count, data = roads });
});

// SignalR Telemetry Hub
var telemetryHub = app.MapHub<TelemetryHub>("/hubs/telemetry");
if (requireAuth) telemetryHub.RequireAuthorization();

app.Run();
