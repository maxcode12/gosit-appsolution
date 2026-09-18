using GoSit.Application.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;
using V2 = Stripe.V2.Core;

namespace GoSit.Infrastructure.Payments;

/// <summary>
/// Caregiver payout accounts through Stripe Connect.
///
/// **Express-equivalent accounts, with Stripe-hosted onboarding**, built on Stripe's v2
/// Accounts API — v1 account creation is deprecated for new Stripe accounts (see
/// docs/DEVELOPMENT.md's payouts section for the incident that surfaced this: a fresh
/// sandbox refused every v1 <c>POST /accounts</c> call outright). The alternative to
/// hosted onboarding — Custom accounts — means GoSit collecting bank details, government
/// ID and dates of birth from every caregiver, and carrying the compliance obligations
/// that come with them. The <c>recipient</c> configuration hands all of that to Stripe:
/// we get an account reference and a yes/no on whether money can be sent.
///
/// Note what this class never touches: a bank account number, a SIN, or a date of birth.
///
/// V1 and v2 share the same "Stripe" and "AccountXxx" type names, so v2 types are
/// referenced through the <c>V2</c> alias throughout rather than a plain
/// <c>using Stripe.V2.Core;</c> — the compiler cannot otherwise tell which
/// <c>AccountCreateOptions</c> (say) a bare reference means.
/// </summary>
public sealed class StripePayoutAccountProvider : IPayoutAccountProvider
{
    private readonly V2.AccountService _accounts;
    private readonly V2.AccountLinkService _accountLinks;
    private readonly AccountLoginLinkService _loginLinks;

    public StripePayoutAccountProvider(IOptions<StripeOptions> options)
    {
        var apiBase = options.Value.ApiBase;

        var client = string.IsNullOrWhiteSpace(apiBase)
            ? new StripeClient(options.Value.SecretKey)
            // Test-only. See StripeOptions.ApiBase.
            : new StripeClient(options.Value.SecretKey, apiBase: apiBase);

        _accounts = client.V2.Core.Accounts;
        _accountLinks = client.V2.Core.AccountLinks;
        // No v2 equivalent ships in this Stripe.NET version yet. Unused by any endpoint
        // today (see CreateAccountDashboardUrlAsync) — left on v1 rather than guessed at.
        _loginLinks = new AccountLoginLinkService(client);
    }

    public bool IsLive => true;

    public async Task<string> CreateAccountAsync(
        string email,
        string country,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        var account = await _accounts.CreateAsync(
            new V2.AccountCreateOptions
            {
                ContactEmail = email,
                // "express": the caregiver never sees a Stripe dashboard, matching the
                // v1 Express account this replaces.
                Dashboard = "express",
                // v2 makes explicit what v1's account type implied: who is responsible
                // for the account's own fees and for a negative balance it can't repay.
                // A recipient-only account (transfers, no card payments) accepts both
                // itself — Stripe rejects "stripe"/"application_express" here with "can
                // only be application for the set of configurations this account has".
                Defaults = new V2.AccountCreateDefaultsOptions
                {
                    Responsibilities = new V2.AccountCreateDefaultsResponsibilitiesOptions
                    {
                        FeesCollector = "application",
                        LossesCollector = "application",
                    },
                },
                Identity = new V2.AccountCreateIdentityOptions
                {
                    Country = country,
                    EntityType = "individual",
                },
                Configuration = new V2.AccountCreateConfigurationOptions
                {
                    Recipient = new V2.AccountCreateConfigurationRecipientOptions
                    {
                        Capabilities = new V2.AccountCreateConfigurationRecipientCapabilitiesOptions
                        {
                            // Transfers is the only capability GoSit needs: money is
                            // charged on the platform account and moved to the
                            // caregiver. Asking for card payments as well would drag
                            // every caregiver through merchant onboarding they have no
                            // use for.
                            StripeBalance = new V2.AccountCreateConfigurationRecipientCapabilitiesStripeBalanceOptions
                            {
                                StripeTransfers =
                                    new V2.AccountCreateConfigurationRecipientCapabilitiesStripeBalanceStripeTransfersOptions
                                    {
                                        Requested = true,
                                    },
                            },
                        },
                    },
                },
                Metadata = new Dictionary<string, string>(metadata),
            },
            new RequestOptions { IdempotencyKey = idempotencyKey },
            cancellationToken);

        return account.Id;
    }

    public async Task<string> CreateOnboardingLinkAsync(
        string accountId,
        string refreshUrl,
        string returnUrl,
        CancellationToken cancellationToken)
    {
        var link = await _accountLinks.CreateAsync(
            new V2.AccountLinkCreateOptions
            {
                Account = accountId,
                UseCase = new V2.AccountLinkCreateUseCaseOptions
                {
                    Type = "account_onboarding",
                    AccountOnboarding = new V2.AccountLinkCreateUseCaseAccountOnboardingOptions
                    {
                        Configurations = ["recipient"],
                        RefreshUrl = refreshUrl,
                        ReturnUrl = returnUrl,
                    },
                },
            },
            cancellationToken: cancellationToken);

        return link.Url;
    }

    public async Task<PayoutAccountStatus> GetAccountStatusAsync(
        string accountId,
        CancellationToken cancellationToken)
    {
        var account = await _accounts.GetAsync(
            accountId,
            new V2.AccountGetOptions { Include = ["configuration.recipient", "requirements"] },
            cancellationToken: cancellationToken);

        return Translate(account);
    }

    public async Task<string> CreateAccountDashboardUrlAsync(
        string accountId,
        CancellationToken cancellationToken)
    {
        // Unverified against a v2-created account — nothing calls this endpoint today
        // (see the IPayoutAccountProvider interface), so it has never been exercised.
        // Stripe's v1/v2 account ids share one namespace, so this may well work
        // unchanged, but confirm against a real onboarded account before wiring it up.
        var link = await _loginLinks.CreateAsync(
            accountId, cancellationToken: cancellationToken);

        return link.Url;
    }

    /// <summary>
    /// Reduces a Stripe v2 account to the two facts GoSit acts on, plus a reason to show.
    ///
    /// <c>DetailsSubmitted</c> has no direct v2 field — v1 reported whether onboarding
    /// data had been submitted regardless of whether it had been reviewed yet. The
    /// closest v2 equivalent is "nothing is currently blocking the recipient
    /// configuration": no outstanding requirement entries. <c>PayoutsEnabled</c> reads
    /// the recipient capability's own status instead of a top-level flag, since v2 tracks
    /// each capability (transfers, payouts) separately rather than one Boolean for the
    /// whole account.
    /// </summary>
    internal static PayoutAccountStatus Translate(V2.Account account)
    {
        var payouts = account.Configuration?.Recipient?.Capabilities?.StripeBalance?.Payouts;

        return new PayoutAccountStatus(
            PayoutsEnabled: payouts?.Status == "active",
            DetailsSubmitted: account.Requirements is null || account.Requirements.Entries.Count == 0,
            DisabledReason: payouts?.StatusDetails?.FirstOrDefault()?.Code
                ?? account.Requirements?.Entries.FirstOrDefault()?.Description);
    }
}
