namespace GoSit.Domain.CareServices;

/// <summary>
/// Where care happens, which decides whose address a booking carries and who may see it.
/// </summary>
public enum CareLocation
{
    /// <summary>At the family's home — the booking carries their address.</summary>
    RecipientHome = 1,

    /// <summary>
    /// At the caregiver's home — the booking carries theirs. Pet care only: care for people
    /// at a caregiver's home is the arrangement Ontario licenses as home child care.
    /// </summary>
    CaregiverHome = 2,
}
