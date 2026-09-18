using GoSit.Application.Abstractions;
using GoSit.Domain.Subscriptions;
using GoSit.Domain.Users;

namespace GoSit.Application.Features.Subscriptions;

/// <summary>
/// What an account is currently entitled to.
///
/// The one rule that matters: entitlements are always resolved here, from stored state
/// and the clock. A client can tell us what plan it thinks it is on, and we ignore it.
/// Every feature gate goes through <see cref="HasAsync"/>.
/// </summary>
public sealed class EntitlementService(
    ISubscriptionRepository subscriptions,
    IUserRepository users,
    IDateTimeProvider clock)
{
    /// <summary>
    /// The features an account has right now. An account with no active subscription
    /// gets its audience's free plan, so there is no "not subscribed" special case for
    /// a caller to forget.
    /// </summary>
    public async Task<EntitlementSet> ResolveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return EntitlementSet.None;
        }

        var audience = user.Role == UserRole.Caregiver
            ? PlanAudience.Caregiver
            : PlanAudience.CareRequester;

        var subscription = await subscriptions.FindActiveForUserAsync(userId, cancellationToken);

        // Re-checked against the clock rather than trusting the stored status alone: a
        // subscription can lapse between the sweep runs that would tidy it up.
        if (subscription is not null && subscription.IsActiveAt(clock.UtcNow))
        {
            return new EntitlementSet(
                subscription.Plan.Code,
                subscription.Plan.Features,
                subscription.CurrentPeriodEndUtc);
        }

        var free = await subscriptions.FindFreePlanAsync(audience, cancellationToken);

        return free is null
            ? EntitlementSet.None
            : new EntitlementSet(free.Code, free.Features, null);
    }

    public async Task<bool> HasAsync(Guid userId, string featureCode, CancellationToken cancellationToken)
    {
        var entitlements = await ResolveAsync(userId, cancellationToken);
        return entitlements.Has(featureCode);
    }

    /// <summary>
    /// Which of these accounts hold a feature, in one query.
    ///
    /// For the cases that ask about many accounts at once — ranking a page of search
    /// results, ordering a caregiver's request queue. Calling <see cref="HasAsync"/> in
    /// a loop would put a round trip inside a sort.
    ///
    /// Only paid features can be answered this way: a free plan's features are held by
    /// everyone and are not stored per account, so nothing would come back for them.
    /// Every caller here is asking about a paid capability, which is the only kind
    /// worth ranking on.
    /// </summary>
    public Task<IReadOnlySet<Guid>> WhoHasAsync(
        IReadOnlyCollection<Guid> userIds,
        string featureCode,
        CancellationToken cancellationToken) =>
        subscriptions.ListUserIdsWithFeatureAsync(
            userIds, featureCode, clock.UtcNow, cancellationToken);
}

/// <summary>The plan an account is on and what it unlocks.</summary>
public sealed record EntitlementSet(
    string PlanCode,
    IReadOnlyList<string> Features,
    DateTime? CurrentPeriodEndUtc)
{
    /// <summary>Used when an account cannot be resolved. Entitles nothing.</summary>
    public static readonly EntitlementSet None = new("NONE", [], null);

    public bool Has(string featureCode) =>
        Features.Contains(featureCode, StringComparer.OrdinalIgnoreCase);
}
