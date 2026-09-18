namespace GoSit.Application.Abstractions;

/// <summary>
/// Name and status lookups for the staff queues.
///
/// Separate from the marketplace repositories on purpose. Everywhere else in GoSit a
/// person is a first name and a last initial, because that is all the other side of a
/// booking needs. Staff deciding whether to let someone into a family's home, or whether
/// to remove an account, need to know who the person actually is — so this returns more,
/// and lives behind an interface used only by admin-gated slices.
/// </summary>
public interface IAdminLookupRepository
{
    /// <summary>
    /// Accounts by id. Includes the email address and current status: staff need to be
    /// able to contact someone about a rejected check, and to see whether an account is
    /// already suspended before acting on it again.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, AdminUserSummary>> ListUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    /// <summary>Maps caregiver profile ids to the owning account.</summary>
    Task<IReadOnlyDictionary<Guid, Guid>> ListCaregiverOwnersAsync(
        IReadOnlyCollection<Guid> caregiverProfileIds,
        CancellationToken cancellationToken);
}

public sealed record AdminUserSummary(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    string Status);
