using System.Security.Cryptography;
using System.Text;
using GoSit.Application.Features.Authentication.AccountRecovery;

namespace GoSit.Infrastructure.Security;

/// <summary>
/// Makes the tokens that go into verification and password-reset links.
///
/// **256 bits from a cryptographic RNG.** A password-reset token is a bearer credential
/// with the power to take over an account, so it gets the same entropy as a session
/// token — not a GUID, whose format leaks structure and whose randomness is not
/// guaranteed to be cryptographic.
///
/// Base64url encoded so it survives being pasted into a URL and back out of an email
/// client without needing escaping. `+`, `/` and `=` all get mangled somewhere along
/// that path.
/// </summary>
public sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    private const int TokenBytes = 32;

    public (string Token, string Hash) Generate()
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

        return (token, Hash(token));
    }

    /// <summary>
    /// SHA-256, unsalted and deliberately so.
    ///
    /// A password hash must be slow and salted because passwords are low-entropy and
    /// guessable. This token is 256 random bits: there is nothing to brute-force, and a
    /// per-token salt would make lookup-by-hash impossible without also storing a
    /// plaintext identifier. Same reasoning as refresh tokens, which hash the same way.
    /// </summary>
    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
