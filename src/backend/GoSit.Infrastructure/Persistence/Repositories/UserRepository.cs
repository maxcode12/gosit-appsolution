using GoSit.Application.Abstractions;
using GoSit.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(GoSitDbContext context) : IUserRepository
{
    public Task<User?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.Users
            .Include(u => u.RefreshTokens)
            .SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

    /// <summary>
    /// Read-only lookup for the current-user query: no tokens loaded and no change
    /// tracking, since nothing about the returned user is modified.
    /// </summary>
    public Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<User?> FindByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.Users
            .Include(u => u.RefreshTokens)
            .SingleOrDefaultAsync(
                u => u.RefreshTokens.Any(t => t.TokenHash == tokenHash),
                cancellationToken);

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        // Normalized here so every caller matches the same way the register flow stored it.
        var normalized = email.Trim().ToUpperInvariant();

        return context.Users
            .Include(u => u.RefreshTokens)
            .Include(u => u.Tokens)
            .SingleOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken);
    }

    public Task<User?> FindByTokenHashAsync(
        UserTokenPurpose purpose,
        string tokenHash,
        CancellationToken cancellationToken) =>
        context.Users
            .Include(u => u.RefreshTokens)
            .Include(u => u.Tokens)
            .SingleOrDefaultAsync(
                u => u.Tokens.Any(t => t.Purpose == purpose && t.TokenHash == tokenHash),
                cancellationToken);

    /// <summary>
    /// Both token collections are loaded. Issuing an emailed token supersedes any earlier
    /// unspent one, and an aggregate whose collection was not loaded would silently
    /// supersede nothing — leaving two live links to the same account.
    /// </summary>
    public Task<User?> FindTrackedByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Users
            .Include(u => u.RefreshTokens)
            .Include(u => u.Tokens)
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<User?> FindByReferralCodeAsync(string referralCode, CancellationToken cancellationToken) =>
        context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.ReferralCode == referralCode, cancellationToken);

    public Task<User?> FindTrackedByGoogleSubjectAsync(string googleSubject, CancellationToken cancellationToken) =>
        context.Users
            .Include(u => u.RefreshTokens)
            .SingleOrDefaultAsync(u => u.GoogleSubject == googleSubject, cancellationToken);

    public Task<User?> FindTrackedByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.Users
            .Include(u => u.RefreshTokens)
            .SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

    public void Add(User user) => context.Users.Add(user);
}
