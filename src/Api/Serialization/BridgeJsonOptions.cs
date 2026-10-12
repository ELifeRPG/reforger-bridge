using System.Text.Json;
using System.Text.Json.Serialization;

namespace ELifeRPG.Bridge.Api.Serialization;

/// <summary>
/// The Bridge's JSON contract with the mod. Both converters are load-bearing:
/// <list type="bullet">
/// <item>without <see cref="JsonStringEnumConverter"/>, System.Text.Json writes every enum as a bare
/// integer, so typing a status that used to be a string would silently turn <c>"status": "active"</c>
/// into <c>"status": 1</c>;</item>
/// <item>without <see cref="EnfusionBooleanConverter"/>, every request body the mod packs with a bool
/// in it is rejected, because Enfusion writes bools as <c>1</c>/<c>0</c>.</item>
/// </list>
///
/// It is a shared method rather than a lambda in Program.cs so the tests can serialize through the
/// exact options the app installs — asserting "the wire values did not change" against a
/// hand-rolled copy of the configuration would prove nothing about what the app actually emits.
/// </summary>
public static class BridgeJsonOptions
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new EnfusionBooleanConverter());
    }
}
