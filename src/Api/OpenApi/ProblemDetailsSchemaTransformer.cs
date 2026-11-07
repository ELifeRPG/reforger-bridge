using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ELifeRPG.Bridge.Api.OpenApi;

/// <summary>
/// Declares the two extension members the Bridge passes on from the Central API (see
/// ApiCallExtensions.ToProblem), so the mod's generated ProblemDetails struct gets fields for them.
/// Both are optional: absent on problems the Central API doesn't mark, and an absent
/// <c>retryable</c> means false.
/// </summary>
public sealed class ProblemDetailsSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type != typeof(Microsoft.AspNetCore.Mvc.ProblemDetails))
        {
            return Task.CompletedTask;
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        schema.Properties["code"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Description = "Machine-readable reason, e.g. insufficient_balance or concurrent_modification.",
        };
        schema.Properties["retryable"] = new OpenApiSchema
        {
            Type = JsonSchemaType.Boolean | JsonSchemaType.Null,
            Description = "Whether resending the same request can succeed. Absent means false.",
        };

        return Task.CompletedTask;
    }
}
