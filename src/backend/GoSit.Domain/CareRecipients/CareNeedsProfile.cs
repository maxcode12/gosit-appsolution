using GoSit.Domain.Common;

namespace GoSit.Domain.CareRecipients;

/// <summary>
/// The support a <see cref="CareRecipient"/> needs. Owned by the recipient — it has no
/// meaning or lifetime of its own.
///
/// Highly sensitive. See docs/ARCHITECTURE.md and the privacy rules in CLAUDE.md:
/// never publicly exposed, never logged, never placed in a shared cache.
/// </summary>
public sealed class CareNeedsProfile : Entity
{
    private const int MaxNoteLength = 2000;
    private const int MaxRequiredSkills = 30;

    private readonly List<string> _requiredSkills = [];

    private CareNeedsProfile()
    {
    }

    public string? MobilityNeeds { get; private set; }

    public string? CommunicationStyle { get; private set; }

    public string? SensoryNeeds { get; private set; }

    public string? BehaviourSupportNotes { get; private set; }

    public string? DietaryRestrictions { get; private set; }

    public string? Allergies { get; private set; }

    public bool ToiletingAssistance { get; private set; }

    public bool FeedingAssistance { get; private set; }

    public string? RoutinePreferences { get; private set; }

    public string? Triggers { get; private set; }

    public string? CalmingStrategies { get; private set; }

    public string? EmergencyInstructions { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyList<string> RequiredSkills => _requiredSkills.AsReadOnly();

    /// <summary>True when nothing has been recorded yet.</summary>
    public bool IsEmpty =>
        MobilityNeeds is null
        && CommunicationStyle is null
        && SensoryNeeds is null
        && BehaviourSupportNotes is null
        && DietaryRestrictions is null
        && Allergies is null
        && !ToiletingAssistance
        && !FeedingAssistance
        && RoutinePreferences is null
        && Triggers is null
        && CalmingStrategies is null
        && EmergencyInstructions is null
        && _requiredSkills.Count == 0;

    internal static CareNeedsProfile Empty() => new();

    internal void Update(CareNeedsDetails details, DateTime utcNow)
    {
        MobilityNeeds = Clean(details.MobilityNeeds, nameof(details.MobilityNeeds));
        CommunicationStyle = Clean(details.CommunicationStyle, nameof(details.CommunicationStyle));
        SensoryNeeds = Clean(details.SensoryNeeds, nameof(details.SensoryNeeds));
        BehaviourSupportNotes = Clean(details.BehaviourSupportNotes, nameof(details.BehaviourSupportNotes));
        DietaryRestrictions = Clean(details.DietaryRestrictions, nameof(details.DietaryRestrictions));
        Allergies = Clean(details.Allergies, nameof(details.Allergies));
        RoutinePreferences = Clean(details.RoutinePreferences, nameof(details.RoutinePreferences));
        Triggers = Clean(details.Triggers, nameof(details.Triggers));
        CalmingStrategies = Clean(details.CalmingStrategies, nameof(details.CalmingStrategies));
        EmergencyInstructions = Clean(details.EmergencyInstructions, nameof(details.EmergencyInstructions));

        ToiletingAssistance = details.ToiletingAssistance;
        FeedingAssistance = details.FeedingAssistance;

        var skills = details.RequiredSkills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (skills.Count > MaxRequiredSkills)
        {
            throw new DomainException($"At most {MaxRequiredSkills} required skills can be recorded.");
        }

        _requiredSkills.Clear();
        _requiredSkills.AddRange(skills);

        UpdatedAtUtc = utcNow;
    }

    private static string? Clean(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxNoteLength)
        {
            throw new DomainException($"{field} must be {MaxNoteLength} characters or fewer.");
        }

        return trimmed;
    }
}
