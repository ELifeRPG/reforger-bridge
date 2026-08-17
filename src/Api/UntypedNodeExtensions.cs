using Microsoft.Kiota.Abstractions.Serialization;

namespace ELifeRPG.Bridge.Api;

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

    public static int ToInt32(this UntypedNode node) => node switch
    {
        UntypedInteger i => i.GetValue(),
        UntypedLong l => (int)l.GetValue(),
        UntypedDecimal d => (int)d.GetValue(),
        _ => throw new InvalidOperationException($"Unexpected untyped node type for an int field: {node.GetType().Name}"),
    };
}
