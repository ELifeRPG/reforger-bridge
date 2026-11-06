using System.Net;
using ELifeRPG.Bridge.Api.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Xunit;

namespace ELifeRPG.Bridge.Api.UnitTests.Extensions;

public sealed class BackendHttpClientExtensionsTests
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task UnsafeRequest_FailingWithServerError_IsSentOnlyOnce(string method)
    {
        var backend = new CountingBackend(HttpStatusCode.ServiceUnavailable);

        using var response = await SendAsync(backend, new HttpMethod(method));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, backend.Calls);
    }

    [Fact]
    public async Task GetRequest_FailingWithServerError_IsStillRetried()
    {
        var backend = new CountingBackend(HttpStatusCode.ServiceUnavailable);

        using var response = await SendAsync(backend, HttpMethod.Get);

        Assert.True(backend.Calls > 1, $"Expected retries, got {backend.Calls} call(s).");
    }

    private static async Task<HttpResponseMessage> SendAsync(CountingBackend backend, HttpMethod method)
    {
        var services = new ServiceCollection();
        services.AddBackendHttpClient(new Uri("http://backend.test"))
            .ConfigurePrimaryHttpMessageHandler(() => backend);
        // Skip the backoff so the retried GET finishes instantly.
        services.Configure<HttpStandardResilienceOptions>(
            $"{BackendHttpClientExtensions.ClientName}-standard",
            options => options.Retry.Delay = TimeSpan.Zero);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(BackendHttpClientExtensions.ClientName);

        return await client.SendAsync(new HttpRequestMessage(method, "bank-accounts/1/withdraw"));
    }

    private sealed class CountingBackend(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
