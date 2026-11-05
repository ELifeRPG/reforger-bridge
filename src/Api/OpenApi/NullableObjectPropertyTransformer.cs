using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ELifeRPG.Bridge.Api.OpenApi;

// A nullable DTO property (e.g. MessageThreadSummary.LastMessage) is emitted as oneOf [null, $ref].
// enfusion-codegen, which generates the mod's client from this document, reads neither oneOf nor
// anyOf, so the field would come out with no type at all. A bare $ref gives it the DTO's class, and an
// Enforce Script class reference can be null anyway.
public sealed class NullableObjectPropertyTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        foreach (var schema in document.Components?.Schemas?.Values ?? [])
        {
            if (schema.Properties is not { } properties)
            {
                continue;
            }

            foreach (var (name, property) in properties.ToList())
            {
                if (NullableReferenceTarget(property) is { } reference)
                {
                    properties[name] = reference;
                }
            }
        }

        return Task.CompletedTask;
    }

    private static OpenApiSchemaReference? NullableReferenceTarget(IOpenApiSchema property) =>
        property.OneOf is { Count: 2 } members && members.Any(member => member.Type == JsonSchemaType.Null)
            ? members.OfType<OpenApiSchemaReference>().SingleOrDefault()
            : null;
}
