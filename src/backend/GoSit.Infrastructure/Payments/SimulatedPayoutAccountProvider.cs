using System.Security.Cryptography;
using System.Text;
using GoSit.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace GoSit.Infrastructure.Payments;

/// <summary>
/// Stand-in used when no Stripe secret key is configured — local development and the
/// integration tests.
///
/// <see cref="IsLive"/> is false, and the caregiver screens read it: with no provider
/// there is no onboarding to offer, so they say payouts are not set up here rather than
/// linking to a page that does not exist.
///
/// It refuses to produce onboarding links for the same reason the simulated billing
/// provider refuses to produce checkout sessions — there is nothing to link to, and
/// returning a plausible-looking dead URL would be worse than failing.
/// </summary>
public sealed class SimulatedPayoutAccountProvider(
    ILogger<SimulatedPayoutAccountProvider> logger) : IPayoutAccountProvider
{
    public bool IsLive => false;

    public Task<string> CreateAccountAsync(
        string email,
        string country,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        var id = $"acct_sim_{Stable(idempotencyKey)}";

        logger.LogInformation(
            "Simulated payout account {AccountId}; Stripe is not configured", id);

        return Task.FromResult(id);
    }

    public Task<string> CreateOnboardingLinkAsync(
        string accountId,
        string refreshUrl,
        string returnUrl,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "Payout onboarding was requested with no provider configured. Callers must "
            + "check IPayoutAccountProvider.IsLive first.");

    /// <summary>
    /// Reports an account that has submitted nothing and cannot be paid.
    ///
    /// Deliberately not "ready": a simulated provider that claimed payouts were enabled
    /// would let the sweep try to send real-looking transfers, and the whole point of
    /// the simulated tier is that no money can move through it.
    /// </summary>
    public Task<PayoutAccountStatus> GetAccountStatusAsync(
        string accountId,
        CancellationToken cancellationToken) =>
        Task.FromResult(new PayoutAccountStatus(false, false, "provider_not_configured"));

    public Task<string> CreateAccountDashboardUrlAsync(
        string accountId,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "A payout dashboard was requested with no provider configured.");

    private static string Stable(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16].ToLowerInvariant();
}
