using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using TradeTest.Api;
using TradeTest.Application;
using TradeTest.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
string root = builder.Configuration["TRADETEST_ROOT"] is { } configuredRoot ? Path.GetFullPath(configuredRoot)
    : DashboardSnapshotBuilder.FindRepositoryRoot(Directory.GetCurrentDirectory());
string urls = builder.Configuration["urls"] ?? "http://127.0.0.1:5080";
string? database = builder.Configuration["TRADETEST_DATABASE"];
var access = new ApiAccess(builder.Configuration["TRADETEST_API_TOKEN"]);
access.ValidateBindings(urls, database is not null);
builder.WebHost.UseUrls(urls);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 0);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
string[] origins = (builder.Configuration["TRADETEST_ALLOWED_ORIGINS"] ?? "http://127.0.0.1:5173;http://localhost:5173")
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
    uri.Scheme is not ("http" or "https") || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0))
    throw new ArgumentException("Allowed origins must be explicit HTTP(S) origins without paths or credentials.");
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(origins)
    .WithMethods("GET", "HEAD").WithHeaders("Authorization", "If-None-Match").WithExposedHeaders("ETag")));
var app = builder.Build();
app.UseCors();
app.UseResponseCompression();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    if (context.Request.Path.StartsWithSegments("/api") && !access.Allows(context.Request.Headers.Authorization))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        await context.Response.WriteAsJsonAsync(new { Error = "Unauthorized" }, context.RequestAborted);
        return;
    }
    try { await next(context); }
    catch (Exception exception) when (exception is SqliteException or InvalidDataException or FormatException)
    {
        app.Logger.LogError("Research read failed ({ErrorType}).", exception.GetType().Name);
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { Error = "Research storage is unavailable or failed integrity checks." }, context.RequestAborted);
        }
    }
});

// Compute once at startup; dashboard requests do not run replays, parse files, or call AI providers.
var snapshot = await DashboardSnapshotBuilder.BuildSyntheticAsync(root);
byte[] snapshotBytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, ResearchJson.OutputOptions);
string etag = "W/\"" + Convert.ToHexString(SHA256.HashData(snapshotBytes)).ToLowerInvariant() + '"';
SqliteStore? store = database is null ? null : new SqliteStore(Path.GetFullPath(database), readOnly: true);

app.MapGet("/health", () => Results.Ok(new { Status = "ok", Mode = "OFFLINE", BrokerConnected = false }));
app.MapMethods("/api/snapshot", ["GET", "HEAD"], (HttpContext context) =>
{
    context.Response.Headers.ETag = etag;
    context.Response.Headers.CacheControl = "private, no-cache";
    if (context.Request.Headers.IfNoneMatch.Any(value => value?.Split(',').Any(tag => tag.Trim() == etag || tag.Trim() == "*") == true))
        return Results.StatusCode(StatusCodes.Status304NotModified);
    return context.Request.Method == "HEAD" ? Results.Ok() : Results.Bytes(snapshotBytes, "application/json; charset=utf-8");
});
app.MapGet("/api/research/health", async (CancellationToken ct) => store is null
    ? Results.NotFound(new { Error = "No research database is configured." })
    : Results.Ok(await store.ReadResearchHealthSnapshotAsync(ct: ct)));
app.MapGet("/api/research/{securityId}", async (string securityId, string? asOf, string? query, CancellationToken ct) =>
{
    if (store is null) return Results.NotFound(new { Error = "No research database is configured." });
    if (securityId.Length > 128 || string.IsNullOrWhiteSpace(securityId) || (query?.Length ?? 0) > 500 ||
        !DateTimeOffset.TryParse(asOf, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
        return Results.BadRequest(new { Error = "Supply a security ID, an ISO asOf timestamp and a query up to 500 characters." });
    return Results.Ok(await store.ReadCompanySnapshotAsync(securityId, at, query ?? "", ct));
});
await app.RunAsync();

public partial class Program;
