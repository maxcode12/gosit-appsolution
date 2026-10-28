using GoSit.Domain.Users;

namespace GoSit.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Tracked lookup for operations that change an account — suspension, reinstatement.
    /// FindByIdAsync is deliberately no-tracking and cannot be used to mutate.
    /// </summary>
    Task<User?> FindTrackedByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Loads the user owning the given refresh token hash, with tokens included.</summary>
    Task<User?> FindByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Looks up the account that owns a referral code.</summary>
    Task<User?> FindByReferralCodeAsync(string referralCode, CancellationToken cancellationToken);

    /// <summary>
    /// Tracked lookup by email address, with emailed tokens loaded, for account
    /// recovery. Normalizes the address itself so callers cannot get that inconsistent.
    /// </summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Tracked lookup by the hash of an emailed token, with tokens loaded.
    ///
    /// Matched on hash because the plaintext is never stored. Purpose is part of the
    /// match so a verification token can never be redeemed as a password reset.
    /// </summary>
    Task<User?> FindByTokenHashAsync(
        UserTokenPurpose purpose,
        string tokenHash,
        CancellationToken cancellationToken);

    /// <summary>Tracked lookup by Google's stable account identifier.</summary>
    Task<User?> FindTrackedByGoogleSubjectAsync(string googleSubject, CancellationToken cancellationToken);

    /// <summary>Tracked lookup by normalized email, for linking an existing account.</summary>
    Task<User?> FindTrackedByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    void Add(User user);
}
