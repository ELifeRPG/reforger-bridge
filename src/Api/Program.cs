using ELifeRPG.BackendApiClient;
using ELifeRPG.Bridge.Api.Authentication;
using ELifeRPG.Bridge.Api.Configuration;
using ELifeRPG.Bridge.Api.Endpoints;
using ELifeRPG.Bridge.Api.OpenApi;
using ELifeRPG.Bridge.Api.Services;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<KeycloakOptions>(builder.Configuration.GetSection("Keycloak"));

builder.Services
    .AddHttpClient("Keycloak", (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<KeycloakOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
    })
    .AddStandardResilienceHandler();

// Singleton, not a typed HttpClient registration: BridgeTokenProvider caches the Bridge's own
// token in an instance field, which only works if one instance lives for the app's lifetime.
builder.Services.AddSingleton<BridgeTokenProvider>(serviceProvider =>
{
    var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("Keycloak");
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<KeycloakOptions>>();
    return new BridgeTokenProvider(httpClient, options);
});

builder.Services
    .AddHttpClient("CentralApi", (serviceProvider, client) =>
    {
        client.BaseAddress = new Uri(builder.Configuration["CentralApi:BaseUrl"]!);
    })
    .AddStandardResilienceHandler();

builder.Services.AddSingleton<EliferpgApiClient>(serviceProvider =>
{
    var tokenProvider = serviceProvider.GetRequiredService<BridgeTokenProvider>();
    var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
    var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("CentralApi");
    var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient)
    {
        BaseUrl = builder.Configuration["CentralApi:BaseUrl"],
    };
    return new EliferpgApiClient(adapter);
});

builder.Services.AddSingleton<PlayerSessionTracker>();

builder.Services.AddOpenApi("v1", options => options.AddSchemaTransformer<EnumSchemaTransformer>());

var app = builder.Build();

// Logs every request/response so we can see whether calls from the mod are actually arriving.
app.Use(async (context, next) =>
{
    var request = context.Request;
    var startedAt = DateTimeOffset.UtcNow;
    RequestLog.Write($"[{startedAt:HH:mm:ss.fff}] --> {request.Method} {request.Path}{request.QueryString} from {context.Connection.RemoteIpAddress}");

    // A non-JSON body is unexpected here and worth seeing in full, e.g. Reforger's RestContext
    // forcing application/x-www-form-urlencoded regardless of what was actually sent.
    if (request.ContentLength is > 0 && request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true)
    {
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        RequestLog.Write($"    body ({request.ContentType}): {await reader.ReadToEndAsync()}");
        request.Body.Position = 0;
    }

    await next();

    var elapsedMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
    RequestLog.Write($"[{DateTimeOffset.UtcNow:HH:mm:ss.fff}] <-- {context.Response.StatusCode} ({elapsedMs:F0}ms)");
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/docs", options =>
    {
        options.WithDynamicBaseServerUrl();
        options.AddDocuments("v1");
    });
}

// Lets the mod check the Bridge itself is reachable, independent of the Central API.
app.MapGet("health", () => Results.Ok(new { status = "ok" }))
    .WithTags("Health")
    .WithName("Health")
    .WithDescription("Liveness check for the Bridge itself.");

app.MapSessionEndpoints();
app.MapBankingEndpoints();
app.MapCharacterEndpoints();
app.MapCompanyEndpoints();
app.MapWhitelistEndpoints();

app.Run();

/// <summary>
/// Prints to console and appends to a log file, rotating that file once it passes
/// <see cref="MaxBytes"/> so it can't grow forever. Keeps one rotated generation
/// (<c>bridge-requests.log.1</c>) alongside the live one.
/// </summary>
internal static class RequestLog
{
    // Bump this if 2 MB of history isn't enough (or shrink it if it's too much).
    private const long MaxBytes = 2 * 1024 * 1024;

    private static readonly string FilePath = Path.Combine(Path.GetTempPath(), "bridge-requests.log");

    public static void Write(string line)
    {
        Console.WriteLine(line);

        if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
        {
            File.Copy(FilePath, FilePath + ".1", overwrite: true);
            File.WriteAllText(FilePath, string.Empty);
        }

        File.AppendAllText(FilePath, line + Environment.NewLine);
    }
}
