using GoSit.Application.Abstractions;
using GoSit.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoSit.Infrastructure.Persistence;

/// <summary>
/// Configuration for the first staff account.
///
/// Read from configuration, never from source. Locally that means user secrets; deployed
/// it means Key Vault. Leaving these unset — which is the default — provisions nothing.
/// </summary>
public sealed class AdministratorSeedOptions
{
    public const string SectionName = "Administration:Bootstrap";

    public string? Email { get; init; }

    public string? Password { get; init; }

    public string FirstName { get; init; } = "GoSit";

    public string LastName { get; init; } = "Admin";
}

/// <summary>
/// Provisions the first administrator, so that staff routes are reachable at all.
///
/// Admin accounts cannot be self-registered — correctly, since they decide who is
/// allowed into a family's home — but until this existed there was no way to create one
/// either, which left every admin route unreachable by anyone and verification approval
/// possible only by editing the database directly.
///
/// The rules that keep this from becoming a back door:
///
///   - It does nothing unless both an email and a password are configured.
///   - It never touches an account that already exists. An operator cannot use it to
///     reset a password, and an attacker who gains configuration access cannot use it
///     to take over an existing admin — they would have to create a new one, which is
///     visible in the audit trail and the account list.
///   - The password is hashed with the same hasher as any other account and is never
///     logged, echoed, or stored in plain text.
/// </summary>
public sealed class AdministratorSeeder(
    GoSitDbContext context,
    IPasswordHasher passwordHasher,
    IDateTimeProvider clock,
    IOptions<AdministratorSeedOptions> options,
    ILogger<AdministratorSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var configured = options.Value;

        if (string.IsNullOrWhiteSpace(configured.Email)
            || string.IsNullOrWhiteSpace(configured.Password))
        {
            return;
        }

        var normalized = configured.Email.Trim().ToUpperInvariant();

        var exists = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.NormalizedEmail == normalized, cancellationToken);

        if (exists)
        {
            // Deliberately not an update. See the class remarks.
            return;
        }

        var admin = User.ProvisionAdministrator(
            configured.Email,
            passwordHasher.Hash(configured.Password),
            configured.FirstName,
            configured.LastName,
            clock.UtcNow);

        context.Users.Add(admin);
        await context.SaveChangesAsync(cancellationToken);

        // The account id, not the email — an email address is personal data and this
        // line goes to shared log storage.
        logger.LogInformation("Provisioned the bootstrap administrator account {UserId}.", admin.Id);
    }
}
