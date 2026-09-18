using System.Security.Cryptography;
using GoSit.Domain.Common;

namespace GoSit.Domain.Growth;

/// <summary>
/// Generates and validates referral codes.
///
/// Codes are random rather than derived from anything about the account, so one cannot
/// be guessed from an email address and cannot leak who referred whom. The alphabet
/// omits characters people confuse when reading a code aloud or off a screen.
/// </summary>
public static class ReferralCode
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int Length = 8;

    public static string Generate() =>
        new(Enumerable.Range(0, Length)
            .Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)])
            .ToArray());

    public static string Normalize(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("A referral code is required.");
        }

        // People type these by hand, so spaces and dashes are forgiven.
        return code.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).ToUpperInvariant();
    }
}
