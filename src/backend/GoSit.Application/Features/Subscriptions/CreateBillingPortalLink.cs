using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Subscriptions;
using GoSit.Domain.Users;

namespace GoSit.Application.Features.Subscriptions;

/// <summary>
/// A one-time link into the provider's billing portal.
///
/// Scoped to the caller's own customer — the route takes no id, so one account cannot
/// open another's billing. The link is short-lived and is never stored or logged.
/// </summary>
public sealed record CreateBillingPortalLinkQuery : IQuery<BillingPortalResponse>;

public sealed record BillingPortalResponse(string Url);

public sealed class CreateBillingPortalLinkHandler(
    IUserRepository users,
    ISubscriptionBillingProvider billing,
    IBillingReturnUrls returnUrls,
    ICurrentUser currentUser) : IQueryHandler<CreateBillingPortalLinkQuery, BillingPortalResponse>
{
    public async Task<BillingPortalResponse> HandleAsync(
        CreateBillingPortalLinkQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        if (!billing.IsLive)
        {
            throw new ConflictException("Billing is not configured.");
        }

        var user = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("The account no longer exists.");

        if (string.IsNullOrWhiteSpace(user.ProviderCustomerId))
        {
            // No customer means nothing has ever been billed. There is no portal to open.
            throw new NotFoundException("There is no billing history for this account.");
        }

        var audience = user.Role == UserRole.Caregiver
            ? PlanAudience.Caregiver
            : PlanAudience.CareRequester;

        var url = await billing.CreateBillingPortalUrlAsync(
            user.ProviderCustomerId,
            returnUrls.BillingPortalReturnUrl(audience),
            cancellationToken);

        return new BillingPortalResponse(url);
    }
}
