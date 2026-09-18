namespace GoSit.Application.Features.Search;

/// <summary>
/// One caregiver in a search result.
///
/// Deliberately narrow. It carries no street address, no phone number, no certification
/// documents, and no account identifiers — only what a care requester needs to choose
/// between caregivers. Location is reduced to city and the postal code's forward
/// sortation area; the exact address is disclosed only on a confirmed booking.
/// </summary>
public sealed record CaregiverSearchResult(
    Guid CaregiverProfileId,
    string FirstName,
    // Last initial only: a full name is not needed to choose a caregiver.
    string LastInitial,
    string? Bio,
    int YearsOfExperience,
    decimal HourlyRate,
    // Price per night for overnight pet care; null for a caregiver who offers none.
    decimal? NightlyRate,
    string ApproximateLocation,
    // Null when either address has not been geocoded.
    double? DistanceKm,
    decimal? AverageRating,
    int ReviewCount,
    bool IsVerified,
    IReadOnlyList<string> ServiceCodes,
    IReadOnlyList<string> CertificationCodes,
    bool IsFavorite);

public sealed record CaregiverSearchResponse(
    IReadOnlyList<CaregiverSearchResult> Results,
    int TotalMatches,
    int Page,
    int PageSize,
    // True when ranking could not use distance because addresses are not geocoded.
    // Lets the client explain why results are not sorted by proximity.
    bool DistanceUnavailable,

    /// <summary>
    /// Why distance could not be used, or null when it could.
    ///
    /// Split from the flag because the two causes need opposite messages. "Add an
    /// address" is good advice to someone who has not given one and a lie to someone
    /// who has — and the second person is the one most likely to go looking for the
    /// setting they already filled in.
    /// </summary>
    string? DistanceUnavailableReason = null);

/// <summary>Why proximity ranking was not applied.</summary>
public static class DistanceUnavailableReasons
{
    /// <summary>No address on the profile. The one case the reader can fix.</summary>
    public const string NoAddress = "NoAddress";

    /// <summary>
    /// An address is on file but has no coordinates — no geocoding provider is
    /// configured, or it could not place it. Nothing for the reader to do.
    /// </summary>
    public const string AddressNotLocated = "AddressNotLocated";

    /// <summary>
    /// The reader is placed but none of the caregivers are. Also not theirs to fix.
    /// </summary>
    public const string CaregiversNotLocated = "CaregiversNotLocated";
}

/// <summary>
/// A caregiver's public detail page. Richer than a search result but still free of
/// exact address, contact details, and certification documents.
/// </summary>
public sealed record CaregiverDetailResponse(
    Guid CaregiverProfileId,
    string FirstName,
    string LastInitial,
    string? Bio,
    int YearsOfExperience,
    decimal HourlyRate,
    decimal? NightlyRate,
    string ApproximateLocation,
    int ServiceRadiusKm,
    decimal? AverageRating,
    int ReviewCount,
    bool IsVerified,

    /// <summary>
    /// Whether their checks clear them for people as well as pets. A caregiver cleared only
    /// for pets cannot be booked for a person, whatever services their profile lists.
    /// </summary>
    bool ClearedForPeopleCare,

    IReadOnlyList<CareServiceSummary> Services,
    IReadOnlyList<string> CertificationCodes,
    IReadOnlyList<AvailabilityWindowSummary> Availability,
    bool IsFavorite);

public sealed record CareServiceSummary(string Code, string Name, string CategoryCode);

public sealed record AvailabilityWindowSummary(DayOfWeek Day, TimeOnly StartTime, TimeOnly EndTime);
