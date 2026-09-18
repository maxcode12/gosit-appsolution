using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.Caregivers;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Payouts;

/// <summary>
/// Getting a caregiver set up to be paid.
///
/// GoSit never collects bank details. Onboarding happens on the provider's own hosted
/// pages; this feature creates the connected account, hands back a link, and records
/// what the provider says afterwards.
/// </summary>
public sealed record StartPayoutOnboardingCommand : ICommand<PayoutOnboardingResponse>;

public sealed record PayoutOnboardingResponse(string OnboardingUrl);

public sealed record GetPayoutAccountQuery : IQuery<PayoutAccountResponse>;

/// <summary>
/// Where a caregiver stands with payouts.
///
/// The three states are deliberately distinguishable. "Not started", "submitted and
/// under review", and "ready" call for different things to be said to someone, and
/// collapsing them into a single boolean would leave a caregiver who has done everything
/// wondering why they are still being told to set up payouts.
/// </summary>
public sealed record PayoutAccountResponse(
    bool BillingConfigured,
    bool HasAccount,
    bool DetailsSubmitted,
    bool PayoutsEnabled,
    string? DisabledReason,
    /// <summary>Owed and waiting on this account being ready.</summary>
    decimal PendingBalance,
    string Currency);

public sealed class StartPayoutOnboardingHandler(
    ICaregiverProfileRepository profiles,
    IUserRepository users,
    IPayoutAccountProvider payoutAccounts,
    IPayoutReturnUrls returnUrls,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<StartPayoutOnboardingHandler> logger)
    : ICommandHandler<StartPayoutOnboardingCommand, PayoutOnboardingResponse>
{
    public async Task<PayoutOnboardingResponse> HandleAsync(
        StartPayoutOnboardingCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        if (!payoutAccounts.IsLive)
        {
            throw new ConflictException("Payouts are not configured.");
        }

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("You do not have a caregiver profile yet.");

        var user = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("The account no longer exists.");

        var utcNow = clock.UtcNow;

        if (string.IsNullOrWhiteSpace(profile.PayoutAccountId))
        {
            var accountId = await payoutAccounts.CreateAccountAsync(
                user.Email,
                // Ontario-first, and Connect requires a country at creation. When GoSit
                // opens elsewhere this comes from the caregiver's own address.
                "CA",
                // Keyed on the profile so a double-submit within the same window cannot
                // leave one caregiver with two accounts and their earnings split across
                // both. Bucketed by a few minutes, not held forever: a key good for all
                // time means a single transient failure — Stripe down for a second, a
                // deploy mid-request — gets its error response cached and replayed for
                // the next 24 hours, and every retry in that window fails identically
                // with nothing the caregiver or an operator can do about it until the key
                // ages out. This happened once already, from an unrelated account-setup
                // issue, and needed a database edit to unstick. A short bucket keeps the
                // double-submit protection this exists for while bounding how long a
                // genuine failure can stay stuck to minutes instead of a day.
                $"payout-account:{profile.Id}:{IdempotencyBucket(utcNow)}",
                new Dictionary<string, string>
                {
                    [PayoutMetadataKeys.CaregiverProfileId] = profile.Id.ToString(),
                    [PayoutMetadataKeys.UserId] = userId.ToString(),
                },
                cancellationToken);

            profile.LinkPayoutAccount(accountId, utcNow);

            audit.Record(
                nameof(CaregiverProfile), profile.Id, "PayoutAccountCreated", userId);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Created a payout account for caregiver {CaregiverProfileId}", profile.Id);
        }

        // Onboarding links are single-use and short-lived, so one is minted per request
        // and never stored. Someone whose link expires simply asks for another.
        var url = await payoutAccounts.CreateOnboardingLinkAsync(
            profile.PayoutAccountId!,
            returnUrls.OnboardingRefreshUrl(),
            returnUrls.OnboardingReturnUrl(),
            cancellationToken);

        return new PayoutOnboardingResponse(url);
    }

    /// <summary>Floors to a 5-minute window, so the idempotency key rotates on its own.</summary>
    private static long IdempotencyBucket(DateTime utcNow) =>
        new DateTimeOffset(utcNow).ToUnixTimeSeconds() / 300;
}

public sealed class GetPayoutAccountHandler(
    ICaregiverProfileRepository profiles,
    IPayoutRepository payouts,
    IPayoutAccountProvider payoutAccounts,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<GetPayoutAccountHandler> logger) : IQueryHandler<GetPayoutAccountQuery, PayoutAccountResponse>
{
    public async Task<PayoutAccountResponse> HandleAsync(
        GetPayoutAccountQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("You do not have a caregiver profile yet.");

        /*
         * Refresh from the provider when there is an account but payouts are not yet
         * enabled.
         *
         * Someone returning from onboarding lands on this screen within a second, and
         * the account.updated webhook may not have arrived. Telling them to "finish
         * setting up payouts" when they just did would be the wrong answer at the worst
         * moment, so their standing is read directly rather than waited for.
         */
        if (payoutAccounts.IsLive
            && !string.IsNullOrWhiteSpace(profile.PayoutAccountId)
            && !profile.PayoutsEnabled)
        {
            try
            {
                var status = await payoutAccounts.GetAccountStatusAsync(
                    profile.PayoutAccountId, cancellationToken);

                profile.UpdatePayoutCapability(
                    status.PayoutsEnabled,
                    status.DetailsSubmitted,
                    status.DisabledReason,
                    clock.UtcNow);

                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // A provider outage must not break the earnings screen. The webhook is
                // the durable path; this is only a convenience.
                logger.LogWarning(
                    ex, "Could not refresh the payout account for caregiver {CaregiverProfileId}",
                    profile.Id);
            }
        }

        var pending = await payouts.SumPendingForCaregiverAsync(profile.Id, cancellationToken);

        return new PayoutAccountResponse(
            payoutAccounts.IsLive,
            !string.IsNullOrWhiteSpace(profile.PayoutAccountId),
            profile.PayoutDetailsSubmitted,
            profile.PayoutsEnabled,
            profile.PayoutDisabledReason,
            pending,
            Domain.Payments.Money.CanadianDollar);
    }
}

/// <summary>
/// Applies an <c>account.updated</c> webhook.
///
/// Matched on the account id GoSit stored when it created the account. An event for an
/// account we do not know is ignored rather than treated as an error — a platform can
/// have connected accounts that predate or sit outside this feature.
/// </summary>
public sealed class PayoutAccountWebhookApplier(
    ICaregiverProfileRepository profiles,
    IDateTimeProvider clock,
    ILogger<PayoutAccountWebhookApplier> logger)
{
    public async Task ApplyAsync(
        PaymentWebhookEvent @event,
        CancellationToken cancellationToken)
    {
        if (@event.PayoutAccount is not { } details)
        {
            return;
        }

        var profile = await profiles.FindByPayoutAccountIdAsync(
            details.AccountId, cancellationToken);

        if (profile is null)
        {
            logger.LogInformation(
                "Ignoring an account update for an unknown connected account");

            return;
        }

        var wasEnabled = profile.PayoutsEnabled;

        profile.UpdatePayoutCapability(
            details.PayoutsEnabled,
            details.DetailsSubmitted,
            details.DisabledReason,
            clock.UtcNow);

        if (!wasEnabled && details.PayoutsEnabled)
        {
            // The sweep picks up everything already queued on its next cycle; there is
            // nothing to do here but note it.
            logger.LogInformation(
                "Payouts enabled for caregiver {CaregiverProfileId}; queued earnings will be sent",
                profile.Id);
        }
    }
}

public static class PayoutMetadataKeys
{
    public const string CaregiverProfileId = "gosit_caregiver_profile_id";
    public const string UserId = "gosit_user_id";
}
