using ELifeRPG.Bridge.Api.Endpoints;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ELifeRPG.Bridge.Api.OpenApi;

/// <summary>
/// The booking routes bind Idempotency-Key as a nullable header, so a missing one reaches the Central
/// API and comes back as its own 400. That leaves the header optional in the generated document; this
/// marks it required, so the mod's generated client asks for it.
/// </summary>
public sealed class IdempotencyKeyHeaderTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
        {
            if (parameter.In == ParameterLocation.Header && parameter.Name == BankingEndpoints.IdempotencyKeyHeader)
            {
                parameter.Required = true;
                parameter.Description =
                    "1 to 128 characters, unique per booking; no commas. Sending the same request again with the same key within "
                    + "24 hours returns the first response and books nothing; the same key with a different body gets 422.";
            }
        }

        return Task.CompletedTask;
    }
}
