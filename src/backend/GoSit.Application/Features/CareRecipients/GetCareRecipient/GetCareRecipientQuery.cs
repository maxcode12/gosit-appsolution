using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRequesters;

namespace GoSit.Application.Features.CareRecipients.GetCareRecipient;

/// <summary>
/// Returns one care recipient including their care needs — the most sensitive read in
/// the profile area.
/// </summary>
public sealed record GetCareRecipientQuery(Guid CareRecipientId) : IQuery<CareRecipientResponse>;

public sealed class GetCareRecipientHandler(
    ICareRequesterProfileRepository profiles,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : IQueryHandler<GetCareRecipientQuery, CareRecipientResponse>
{
    public async Task<CareRecipientResponse> HandleAsync(
        GetCareRecipientQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("That care recipient does not exist.");

        // The lookup runs against the caller's own profile, so a recipient belonging to
        // anyone else simply is not there. Reporting "not found" rather than "forbidden"
        // is deliberate: a 403 would confirm the id exists.
        var recipient = profile.FindCareRecipient(query.CareRecipientId)
            ?? throw new NotFoundException("That care recipient does not exist.");

        return recipient.ToResponse(DateOnly.FromDateTime(clock.UtcNow));
    }
}
