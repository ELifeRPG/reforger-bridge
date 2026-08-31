using System.Net;
using System.Net.Sockets;

namespace ELifeRPG.Bridge.Api.Services;

public interface IDependencyProbe
{
    string Name { get; }

    Task<ProbeOutcome> ProbeAsync(CancellationToken cancellationToken);
}

public readonly record struct ProbeOutcome(HealthStatus Status, string? Detail);

public sealed class HttpDependencyProbe(string name, HttpClient httpClient, string path) : IDependencyProbe
{
    public string Name => name;

    public async Task<ProbeOutcome> ProbeAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        return ProbeOutcomes.FromStatusCode(response.StatusCode);
    }
}

public static class ProbeOutcomes
{
    public static ProbeOutcome FromStatusCode(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;

        if (code is >= 200 and < 300)
        {
            return new ProbeOutcome(HealthStatus.Healthy, null);
        }

        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new ProbeOutcome(HealthStatus.Healthy, null);
        }

        return code >= 500
            ? new ProbeOutcome(HealthStatus.Unhealthy, $"Responded {code}.")
            : new ProbeOutcome(HealthStatus.Degraded, $"Responded {code}.");
    }

    public static ProbeOutcome FromException(Exception exception) => exception switch
    {
        HttpRequestException request => new ProbeOutcome(HealthStatus.Unhealthy, $"Could not connect: {Describe(request)}."),
        _ => new ProbeOutcome(HealthStatus.Unhealthy, exception.GetType().Name),
    };

    private static string Describe(HttpRequestException exception)
    {
        if (exception.HttpRequestError != HttpRequestError.Unknown)
        {
            return exception.HttpRequestError.ToString();
        }

        var innermost = exception.GetBaseException();
        return innermost is SocketException socket ? socket.SocketErrorCode.ToString() : innermost.GetType().Name;
    }
}
