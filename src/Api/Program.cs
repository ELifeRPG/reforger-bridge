using ELifeRPG.Bridge.ApiClient;
using ELifeRPG.Bridge.Api;
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/docs", options =>
    {
        options.WithDynamicBaseServerUrl();
        options.AddDocuments("v1");
    });
}

app.MapSessionEndpoints();
app.MapBankingEndpoints();
app.MapCharacterEndpoints();
app.MapCompanyEndpoints();
app.MapWhitelistEndpoints();

app.Run();
