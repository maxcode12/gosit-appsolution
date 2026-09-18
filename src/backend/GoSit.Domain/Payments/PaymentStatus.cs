namespace GoSit.Domain.Payments;

/// <summary>
/// Payment lifecycle. GoSit never sets Succeeded from a client response — only from a
/// verified provider webhook or a server-side confirmation against the provider API.
/// </summary>
public enum PaymentStatus
{
    /// <summary>Intent created; the requester has not completed payment yet.</summary>
    Pending = 1,

    /// <summary>The provider is processing; the outcome is not yet known.</summary>
    Processing = 2,

    Succeeded = 3,
    Failed = 4,

    /// <summary>Abandoned before completion, for example when the booking was cancelled.</summary>
    Cancelled = 5,

    PartiallyRefunded = 6,
    Refunded = 7,
}
