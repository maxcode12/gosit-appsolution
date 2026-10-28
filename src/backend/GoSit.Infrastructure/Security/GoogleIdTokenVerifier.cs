using System.ComponentModel.DataAnnotations;
using GoSit.Application.Abstractions;
using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoSit.Infrastructure.Security;

/// <summary>
/// Google sign-in configuration. The client IDs are public identifiers, not secrets.
/// </summary>
public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    /// <summary>
    /// OAuth client IDs this API accepts tokens for — one for the web app, and one per
    /// mobile platform once those ship. A token issued for any other application is
    /// refused, which is what stops another site's Google sign-in being replayed here.
    /// Empty turns Google sign-in off.
    /// </summary>
    public string[] ClientIds { get; set; } = [];

    public bool IsConfigured => ClientIds.Any(id => !string.IsNullOrWhiteSpace(id));
}

/// <summary>
/// Verifies Google ID tokens: signature against Google's published keys, expiry, issuer,
/// and that the token was issued to one of GoSit's own client IDs.
/// </summary>
public sealed class GoogleIdTokenVerifier(
    IOptions<GoogleAuthOptions> options,
    ILogger<GoogleIdTokenVerifier> logger) : IGoogleIdTokenVerifier
{
    private readonly GoogleAuthOptions _options = options.Value;

    public async Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            logger.LogWarning("Google sign-in was attempted but no client ID is configured");
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = _options.ClientIds.Where(id => !string.IsNullOrWhiteSpace(id)),
                });

            return new GoogleIdentity(
                payload.Subject,
                payload.Email,
                payload.EmailVerified,
                payload.GivenName,
                payload.FamilyName);
        }
        catch (InvalidJwtException)
        {
            // Forged, expired, or issued for someone else's application.
            logger.LogInformation("A Google ID token was rejected");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Could not fetch Google's keys; treated as a refusal rather than a crash.
            logger.LogWarning("Could not reach Google to verify a token ({ExceptionType})", ex.GetType().Name);
            return null;
        }
    }
}
