using System.Security.Cryptography;
using GoSit.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace GoSit.Infrastructure.Payments;

/// <summary>
/// Stand-in used when no Stripe secret key is configured — local development and the
/// integration tests.
///
/// It fabricates provider references so the booking and payment flows can be exercised
/// end to end without credentials. It deliberately does NOT simulate a successful
/// payment: nothing here can mark a payment succeeded, because that only ever happens
/// through verified webhook handling. Tests drive success by posting a signed webhook,
/// which exercises the real verification path.
/// </summary>
public sealed class SimulatedPaymentProvider(
    ILogger<SimulatedPaymentProvider> logger) : IPaymentProvider
{
    public Task<PaymentIntentResult> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        // Derived from the idempotency key, so a repeated request for the same booking
        // returns the same intent — matching how a real provider behaves.
        var id = $"pi_sim_{Stable(idempotencyKey)}";

        logger.LogInformation(
            "Simulated payment intent {PaymentIntentId} created; Stripe is not configured", id);

        return Task.FromResult(new PaymentIntentResult(id, $"{id}_secret_simulated"));
    }

    public Task<ProviderRefundResult> RefundAsync(
        string providerPaymentIntentId,
        decimal amount,
        string? reason,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        Task.FromResult(new ProviderRefundResult($"re_sim_{Stable(idempotencyKey)}", amount));

    public Task<ProviderTransferResult> CreateTransferAsync(
        string destinationAccountId,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        Task.FromResult(new ProviderTransferResult($"tr_sim_{Stable(idempotencyKey)}"));

    /// <summary>A short, deterministic id derived from the idempotency key.</summary>
    private static string Stable(string idempotencyKey) =>
        Convert.ToHexString(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(idempotencyKey)))[..16]
            .ToLowerInvariant();
}
