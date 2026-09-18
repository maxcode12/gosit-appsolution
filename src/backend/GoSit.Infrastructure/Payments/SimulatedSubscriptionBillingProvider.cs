using System.Security.Cryptography;
using System.Text;
using GoSit.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace GoSit.Infrastructure.Payments;

/// <summary>
/// Stand-in used when no Stripe secret key is configured — local development and the
/// integration tests.
///
/// <see cref="IsLive"/> is false, and that is the important part: the subscribe handler
/// branches on it and starts the plan directly instead of sending anyone to a checkout
/// page that does not exist. The UI reads the same flag and says plainly that no payment
/// is being taken, rather than implying a charge that never happened.
///
/// It fabricates references so the flow is exercisable, and refuses to produce a
/// checkout URL — there is nothing to check out with.
/// </summary>
public sealed class SimulatedSubscriptionBillingProvider(
    ILogger<SimulatedSubscriptionBillingProvider> logger) : ISubscriptionBillingProvider
{
    public bool IsLive => false;

    public Task<string> EnsureCustomerAsync(
        string? existingCustomerId,
        string email,
        string fullName,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken) =>
        Task.FromResult(existingCustomerId ?? $"cus_sim_{Stable(idempotencyKey)}");

    public Task<BillingCheckoutSession> StartCheckoutAsync(
        BillingCheckoutRequest request,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "Checkout was requested with no billing provider configured. Callers must "
            + "check ISubscriptionBillingProvider.IsLive first.");

    public Task CancelAtPeriodEndAsync(
        string providerSubscriptionId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Simulated cancellation; Stripe is not configured");

        return Task.CompletedTask;
    }

    public Task<string> CreateBillingPortalUrlAsync(
        string providerCustomerId,
        string returnUrl,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "A billing portal was requested with no billing provider configured.");

    private static string Stable(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16].ToLowerInvariant();
}
