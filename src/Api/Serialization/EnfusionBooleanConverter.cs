using System.Text.Json;
using System.Text.Json.Serialization;

namespace ELifeRPG.Bridge.Api.Serialization;

/// <summary>
/// Enfusion's <c>JsonApiStruct.Pack()</c> writes bools as <c>1</c>/<c>0</c>, so reading accepts those
/// as well as <c>true</c>/<c>false</c>. Writing stays a real JSON boolean.
/// </summary>
public sealed class EnfusionBooleanConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number when reader.TryGetInt32(out var value) && value is 0 or 1 => value == 1,
            _ => throw new JsonException($"Expected a boolean or 0/1, got {reader.TokenType}."),
        };

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        => writer.WriteBooleanValue(value);
}
