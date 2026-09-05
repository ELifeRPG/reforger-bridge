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
            .WithDescription("In-game reachability check for the mod, on the public port. Answers as long as the Bridge process is serving HTTP; makes no call to the Central API or Keycloak. This is not a Kubernetes probe — see /health/live and /health/ready on the management port.");

        group.MapGet("health", (DependencyHealthCache cache) => Results.Ok(cache.Current))
            .Produces<HealthReport>()
            .WithName("Health")
            .WithDescription("Dependency report, on the management port: the result of the last background scan. Makes no call of its own, so it answers instantly and cannot be used to hammer the Central API or Keycloak. Always 200 — the body carries the verdict. Until the first scan finishes, \"checkedAt\" is null and every status is \"unknown\"; a \"checkedAt\" much older than the scan interval means the scanner itself is wedged. For Kubernetes, use /health/ready, which reports the same state as a status code.");

        group.MapGet("health/live", () => Results.Ok())
            .Produces(StatusCodes.Status200OK)
            .WithName("HealthLive")
            .WithDescription("Kubernetes liveness probe, on the management port. 200 for as long as the process serves HTTP, and deliberately depends on nothing else: a Central API or Keycloak outage must not restart the Bridge.");

        group.MapGet("health/ready", (DependencyHealthCache cache) =>
                HealthStatuses.IsReady(cache.Current.Status)
                    ? Results.Ok()
                    : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status503ServiceUnavailable)
            .WithName("HealthReady")
            .WithDescription("Kubernetes readiness probe, on the management port, driven by the last background dependency scan. 503 unless every dependency is healthy, and before the first scan completes. Carries no body; see /health for which dependency is at fault.");

        return app;
    }
}

public sealed record PingResponse(string Message);
