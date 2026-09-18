using System.ComponentModel.DataAnnotations;

namespace GoSit.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Signing key. Supplied from Azure Key Vault in every deployed environment and
    /// never from appsettings.json. Must be at least 32 bytes for HMAC-SHA256.
    /// </summary>
    [Required]
    [MinLength(32, ErrorMessage = "The JWT signing key must be at least 32 characters.")]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Access tokens are short-lived; clients refresh silently.</summary>
    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 14;
}
