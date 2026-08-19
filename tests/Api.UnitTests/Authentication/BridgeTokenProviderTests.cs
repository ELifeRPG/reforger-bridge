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
    public async Task GetOwnTokenAsync_CachesUntilNearExpiry()
    {
        var callCount = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            callCount++;
            return JsonResponse(new { access_token = "bridge-own-token", expires_in = 300 });
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

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
