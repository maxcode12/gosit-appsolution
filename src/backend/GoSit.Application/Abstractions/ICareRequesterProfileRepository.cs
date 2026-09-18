using GoSit.Domain.CareRequesters;

namespace GoSit.Application.Abstractions;

public interface ICareRequesterProfileRepository
{
    /// <summary>
    /// Loads the profile owned by the given account, including its care recipients and
    /// emergency contacts.
    ///
    /// Every read is scoped by user id rather than by profile id. That is the ownership
    /// boundary: a handler cannot accidentally load someone else's profile because it
    /// never gets to ask for one by primary key.
    /// </summary>
    Task<CareRequesterProfile?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> ExistsForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The emergency contacts recorded by one family, by profile id.
    ///
    /// The single exception to the rule above, and kept deliberately narrow rather than
    /// answered by adding a `FindByIdAsync`: a caregiver on a confirmed booking has to be
    /// able to reach someone, and they know the family only by the booking's profile id.
    /// Returning the contacts alone means this cannot become a general way to load a
    /// stranger's profile — which is exactly what the by-user-id rule exists to prevent.
    ///
    /// Who may call it, and when, is decided by
    /// <see cref="Features.Bookings.GetBookingEmergencyContacts.GetBookingEmergencyContactsQuery"/>,
    /// which checks participation and booking state and audits every read.
    /// </summary>
    Task<IReadOnlyList<EmergencyContact>> ListEmergencyContactsAsync(
        Guid careRequesterProfileId,
        CancellationToken cancellationToken);

    void Add(CareRequesterProfile profile);
}
