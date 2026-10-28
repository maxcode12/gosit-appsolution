namespace GoSit.Application.Abstractions;

/// <summary>
/// Checks a Google ID token — the signed proof, from Google, of who just signed in.
/// </summary>
public interface IGoogleIdTokenVerifier
{
    /// <summary>
    /// Returns the verified identity, or null when the token is forged, expired, issued
    /// for a different application, or otherwise not acceptable. Never throws for a bad
    /// token: a caller must answer the same way whatever was wrong with it.
    /// </summary>
    Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken);
}

/// <summary>What Google vouches for about a person who signed in.</summary>
public sealed record GoogleIdentity(
    string Subject,
    string Email,
    bool EmailVerified,
    string? GivenName,
    string? FamilyName);
