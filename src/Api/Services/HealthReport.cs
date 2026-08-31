using System.Text.Json.Serialization;

namespace ELifeRPG.Bridge.Api.Services;

public sealed record HealthReport(
    HealthStatus Status,
    DateTimeOffset? CheckedAt,
    IReadOnlyList<DependencyHealth> Dependencies);

public sealed record DependencyHealth(
    string Name,
    HealthStatus Status,
    long? DurationMs,
    string? Detail);

public enum HealthStatus
{
    [JsonStringEnumMemberName("unknown")] Unknown = 0,

    [JsonStringEnumMemberName("healthy")] Healthy,

    [JsonStringEnumMemberName("degraded")] Degraded,

    [JsonStringEnumMemberName("unhealthy")] Unhealthy,
}

public static class DependencyNames
{
    public const string Backend = "backend";

    public const string Keycloak = "keycloak";
}

public static class HealthStatuses
{
    public static bool IsReady(HealthStatus status) => status == HealthStatus.Healthy;

    public static HealthStatus Aggregate(IReadOnlyCollection<HealthStatus> statuses)
    {
        if (statuses.Count == 0)
        {
            return HealthStatus.Unknown;
        }

        if (statuses.All(status => status == HealthStatus.Healthy))
        {
            return HealthStatus.Healthy;
        }

        if (statuses.All(status => status == HealthStatus.Unknown))
        {
            return HealthStatus.Unknown;
        }

        if (statuses.All(status => status == HealthStatus.Unhealthy))
        {
            return HealthStatus.Unhealthy;
        }

        return HealthStatus.Degraded;
    }
}
