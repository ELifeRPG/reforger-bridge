using ELifeRPG.Bridge.Api.Services;

namespace ELifeRPG.Bridge.Api.Endpoints;

public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("").WithTags("Health");

        group.MapGet("ping", () => Results.Ok(new PingResponse("pong")))
            .Produces<PingResponse>()
            .WithName("Ping")
            .WithDescription("Liveness check. Answers as long as the Bridge process is serving HTTP; makes no call to the Central API.");

        group.MapGet("health", (DependencyHealthCache cache) => Results.Ok(cache.Current))
            .Produces<HealthReport>()
            .WithName("Health")
            .WithDescription("Readiness snapshot: the result of the last background dependency scan. Makes no call of its own, so it answers instantly and cannot be used to hammer the Central API or Keycloak. Always 200 — the body carries the verdict. Until the first scan finishes, \"checkedAt\" is null and every status is \"unknown\"; a \"checkedAt\" much older than the scan interval means the scanner itself is wedged.");

        return app;
    }
}

/// <summary>
/// Fixed liveness response. Deliberately carries no state: a green ping means the Bridge is
/// serving, not that the Central API or Keycloak are reachable.
/// </summary>
public sealed record PingResponse(string Message);
