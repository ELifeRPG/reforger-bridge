using System.Globalization;
using Microsoft.Kiota.Abstractions.Serialization;

namespace ELifeRPG.Bridge.Api.Extensions;

/// <summary>
/// Several Central API response fields (bank fees, balances, transaction amounts, member counts)
/// come back from Kiota as UntypedNode rather than decimal/int, since Kiota doesn't support
/// OpenAPI's format: double/int32 cleanly (see MIGRATION.md's Bridge section). UntypedNode.GetValue()
/// looks like the way to unwrap one, but it's hidden (not overridden) by each concrete subtype
/// (UntypedInteger, UntypedDecimal, ...) — calling it through a variable/property statically typed
/// as the UntypedNode base, which is exactly what these DTOs declare, always throws
/// NotImplementedException from the base's own stub, regardless of the real runtime type. Confirmed
/// with a throwaway test reproducing Kiota's actual JSON deserialization path before writing this.
/// The fix is to pattern-match to the concrete subtype first. Which subtype shows up for a given
/// field also isn't fixed — a whole-number JSON literal (e.g. "2") deserializes as UntypedInteger,
/// one with a decimal point (e.g. "2.5") as UntypedDecimal — so both need handling for the same
/// logical field.
/// </summary>
public static class UntypedNodeExtensions
{
    public static decimal ToDecimal(this UntypedNode node) => node switch
    {
        UntypedDecimal d => d.GetValue(),
        UntypedDouble d => (decimal)d.GetValue(),
        UntypedFloat f => (decimal)f.GetValue(),
        UntypedInteger i => i.GetValue(),
        UntypedLong l => l.GetValue(),
        _ => throw new InvalidOperationException($"Unexpected untyped node type for a decimal field: {node.GetType().Name}"),
    };

    /// <summary>
    /// The write direction, for a request DTO whose int field Kiota also declares as UntypedNode —
    /// the message-ack sequences are the first. A whole number is always an UntypedInteger on the
    /// way out; the ambiguity described above is a deserialization problem, not a serialization one.
    /// </summary>
    public static UntypedNode ToUntypedNode(this int value) => new UntypedInteger(value);

    /// <summary>
    /// A notification's <c>Payload</c> is a string-to-string map in the Central API, but an open
    /// OpenAPI object reaches Kiota as an IAdditionalDataHolder whose values are <c>object</c> — and
    /// Kiota *sniffs a type out of the JSON content* rather than handing back what the schema said.
    /// A string holding a GUID deserializes as <see cref="Guid"/>, one holding a timestamp as
    /// <see cref="DateTimeOffset"/>; only text that resembles nothing stays a string. Messages'
    /// payload is two GUIDs, so the common case is the sniffed one, not the declared one.
    ///
    /// Each shape is therefore formatted back to the string the Central API actually sent —
    /// round-trip formats, not <c>ToString()</c>, so a GUID keeps its canonical form, a timestamp
    /// keeps ISO-8601 and a bool stays lowercase JSON rather than becoming "True". A nested object
    /// or array means the string-to-string contract itself broke, which is worth failing loudly for
    /// rather than papering over with a type name.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ToStringDictionary(this IAdditionalDataHolder? holder)
    {
        if (holder?.AdditionalData is not { Count: > 0 } data)
        {
            return new Dictionary<string, string>();
        }

        return data.ToDictionary(entry => entry.Key, entry => ToPayloadString(entry.Key, entry.Value));
    }

    private static string ToPayloadString(string key, object? value) => value switch
    {
        null or UntypedNull => string.Empty,
        string text => text,
        Guid guid => guid.ToString("D", CultureInfo.InvariantCulture),
        DateTimeOffset at => at.ToString("O", CultureInfo.InvariantCulture),
        DateTime at => at.ToString("O", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        UntypedString s => s.GetValue() ?? string.Empty,
        UntypedBoolean b => b.GetValue() ? "true" : "false",
        UntypedNode node => node.ToDecimal().ToString(CultureInfo.InvariantCulture),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException(
            $"Payload value for key '{key}' is neither a scalar nor null, so the string-to-string contract no longer holds: {value.GetType().Name}"),
    };

    public static int ToInt32(this UntypedNode node) => node switch
    {
        UntypedInteger i => i.GetValue(),
        UntypedLong l => (int)l.GetValue(),
        UntypedDecimal d => (int)d.GetValue(),
        _ => throw new InvalidOperationException($"Unexpected untyped node type for an int field: {node.GetType().Name}"),
    };
}
