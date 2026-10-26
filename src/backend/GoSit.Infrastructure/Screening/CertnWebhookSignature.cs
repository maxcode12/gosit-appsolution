using System.Security.Cryptography;
using System.Text;

namespace GoSit.Infrastructure.Screening;

/// <summary>
/// Checks that a notification really came from Certn.
///
/// Certn signs the exact bytes it sends with HMAC-SHA256, using the key issued when the
/// notification address was added, and puts the result in the X-Signature header as hex. So
/// the body must be checked before it is parsed: re-serialising it first would change the
/// bytes. The comparison takes the same time whether or not it matches, so response time
/// teaches an attacker nothing about the correct value.
/// </summary>
public static class CertnWebhookSignature
{
    public static bool IsValid(ReadOnlySpan<byte> body, string? signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        byte[] supplied;

        try
        {
            supplied = Convert.FromHexString(signatureHeader.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);

        return CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    /// <summary>What Certn would send. Exposed so tests sign messages the same way.</summary>
    public static string Sign(ReadOnlySpan<byte> body, string secret) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body)).ToLowerInvariant();
}
