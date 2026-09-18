namespace GoSit.Domain.CareServices;

/// <summary>
/// How a service is priced. Almost everything is by the hour. Overnight pet care —
/// boarding and in-home sitting — is by the night, because billing a week's stay hour by
/// hour makes sense to nobody.
/// </summary>
public enum PricingUnit
{
    Hourly = 1,
    Nightly = 2,
}
