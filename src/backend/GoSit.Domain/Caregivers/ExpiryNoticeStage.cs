namespace GoSit.Domain.Caregivers;

/// <summary>
/// How far a caregiver has been warned that a certification is lapsing.
///
/// Ordered, and only ever advanced, so the sweeper that runs every quarter hour tells
/// someone once rather than repeatedly. Renewal is a new certification record, so nothing
/// here has to be wound back.
/// </summary>
public enum ExpiryNoticeStage
{
    /// <summary>Nothing said yet.</summary>
    None = 0,

    /// <summary>Warned while there was still time to renew.</summary>
    Expiring = 1,

    /// <summary>Told that it has lapsed and what that costs them.</summary>
    Expired = 2,
}
