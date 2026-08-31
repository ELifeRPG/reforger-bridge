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

        return app;
    }
}

/// <summary>
/// Fixed liveness response. Deliberately carries no state: a green ping means the Bridge is
/// serving, not that the Central API or Keycloak are reachable.
/// </summary>
public sealed record PingResponse(string Message);
