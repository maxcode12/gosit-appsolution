namespace GoSit.Domain.Caregivers;

/// <summary>
/// The kind of coverage a caregiver's policy provides.
///
/// Which type a given service requires, and at what liability limit, is an open
/// insurance decision — see docs/BUSINESS_MODEL.md and docs/TRUST_AND_SAFETY.md. This
/// enum only names the categories a caregiver can submit a policy under.
/// </summary>
public enum InsuranceCoverageType
{
    GeneralLiability = 1,
    ProfessionalLiability = 2,

    /// <summary>Required only for a caregiver who transports a care recipient or a pet.</summary>
    Automobile = 3,

    Other = 4,
}
