using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ELifeRPG.Bridge.Api;

// Microsoft.AspNetCore.OpenApi doesn't emit x-enum-varnames for enum schemas (unlike NSwag), which
// is what lets OpenAPI-based client generators (e.g. Kiota) produce a named enum type instead of a
// bare integer. Tracked upstream: https://github.com/dotnet/aspnetcore/issues/63223.
public sealed class EnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;

        if (!type.IsEnum)
        {
            return Task.CompletedTask;
        }

        schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        schema.Extensions["x-enum-varnames"] = new JsonNodeExtension(JsonSerializer.SerializeToNode(Enum.GetNames(type))!);

        return Task.CompletedTask;
    }
}
