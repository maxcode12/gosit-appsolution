namespace GoSit.Application.Abstractions;

/// <summary>
/// Connected accounts — how a caregiver is set up to be paid.
///
/// Separate from <see cref="IPaymentProvider"/>, which sends the money, because
/// onboarding is a different job with a different shape: it is a hosted flow the
/// provider owns, and its outcome arrives asynchronously on a webhook rather than as a
/// return value.
///
/// <b>GoSit never sees a caregiver's bank details.</b> They are given to the provider
/// through its own hosted onboarding, and all that comes back across this boundary is an
/// account reference and whether money can be sent to it. That is the whole point of
/// using hosted onboarding rather than collecting the details ourselves.
/// </summary>
public interface IPayoutAccountProvider
{
    /// <summary>
    /// Whether real payout accounts are configured. With no provider the caregiver
    /// screens say so rather than offering an onboarding flow that goes nowhere.
    /// </summary>
    bool IsLive { get; }

    /// <summary>
    /// Creates a connected account for a caregiver. Idempotent on
    /// <paramref name="idempotencyKey"/> so a retry cannot leave one person with two
    /// accounts and their earnings split across both.
    /// </summary>
    Task<string> CreateAccountAsync(
        string email,
        string country,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken);

    /// <summary>
    /// A single-use link into the provider's hosted onboarding.
    ///
    /// Short-lived by design, so it is generated on demand and never stored. The refresh
    /// URL is where the provider sends someone whose link expired before they finished.
    /// </summary>
    Task<string> CreateOnboardingLinkAsync(
        string accountId,
        string refreshUrl,
        string returnUrl,
        CancellationToken cancellationToken);

    /// <summary>
    /// What the provider currently says about the account.
    ///
    /// Read on demand as well as applied from webhooks: a caregiver returning from
    /// onboarding should not have to wait on a webhook to be told where they stand.
    /// </summary>
    Task<PayoutAccountStatus> GetAccountStatusAsync(
        string accountId,
        CancellationToken cancellationToken);

    /// <summary>
    /// A link into the provider's own dashboard for the connected account, where a
    /// caregiver can see their transfers and change their bank details.
    /// </summary>
    Task<string> CreateAccountDashboardUrlAsync(
        string accountId,
        CancellationToken cancellationToken);
}

/// <summary>
/// What the provider reports about a connected account.
///
/// <see cref="PayoutsEnabled"/> and <see cref="DetailsSubmitted"/> are deliberately
/// separate: an account can have submitted everything and still be held for review, and
/// "we are checking your details" is a different thing to tell someone than "you have
/// not finished".
/// </summary>
public sealed record PayoutAccountStatus(
    bool PayoutsEnabled,
    bool DetailsSubmitted,
    string? DisabledReason);
