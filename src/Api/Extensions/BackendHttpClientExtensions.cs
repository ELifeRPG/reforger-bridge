using Microsoft.Extensions.Http.Resilience;

namespace ELifeRPG.Bridge.Api.Extensions;

public static class BackendHttpClientExtensions
{
    public const string ClientName = "Backend";

    public static IHttpClientBuilder AddBackendHttpClient(this IServiceCollection services, Uri baseAddress)
    {
        var builder = services.AddHttpClient(ClientName, client => client.BaseAddress = baseAddress);

        // The backend has no duplicate check, so a retried booking would be booked twice.
        builder.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

        return builder;
    }
}
