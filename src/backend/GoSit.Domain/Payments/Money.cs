using GoSit.Domain.Common;

namespace GoSit.Domain.Payments;

/// <summary>
/// Conversion between the decimal amounts GoSit stores and the integer minor units
/// payment providers use.
///
/// This is a classic source of quiet financial bugs: a double somewhere, or a truncation
/// instead of a round, and every charge is a cent out. Everything goes through here, and
/// nothing else in the codebase multiplies a money value by 100.
/// </summary>
public static class Money
{
    public const string CanadianDollar = "CAD";

    /// <summary>Cents for CAD. Zero-decimal currencies would need their own factor.</summary>
    private const int MinorUnitsPerUnit = 100;

    /// <summary>
    /// Converts to minor units, rejecting anything that is not already a whole number
    /// of cents rather than silently rounding someone's money.
    /// </summary>
    public static long ToMinorUnits(decimal amount)
    {
        if (amount < 0)
        {
            throw new DomainException("A monetary amount cannot be negative.");
        }

        var minorUnits = amount * MinorUnitsPerUnit;

        if (decimal.Truncate(minorUnits) != minorUnits)
        {
            throw new DomainException(
                $"{amount} cannot be represented exactly in cents.");
        }

        return (long)minorUnits;
    }

    public static decimal FromMinorUnits(long minorUnits)
    {
        if (minorUnits < 0)
        {
            throw new DomainException("A monetary amount cannot be negative.");
        }

        return (decimal)minorUnits / MinorUnitsPerUnit;
    }

    /// <summary>Rounds to cents, away from zero — the convention for billing.</summary>
    public static decimal Round(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
