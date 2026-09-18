using GoSit.Domain.Growth;
using GoSit.Domain.Subscriptions;

namespace GoSit.Application.Abstractions;

public interface ISubscriptionRepository
{
    Task<IReadOnlyList<SubscriptionPlan>> ListPlansAsync(
        PlanAudience audience,
        CancellationToken cancellationToken);

    Task<SubscriptionPlan?> FindPlanByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>The free plan for an audience. Every audience has exactly one.</summary>
    Task<SubscriptionPlan?> FindFreePlanAsync(
        PlanAudience audience,
        CancellationToken cancellationToken);

    /// <summary>
    /// The account's current subscription, if any, with its plan loaded. Returns rows
    /// that are Active or PastDue; the caller re-checks against the clock.
    /// </summary>
    Task<UserSubscription?> FindActiveForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The subscription behind a provider reference, whatever its status.
    ///
    /// Deliberately not filtered to active rows, unlike
    /// <see cref="FindActiveForUserAsync"/>: a cancellation webhook is precisely an
    /// event about a subscription that is on its way out, and filtering it away would
    /// mean never applying the thing the event describes.
    /// </summary>
    Task<UserSubscription?> FindByProviderSubscriptionIdAsync(
        string providerSubscriptionId,
        CancellationToken cancellationToken);

    void Add(UserSubscription subscription);

    /// <summary>
    /// Which of these accounts hold <paramref name="featureCode"/> right now.
    ///
    /// One query for a whole page of results. Ranking a page of caregivers, or ordering
    /// a caregiver's request queue, needs the answer for every row at once; asking per
    /// row would put a database round trip inside a sort.
    /// </summary>
    Task<IReadOnlySet<Guid>> ListUserIdsWithFeatureAsync(
        IReadOnlyCollection<Guid> userIds,
        string featureCode,
        DateTime utcNow,
        CancellationToken cancellationToken);

    /// <summary>Subscriptions the sweep should close out.</summary>
    Task<IReadOnlyList<UserSubscription>> ListDueForExpiryAsync(
        DateTime utcNow,
        int limit,
        CancellationToken cancellationToken);
}

public interface IGrowthRepository
{
    Task<Referral?> FindByRefereeAsync(Guid refereeUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Referral>> ListByReferrerAsync(
        Guid referrerUserId,
        CancellationToken cancellationToken);

    void Add(Referral referral);

    Task<PromoCode?> FindPromoByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Tracked, so staff can deactivate it.</summary>
    Task<PromoCode?> FindPromoByIdAsync(Guid promoCodeId, CancellationToken cancellationToken);

    /// <summary>
    /// Every code, newest first. Inactive ones are included by default: a campaign that
    /// has been switched off is exactly what someone comes here to check, and hiding it
    /// makes a code look as though it never existed.
    /// </summary>
    Task<IReadOnlyList<PromoCode>> ListPromosAsync(CancellationToken cancellationToken);

    Task<bool> HasRedeemedAsync(Guid promoCodeId, Guid userId, CancellationToken cancellationToken);

    void Add(PromoCode promoCode);

    void Add(PromoRedemption redemption);
}
