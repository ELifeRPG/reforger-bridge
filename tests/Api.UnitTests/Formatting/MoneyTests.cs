using ELifeRPG.Bridge.Api.Formatting;
using Xunit;

namespace ELifeRPG.Bridge.Api.UnitTests.Formatting;

/// <summary>
/// The mod shows these strings as they are, so the format is the contract: the phone's former
/// FormatMoney output, exact at any size.
/// </summary>
public sealed class MoneyTests
{
    [Theory]
    [InlineData("0", "0.00 Ŧ")]
    [InlineData("0.5", "0.50 Ŧ")]
    [InlineData("999.99", "999.99 Ŧ")]
    [InlineData("1000", "1 000.00 Ŧ")]
    [InlineData("1234567.89", "1 234 567.89 Ŧ")]
    [InlineData("-42", "-42.00 Ŧ")]
    [InlineData("-1234.5", "-1 234.50 Ŧ")]
    // Above the mod's int range in cents, and above 2^53 cents, where a double stops being exact.
    [InlineData("21474836.48", "21 474 836.48 Ŧ")]
    [InlineData("123456789012345678.91", "123 456 789 012 345 678.91 Ŧ")]
    public void Format_WritesTheModsFormat(string amount, string expected)
    {
        Assert.Equal(expected, Money.Format(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Format_IgnoresTheCurrentCulture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("1 234.50 Ŧ", Money.Format(1234.5m));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(12.5, false, "+12.50 Ŧ")]
    [InlineData(12.5, true, "-12.50 Ŧ")]
    [InlineData(0, false, "0.00 Ŧ")]
    [InlineData(0, true, "0.00 Ŧ")]
    public void FormatSigned_SignsByDirection(double amount, bool outgoing, string expected)
    {
        Assert.Equal(expected, Money.FormatSigned((decimal)amount, outgoing));
    }

    [Theory]
    [InlineData(12345, "123.45")]
    [InlineData(1, "0.01")]
    [InlineData(int.MaxValue, "21474836.47")]
    public void FromCents_IsExact(int cents, string expected)
    {
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), Money.FromCents(cents));
    }

    [Theory]
    [InlineData("0.20", "0.02", "0.20 Ŧ + 2 %")]
    [InlineData("1", "0.015", "1.00 Ŧ + 1.5 %")]
    [InlineData("0", "0.02", "2 %")]
    [InlineData("0.20", "0", "0.20 Ŧ")]
    [InlineData("0", "0", "0.00 Ŧ")]
    [InlineData("1000", "0.0025", "1 000.00 Ŧ + 0.25 %")]
    public void FormatFeeTerms_ShowsTheFixedPartAndThePercentage(string feeBase, string multiplier, string expected)
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;

        Assert.Equal(expected, Money.FormatFeeTerms(decimal.Parse(feeBase, invariant), decimal.Parse(multiplier, invariant)));
    }

    [Fact]
    public void FormatFeeTerms_IgnoresTheCurrentCulture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("1.00 Ŧ + 1.5 %", Money.FormatFeeTerms(1m, 0.015m));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
