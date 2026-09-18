using GoSit.Application.Abstractions;
using GoSit.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace GoSit.Infrastructure.Security;

/// <summary>
/// Password hashing backed by ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>
/// (PBKDF2-HMAC-SHA512, 210k iterations under <c>IdentityV3</c>).
///
/// Identity supplies the hashing primitive only. GoSit deliberately does not use
/// IdentityDbContext or IdentityUser — see docs/ADR-002-identity-approach.md.
/// </summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(user: null!, password);

    public bool Verify(string password, string passwordHash, out bool rehashNeeded)
    {
        var result = _hasher.VerifyHashedPassword(user: null!, passwordHash, password);

        rehashNeeded = result == PasswordVerificationResult.SuccessRehashNeeded;
        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
