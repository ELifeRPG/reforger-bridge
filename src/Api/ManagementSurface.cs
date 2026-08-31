namespace ELifeRPG.Bridge.Api;

public static class ManagementSurface
{
    private const string HealthPrefix = "/health";

    public static bool IsAllowed(int localPort, PathString path, int managementPort)
    {
        var isHealthPath = path.StartsWithSegments(HealthPrefix, StringComparison.OrdinalIgnoreCase);
        return localPort == managementPort ? isHealthPath : !isHealthPath;
    }

    public static IApplicationBuilder UseManagementSurface(this WebApplication app)
    {
        var managementPort = new Uri(app.Configuration["Kestrel:Endpoints:Management:Url"]!).Port;

        return app.Use(async (context, next) =>
        {
            if (!IsAllowed(context.Connection.LocalPort, context.Request.Path, managementPort))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next(context);
        });
    }
}
