using System.Globalization;

namespace ELifeRPG.Bridge.Api.Formatting;

/// <summary>
/// Money crosses to the mod as text and comes back from it as integer cents. Enforce Script only has
/// 32-bit int and float: a float balance is off by cents above about 130,000, and int cents wrap above
/// 21,474,836.47. The mod never calculates with a balance, so the Bridge formats every money value it
/// sends from the exact decimal the backend returns, and the mod only sends amounts it caps itself.
///
/// The format is the one the mod's phone used before (ELIFE_PhoneBankingService.FormatMoney): a space
/// between thousands, a dot before the cents, the unit after the number, so the sign stays leading.
/// </summary>
public static class Money
{
    public const string CurrencySymbol = "Ŧ";

    private static readonly NumberFormatInfo DisplayFormat = new()
    {
        NumberGroupSeparator = " ",
        NumberDecimalSeparator = ".",
        NumberGroupSizes = [3],
        NegativeSign = "-",
        NumberNegativePattern = 1,
    };

    /// <summary>E.g. <c>"1 234.50 Ŧ"</c> or <c>"-42.00 Ŧ"</c>.</summary>
    public static string Format(decimal amount) => amount.ToString("N2", DisplayFormat) + " " + CurrencySymbol;

    /// <summary>
    /// A ledger row's amount as the account holder sees it: <c>-</c> when money left the account,
    /// <c>+</c> when it arrived, no sign for zero.
    /// </summary>
    public static string FormatSigned(decimal amount, bool outgoing)
    {
        var signed = outgoing ? -amount : amount;
        return signed > 0 ? "+" + Format(signed) : Format(signed);
    }

    public static decimal FromCents(int cents) => cents / 100m;
}
