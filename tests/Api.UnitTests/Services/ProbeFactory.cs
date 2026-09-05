using System.Net;
using ELifeRPG.Bridge.Api.Services;

namespace ELifeRPG.Bridge.Api.UnitTests.Services;

internal static class ProbeFactory
{
    public static HttpDependencyProbe Responding(string name, HttpStatusCode statusCode)
        => Build(name, new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(statusCode))));

    public static HttpDependencyProbe Throwing(string name, Exception exception)
        => Build(name, new FakeHttpMessageHandler((_, _) => throw exception));

    public static HttpDependencyProbe Hanging(string name)
        => Build(name, new FakeHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

    private static HttpDependencyProbe Build(string name, HttpMessageHandler handler)
        => new(name, new HttpClient(handler) { BaseAddress = new Uri("http://dependency.test") }, "probe");

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request, cancellationToken);
    }
}
