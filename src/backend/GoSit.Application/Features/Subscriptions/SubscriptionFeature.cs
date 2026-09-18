using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.Growth;
using GoSit.Domain.Subscriptions;
using GoSit.Domain.Users;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Subscriptions;

public sealed record ListPlansQuery : IQuery<IReadOnlyList<PlanResponse>>;

public sealed record GetMySubscriptionQuery : IQuery<MySubscriptionResponse>;

/// <summary>
/// Subscribes to a paid plan. An optional promo code is validated and redeemed here —
/// the discount is computed server-side from the plan's price.
/// </summary>
public sealed record SubscribeCommand(string PlanCode, string? PromoCode)
    : ICommand<SubscribeResult>;

/// <summary>
/// What subscribing produced.
///
/// Two shapes, because two things can happen. With billing configured the answer is
/// "go here and pay", and nothing has been granted; without it the plan simply started.
/// A single response type carrying an optional URL would have let a caller treat a
/// redirect as a completed subscription, which is exactly the mistake worth preventing.
/// </summary>
public sealed record SubscribeResult(string? CheckoutUrl, MySubscriptionResponse? Subscription)
{
    public static SubscribeResult RedirectTo(string checkoutUrl) => new(checkoutUrl, null);

    public static SubscribeResult Started(MySubscriptionResponse subscription) =>
        new(null, subscription);
}

public sealed record CancelSubscriptionCommand : ICommand<MySubscriptionResponse>;

public sealed record PlanResponse(
    string Code,
    string Name,
    string Audience,
    decimal MonthlyPrice,
    string Currency,
    bool IsFree,
    IReadOnlyList<string> Features);

/// <summary>
/// What the caller is on. <see cref="Features"/> is the authoritative list — a client
/// should gate its UI on this rather than inferring from the plan code.
/// </summary>
public sealed record MySubscriptionResponse(
    string PlanCode,
    string PlanName,
    string Status,
    IReadOnlyList<string> Features,
    DateTime? CurrentPeriodEndUtc,
    bool CancelAtPeriodEnd,
    decimal? AmountCharged,
    decimal? DiscountApplied,

    /// <summary>
    /// Whether real billing is configured.
    ///
    /// The UI reads this rather than assuming. With no provider a plan grants its
    /// features without any payment being taken, and telling someone they are being
    /// charged when they are not would be a lie — so the screen says which world it is
    /// in instead of guessing.
    /// </summary>
    bool BillingEnabled = false,

    /// <summary>True when this account has a provider customer to open a portal for.</summary>
    bool HasBillingPortal = false);

public sealed class SubscribeValidator : AbstractValidator<SubscribeCommand>
{
    public SubscribeValidator()
    {
        RuleFor(x => x.PlanCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.PromoCode).MaximumLength(32);
    }
}

public sealed class ListPlansHandler(
    ISubscriptionRepository subscriptions,
    IUserRepository users,
    ICurrentUser currentUser) : IQueryHandler<ListPlansQuery, IReadOnlyList<PlanResponse>>
{
    public async Task<IReadOnlyList<PlanResponse>> HandleAsync(
        ListPlansQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var user = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("The account no longer exists.");

        // Only the plans for this side of the marketplace: a caregiver has no use for a
        // requester plan, and showing it invites buying the wrong thing.
        var audience = user.Role == UserRole.Caregiver
            ? PlanAudience.Caregiver
            : PlanAudience.CareRequester;

        var plans = await subscriptions.ListPlansAsync(audience, cancellationToken);

        return plans
            .Select(p => new PlanResponse(
                p.Code, p.Name, p.Audience.ToString(), p.MonthlyPrice,
                p.Currency, p.IsFree, p.Features))
            .ToList();
    }
}

public sealed class GetMySubscriptionHandler(
    EntitlementService entitlements,
    ISubscriptionRepository subscriptions,
    IUserRepository users,
    ISubscriptionBillingProvider billing,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IQueryHandler<GetMySubscriptionQuery, MySubscriptionResponse>
{
    public async Task<MySubscriptionResponse> HandleAsync(
        GetMySubscriptionQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var resolved = await entitlements.ResolveAsync(userId, cancellationToken);
        var subscription = await subscriptions.FindActiveForUserAsync(userId, cancellationToken);

        var isLive = subscription is not null && subscription.IsActiveAt(clock.UtcNow);

        // Without a live subscription the caller is on the free plan, and its display
        // name has to be looked up — falling back to the plan *code* put "FREE_REQUESTER"
        // on screen where a person expects "GoSit".
        var planName = isLive
            ? subscription!.Plan.Name
            : (await subscriptions.FindPlanByCodeAsync(resolved.PlanCode, cancellationToken))?.Name
              ?? resolved.PlanCode;

        var user = await users.FindByIdAsync(userId, cancellationToken);

        return new MySubscriptionResponse(
            resolved.PlanCode,
            planName,
            isLive ? subscription!.Status.ToString() : "Free",
            resolved.Features,
            resolved.CurrentPeriodEndUtc,
            isLive && subscription!.CancelAtPeriodEnd,
            AmountCharged: null,
            DiscountApplied: null,
            BillingEnabled: billing.IsLive,
            HasBillingPortal: billing.IsLive
                && !string.IsNullOrWhiteSpace(user?.ProviderCustomerId));
    }
}

public sealed class SubscribeHandler(
    ISubscriptionRepository subscriptions,
    IGrowthRepository growth,
    IUserRepository users,
    ISubscriptionBillingProvider billing,
    IBillingReturnUrls returnUrls,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<SubscribeHandler> logger) : ICommandHandler<SubscribeCommand, SubscribeResult>
{
    public async Task<SubscribeResult> HandleAsync(
        SubscribeCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var user = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("The account no longer exists.");

        var plan = await subscriptions.FindPlanByCodeAsync(command.PlanCode, cancellationToken)
            ?? throw new NotFoundException("That plan is not available.");

        var audience = user.Role == UserRole.Caregiver
            ? PlanAudience.Caregiver
            : PlanAudience.CareRequester;

        // A caregiver must not be able to buy a requester plan by sending its code.
        if (plan.Audience != audience)
        {
            throw new ForbiddenException("That plan is not available for this account.");
        }

        if (plan.IsFree)
        {
            throw new ConflictException("The free plan does not need a subscription.");
        }

        var utcNow = clock.UtcNow;

        var existing = await subscriptions.FindActiveForUserAsync(userId, cancellationToken);

        if (existing is not null && existing.IsActiveAt(utcNow))
        {
            throw new ConflictException("This account already has an active subscription.");
        }

        /*
         * With billing configured, subscribing does not grant anything — it starts a
         * hosted checkout and hands back a URL. The subscription becomes real when the
         * provider says it has been paid for, over a verified webhook.
         *
         * This is the whole point of wiring Stripe up. Activating here and trusting the
         * browser to complete payment would mean anyone who called this endpoint and
         * closed the tab kept the plan for free.
         */
        if (billing.IsLive)
        {
            if (!plan.IsPurchasable)
            {
                throw new ConflictException(
                    "That plan cannot be purchased yet. Please try again later.");
            }

            var checkout = await StartCheckoutAsync(user, plan, cancellationToken);

            // Persist the customer link created above, and nothing else — no
            // subscription row exists until the webhook arrives.
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return SubscribeResult.RedirectTo(checkout.Url);
        }

        // No billing provider configured — local development and the integration tests.
        // The plan starts immediately and nothing is charged, which the UI states
        // plainly rather than implying a payment that did not happen.
        var discount = await ApplyPromoAsync(
            command.PromoCode, plan, userId, audience, utcNow, cancellationToken);

        var subscription = UserSubscription.Start(userId, plan, utcNow);
        subscriptions.Add(subscription);

        audit.Record(nameof(UserSubscription), subscription.Id, "SubscriptionStarted", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Account {UserId} subscribed to {PlanCode} without billing", userId, plan.Code);

        return SubscribeResult.Started(new MySubscriptionResponse(
            plan.Code,
            plan.Name,
            subscription.Status.ToString(),
            plan.Features,
            subscription.CurrentPeriodEndUtc,
            subscription.CancelAtPeriodEnd,
            plan.MonthlyPrice - discount,
            discount == 0m ? null : discount));
    }

    /// <summary>
    /// Creates the provider customer if this account has none, then opens a checkout
    /// session for the plan.
    ///
    /// The promo code is deliberately not redeemed here. Redeeming before payment would
    /// consume a single-use code for a checkout that may be abandoned; Stripe applies
    /// its own promotion codes, and reconciling ours is left as a follow-up rather than
    /// silently burning someone's discount.
    /// </summary>
    private async Task<BillingCheckoutSession> StartCheckoutAsync(
        User user,
        SubscriptionPlan plan,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>
        {
            [SubscriptionMetadataKeys.UserId] = user.Id.ToString(),
            [SubscriptionMetadataKeys.PlanCode] = plan.Code,
        };

        var customerId = await billing.EnsureCustomerAsync(
            user.ProviderCustomerId,
            user.Email,
            $"{user.FirstName} {user.LastName}".Trim(),
            // Keyed on the account, so a retry cannot leave one person with two
            // customers and a split billing history.
            $"customer:{user.Id}",
            metadata,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(user.ProviderCustomerId))
        {
            user.LinkProviderCustomer(customerId);
        }

        return await billing.StartCheckoutAsync(
            new BillingCheckoutRequest(
                customerId,
                plan.ProviderPriceId!,
                returnUrls.CheckoutSuccessUrl(plan.Audience),
                returnUrls.CheckoutCancelUrl(plan.Audience),
                // Not keyed on the plan alone: someone who abandons checkout and comes
                // back must get a fresh session, so the key includes the day.
                $"checkout:{user.Id}:{plan.Code}:{clock.UtcNow:yyyyMMddHHmm}",
                metadata),
            cancellationToken);
    }

    /// <summary>
    /// Validates and redeems a promo code, returning the discount. Every reason a code
    /// can be refused lives on the aggregate; this adds the one-use-per-account rule,
    /// which needs storage to answer.
    /// </summary>
    private async Task<decimal> ApplyPromoAsync(
        string? code,
        SubscriptionPlan plan,
        Guid userId,
        PlanAudience audience,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return 0m;
        }

        var promo = await growth.FindPromoByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException("That promo code was not recognized.");

        if (promo.RejectionReason(utcNow, audience) is { } reason)
        {
            throw new ConflictException(reason);
        }

        if (await growth.HasRedeemedAsync(promo.Id, userId, cancellationToken))
        {
            throw new ConflictException("You have already used that promo code.");
        }

        var discount = promo.DiscountOn(plan.MonthlyPrice);

        promo.Redeem(utcNow, audience);
        growth.Add(PromoRedemption.Create(promo.Id, userId, discount, utcNow));

        return discount;
    }
}

public sealed class CancelSubscriptionHandler(
    ISubscriptionRepository subscriptions,
    ISubscriptionBillingProvider billing,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<CancelSubscriptionHandler> logger)
    : ICommandHandler<CancelSubscriptionCommand, MySubscriptionResponse>
{
    public async Task<MySubscriptionResponse> HandleAsync(
        CancelSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var subscription = await subscriptions.FindActiveForUserAsync(userId, cancellationToken)
            ?? throw new NotFoundException("There is no subscription to cancel.");

        var utcNow = clock.UtcNow;

        /*
         * Stop the renewal at the provider first.
         *
         * Order matters. If this throws, nothing local has changed and the person can
         * try again — annoying but honest. Cancelling locally first and then failing
         * here would show them a cancelled subscription while the provider carried on
         * billing their card, which is the one outcome worth engineering against.
         */
        if (billing.IsLive && !string.IsNullOrWhiteSpace(subscription.ProviderSubscriptionId))
        {
            await billing.CancelAtPeriodEndAsync(
                subscription.ProviderSubscriptionId, cancellationToken);

            logger.LogInformation(
                "Renewal stopped at the provider for subscription {SubscriptionId}",
                subscription.Id);
        }

        // Stops the renewal but keeps what was paid for until the period ends.
        subscription.CancelAtEndOfPeriod(utcNow);

        audit.Record(nameof(UserSubscription), subscription.Id, "SubscriptionCancelled", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new MySubscriptionResponse(
            subscription.Plan.Code,
            subscription.Plan.Name,
            subscription.Status.ToString(),
            subscription.Plan.Features,
            subscription.CurrentPeriodEndUtc,
            subscription.CancelAtPeriodEnd,
            AmountCharged: null,
            DiscountApplied: null);
    }
}
