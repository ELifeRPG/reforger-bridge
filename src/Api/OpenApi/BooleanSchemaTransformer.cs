using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ELifeRPG.Bridge.Api.OpenApi;

// A custom JsonConverter (EnfusionBooleanConverter) leaves bools untyped ({}) in the generated schema;
// the mod's client is generated from this document, so restore "boolean".
public sealed class BooleanSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        if (type == typeof(bool))
        {
            schema.Type = JsonSchemaType.Boolean;
        }
        else if (type == typeof(bool?))
        {
            schema.Type = JsonSchemaType.Boolean | JsonSchemaType.Null;
        }

        return Task.CompletedTask;
    }
}
