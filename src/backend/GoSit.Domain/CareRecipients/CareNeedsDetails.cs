namespace GoSit.Domain.CareRecipients;

/// <summary>
/// Input for updating a <see cref="CareNeedsProfile"/>. Every field is optional so a
/// requester can share only what a caregiver genuinely needs to know.
///
/// This is sensitive personal information. It must never be logged, cached in a
/// shared cache, or returned to anyone but the owning CareRequester and a caregiver
/// on a confirmed booking.
/// </summary>
public sealed record CareNeedsDetails
{
    public string? MobilityNeeds { get; init; }

    public string? CommunicationStyle { get; init; }

    public string? SensoryNeeds { get; init; }

    public string? BehaviourSupportNotes { get; init; }

    public string? DietaryRestrictions { get; init; }

    public string? Allergies { get; init; }

    public bool ToiletingAssistance { get; init; }

    public bool FeedingAssistance { get; init; }

    public string? RoutinePreferences { get; init; }

    public string? Triggers { get; init; }

    public string? CalmingStrategies { get; init; }

    public string? EmergencyInstructions { get; init; }

    /// <summary>
    /// Skill codes a caregiver must hold to be eligible for this recipient, checked
    /// against caregiver qualifications before a booking can be accepted.
    /// </summary>
    public IReadOnlyList<string> RequiredSkills { get; init; } = [];
}
