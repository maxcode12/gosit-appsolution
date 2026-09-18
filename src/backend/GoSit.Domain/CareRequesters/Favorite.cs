using GoSit.Domain.Common;

namespace GoSit.Domain.CareRequesters;

/// <summary>
/// A caregiver a care requester has saved. Private to the requester — a caregiver is
/// never told who has favourited them, and counts are never exposed.
/// </summary>
public sealed class Favorite : Entity
{
    private Favorite()
    {
    }

    public Guid CareRequesterProfileId { get; private set; }

    public Guid CaregiverProfileId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    // Public, unlike the other child entities: a favourite is a standalone association
    // created by the favorites repository rather than through the profile aggregate.
    public static Favorite Create(Guid careRequesterProfileId, Guid caregiverProfileId, DateTime utcNow)
    {
        if (caregiverProfileId == Guid.Empty)
        {
            throw new DomainException("A caregiver is required.");
        }

        return new Favorite
        {
            CareRequesterProfileId = careRequesterProfileId,
            CaregiverProfileId = caregiverProfileId,
            CreatedAtUtc = utcNow,
        };
    }
}
