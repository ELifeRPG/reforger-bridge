using System.Net;
using System.Text;
using System.Text.Json;
using ELifeRPG.Bridge.Api.Authentication;
using ELifeRPG.Bridge.Api.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace ELifeRPG.Bridge.Api.UnitTests.Authentication;

public sealed class BridgeTokenProviderTests
{
    private static readonly KeycloakOptions Options = new()
    {
        BaseUrl = "http://keycloak.test/",
        Realm = "eliferpg",
        ClientId = "gameserver-dev",
        ClientSecret = "test-secret",
    };

    [Fact]
    public async Task ExchangeForPlayerTokenAsync_WithNonActiveStatus_ReturnsNullWithoutCallingKeycloak()
    {
        // The handler throws if invoked at all — proves the status gate short-circuits before any
        // HTTP call, matching ARCHITECTURE.md §4.3's claim that a blocked/not-whitelisted account
        // never even attempts the exchange.
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Should not call Keycloak for a non-active status."));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(Options.BaseUrl) };
        var provider = new BridgeTokenProvider(httpClient, Microsoft.Extensions.Options.Options.Create(Options));

        var result = await provider.ExchangeForPlayerTokenAsync("some-username", "blocked", CancellationToken.None);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("not_whitelisted")]
    [InlineData("blocked")]
    [InlineData("")]
    public async Task ExchangeForPlayerTokenAsync_WithAnyNonActiveStatus_ReturnsNull(string status)
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Should not call Keycloak for a non-active status."));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(Options.BaseUrl) };
        var provider = new BridgeTokenProvider(httpClient, Microsoft.Extensions.Options.Options.Create(Options));

        var result = await provider.ExchangeForPlayerTokenAsync("some-username", status, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExchangeForPlayerTokenAsync_WithActiveStatus_ExchangesOwnTokenForPlayerToken()
    {
        var requests = new List<string>();
        var handler = new FakeHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            requests.Add(body);

            // First call: Bridge's own client-credentials token. Second call: the token exchange.
            var isExchange = body.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Atoken-exchange");
            var accessToken = isExchange ? FakeJwt("player-jti-123") : FakeJwt("bridge-own-jti");

            return JsonResponse(new { access_token = accessToken, expires_in = 300 });
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(Options.BaseUrl) };
        var provider = new BridgeTokenProvider(httpClient, Microsoft.Extensions.Options.Options.Create(Options));

        var result = await provider.ExchangeForPlayerTokenAsync("some-username", "active", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("player-jti-123", result!.Jti);
        Assert.Equal(300, result.ExpiresInSeconds);
        Assert.Equal(2, requests.Count);
        Assert.Contains("grant_type=client_credentials", requests[0]);
        Assert.Contains("requested_subject=some-username", requests[1]);
        Assert.Contains("subject_token_type=urn%3Aietf%3Aparams%3Aoauth%3Atoken-type%3Aaccess_token", requests[1]);
    }

    [Fact]
    public async Task GetOwnTokenAsync_CachesUntilNearExpiry()
    {
        var callCount = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            callCount++;
            return JsonResponse(new { access_token = FakeJwt("bridge-own-jti"), expires_in = 300 });
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(Options.BaseUrl) };
        var provider = new BridgeTokenProvider(httpClient, Microsoft.Extensions.Options.Options.Create(Options));

        var first = await provider.GetOwnTokenAsync(CancellationToken.None);
        var second = await provider.GetOwnTokenAsync(CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Equal(1, callCount);
    }

    private static HttpResponseMessage JsonResponse(object body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };

    /// <summary>
    /// A syntactically valid, unsigned JWT with the given `jti` claim — enough for
    /// JwtSecurityTokenHandler.ReadJwtToken (structure-only parse, no signature validation) to read
    /// the Id property back out. Not a real Keycloak token; only used to satisfy the parser.
    /// </summary>
    private static string FakeJwt(string jti)
    {
        var header = Base64UrlEncode("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64UrlEncode($$"""{"jti":"{{jti}}"}""");
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(string json)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
