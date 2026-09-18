using GoSit.Application.Abstractions;
using GoSit.Domain.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace GoSit.Infrastructure.Payments;

/// <summary>
/// Stripe implementation of <see cref="IPaymentProvider"/>.
///
/// Notes that matter:
/// - amounts are converted to minor units exactly once, through <see cref="Money"/>;
/// - every mutating call carries an idempotency key, so a retry cannot charge twice;
/// - nothing here logs a key, a client secret, or any card detail.
/// </summary>
public sealed class StripePaymentProvider(
    IOptions<StripeOptions> options,
    ILogger<StripePaymentProvider> logger) : IPaymentProvider
{
    private readonly StripeClient _client = new(options.Value.SecretKey);

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        var service = new PaymentIntentService(_client);

        var options = new PaymentIntentCreateOptions
        {
            Amount = Money.ToMinorUnits(amount),
            Currency = currency.ToLowerInvariant(),
            Metadata = metadata.ToDictionary(kv => kv.Key, kv => kv.Value),
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
            },
        };

        var intent = await service.CreateAsync(
            options,
            // A repeat of the same request returns the original intent rather than
            // creating a second one.
            new RequestOptions { IdempotencyKey = idempotencyKey },
            cancellationToken);

        // The id is safe to log; the client secret is not.
        logger.LogInformation("Created Stripe payment intent {PaymentIntentId}", intent.Id);

        return new PaymentIntentResult(intent.Id, intent.ClientSecret);
    }

    public async Task<ProviderRefundResult> RefundAsync(
        string providerPaymentIntentId,
        decimal amount,
        string? reason,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var service = new RefundService(_client);

        var refund = await service.CreateAsync(
            new RefundCreateOptions
            {
                PaymentIntent = providerPaymentIntentId,
                Amount = Money.ToMinorUnits(amount),
                Metadata = reason is null
                    ? null
                    : new Dictionary<string, string> { ["reason"] = reason },
            },
            new RequestOptions { IdempotencyKey = idempotencyKey },
            cancellationToken);

        logger.LogInformation(
            "Created Stripe refund {RefundId} for intent {PaymentIntentId}",
            refund.Id, providerPaymentIntentId);

        return new ProviderRefundResult(refund.Id, Money.FromMinorUnits(refund.Amount));
    }

    public async Task<ProviderTransferResult> CreateTransferAsync(
        string destinationAccountId,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var service = new TransferService(_client);

        var transfer = await service.CreateAsync(
            new TransferCreateOptions
            {
                Amount = Money.ToMinorUnits(amount),
                Currency = currency.ToLowerInvariant(),
                Destination = destinationAccountId,
            },
            new RequestOptions { IdempotencyKey = idempotencyKey },
            cancellationToken);

        logger.LogInformation("Created Stripe transfer {TransferId}", transfer.Id);

        return new ProviderTransferResult(transfer.Id);
    }
}
