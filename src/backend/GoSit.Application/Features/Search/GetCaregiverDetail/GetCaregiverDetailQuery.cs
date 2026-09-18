using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Caregivers;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Search.GetCaregiverDetail;

/// <summary>
/// The public detail page for one caregiver.
///
/// Only listable caregivers are returned. An unverified, delisted, or suspended
/// caregiver is reported as not found rather than forbidden, so the endpoint cannot be
/// used to discover that an account exists or to infer that it was rejected.
/// </summary>
public sealed record GetCaregiverDetailQuery(Guid CaregiverProfileId) : IQuery<CaregiverDetailResponse>;

public sealed class GetCaregiverDetailHandler(
    ICaregiverSearchRepository search,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverExposureRepository exposure,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<GetCaregiverDetailHandler> logger)
    : IQueryHandler<GetCaregiverDetailQuery, CaregiverDetailResponse>
{
    public async Task<CaregiverDetailResponse> HandleAsync(
        GetCaregiverDetailQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var caregiver = await search.FindListableByIdAsync(query.CaregiverProfileId, cancellationToken)
            ?? throw new NotFoundException("That caregiver is not available.");

        var today = DateOnly.FromDateTime(clock.UtcNow);

        var requesterProfile = await requesterProfiles.FindByUserIdAsync(userId, cancellationToken);

        var isFavorite = requesterProfile is not null
            && (await search.ListFavoriteCaregiverIdsAsync(requesterProfile.Id, cancellationToken))
                .Contains(caregiver.Id);

        var names = await search.ListCaregiverNamesAsync([caregiver.UserId], cancellationToken);
        var name = names.TryGetValue(caregiver.UserId, out var found) ? found : new CaregiverName("Caregiver", string.Empty);

        await RecordViewAsync(caregiver, userId, today, cancellationToken);

        return new CaregiverDetailResponse(
            caregiver.Id,
            name.FirstName,
            name.LastName.Length > 0 ? name.LastName[..1] : string.Empty,
            caregiver.Bio,
            caregiver.YearsOfExperience,
            caregiver.HourlyRate,
            caregiver.NightlyRate,
            caregiver.Address?.ToApproximate() ?? string.Empty,
            caregiver.ServiceRadiusKm,
            caregiver.AverageRating,
            caregiver.ReviewCount,
            caregiver.VerificationStatus == VerificationStatus.Verified,
            caregiver.ClearedForPeopleCare,
            caregiver.Services
                .Select(s => new CareServiceSummary(s.Code, s.Name, s.Category.Code))
                .OrderBy(s => s.Name)
                .ToList(),
            caregiver.ValidCertificationCodesOn(today).OrderBy(c => c).ToList(),
            caregiver.Availability
                .OrderBy(a => a.Day)
                .ThenBy(a => a.StartTime)
                .Select(a => new AvailabilityWindowSummary(a.Day, a.StartTime, a.EndTime))
                .ToList(),
            isFavorite);
    }

    /// <summary>
    /// Counts the view, and only the counting — nothing about who opened the page is
    /// stored. See <see cref="CaregiverExposure"/>.
    ///
    /// A caregiver looking at their own profile does not count — they check it constantly
    /// while setting it up, and a view figure that is mostly the caregiver refreshing
    /// their own page reads as interest that is not there.
    ///
    /// That check cannot fire today: this controller is CareRequesterOnly, so a caregiver
    /// is refused before reaching here. It is kept deliberately, because whether the
    /// profile page stays behind a login is an open question in PLAN.md, and the day it
    /// becomes public is the day this becomes load-bearing without anyone remembering it
    /// was needed.
    ///
    /// Failures are swallowed for the same reason as in search — a statistics row must
    /// never be the thing that stops a family reading about a caregiver.
    /// </summary>
    private async Task RecordViewAsync(
        CaregiverProfile caregiver,
        Guid viewerUserId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (caregiver.UserId == viewerUserId)
        {
            return;
        }

        try
        {
            await exposure.RecordProfileViewAsync(caregiver.Id, today, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "A profile view was not recorded");
        }
    }
}
