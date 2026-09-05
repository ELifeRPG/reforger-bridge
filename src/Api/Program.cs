using ELifeRPG.BackendApiClient;
using ELifeRPG.Bridge.Api;
using ELifeRPG.Bridge.Api.Authentication;
using ELifeRPG.Bridge.Api.Configuration;
using ELifeRPG.Bridge.Api.Endpoints;
using ELifeRPG.Bridge.Api.OpenApi;
using ELifeRPG.Bridge.Api.Serialization;
using ELifeRPG.Bridge.Api.Services;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<KeycloakOptions>(builder.Configuration.GetSection("Keycloak"));

builder.Services
    .AddOptions<DependencyHealthOptions>()
    .Bind(builder.Configuration.GetSection("DependencyHealth"))
    .Validate(
        options => options.ScanInterval > TimeSpan.Zero && options.ProbeTimeout > TimeSpan.Zero,
        "DependencyHealth ScanInterval and ProbeTimeout must be positive TimeSpans, e.g. \"00:01:00\".")
    .ValidateOnStart();

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
    .AddHttpClient("Backend", (serviceProvider, client) =>
    {
        client.BaseAddress = new Uri(builder.Configuration["Backend:BaseUrl"]!);
    })
    .AddStandardResilienceHandler();

builder.Services.AddSingleton<EliferpgApiClient>(serviceProvider =>
{
    var tokenProvider = serviceProvider.GetRequiredService<BridgeTokenProvider>();
    var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
    var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("Backend");
    var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient)
    {
        BaseUrl = builder.Configuration["Backend:BaseUrl"],
    };
    return new EliferpgApiClient(adapter);
});

builder.Services.AddSingleton<PlayerSessionTracker>();

const string backendProbeClient = "BackendProbe";
const string keycloakProbeClient = "KeycloakProbe";

builder.Services.AddHttpClient(backendProbeClient, client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Backend:BaseUrl"]!);
});

builder.Services.AddHttpClient(keycloakProbeClient, (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<KeycloakOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

builder.Services.AddSingleton<HttpDependencyProbe>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DependencyHealthOptions>>().Value;
    var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(backendProbeClient);
    return new HttpDependencyProbe(DependencyNames.Backend, httpClient, options.BackendProbePath);
});

builder.Services.AddSingleton<HttpDependencyProbe>(serviceProvider =>
{
    var keycloak = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<KeycloakOptions>>().Value;
    var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(keycloakProbeClient);
    return new HttpDependencyProbe(
        DependencyNames.Keycloak,
        httpClient,
        $"realms/{keycloak.Realm}/.well-known/openid-configuration");
});

builder.Services.AddSingleton<DependencyHealthCache>();
builder.Services.AddHostedService<DependencyHealthScanner>();

// Results.Ok<T> serializes through these, not through Program-level defaults — see BridgeJsonOptions
// for why a status must not reach the mod as an integer.
builder.Services.ConfigureHttpJsonOptions(options => BridgeJsonOptions.Configure(options.SerializerOptions));

builder.Services.AddOpenApi("v1", options => options.AddSchemaTransformer<EnumSchemaTransformer>());

var app = builder.Build();

app.UseManagementSurface();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/docs", options =>
    {
        options.WithDynamicBaseServerUrl();
        options.AddDocuments("v1");
    });
}

app.MapHealthEndpoints();
app.MapSessionEndpoints();
app.MapBankingEndpoints();
app.MapCharacterEndpoints();
app.MapCompanyEndpoints();
app.MapPhoneEndpoints();
app.MapPhoneContactEndpoints();
app.MapPhoneMessageEndpoints();

app.Run();
