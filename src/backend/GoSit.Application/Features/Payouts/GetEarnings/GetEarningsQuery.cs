using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Payouts;

namespace GoSit.Application.Features.Payouts.GetEarnings;

/// <summary>
/// The caregiver's wallet: what they have earned, what is still to come, and the
/// statement lines behind both.
///
/// Scoped to the calling caregiver — there is no route that takes a caregiver id, so
/// one caregiver cannot see another's earnings.
/// </summary>
public sealed record GetEarningsQuery : IQuery<EarningsResponse>;

public sealed record EarningsResponse(
    string Currency,
    // Paid out and settled.
    decimal TotalPaid,
    // Earned and owed, but not yet transferred.
    decimal PendingBalance,
    decimal LifetimeGross,
    decimal LifetimePlatformFees,
    IReadOnlyList<StatementLine> Statement);

/// <summary>
/// One booking's contribution to earnings. Shows the gross and the fee as well as the
/// net, so a caregiver can see exactly what was deducted and why.
/// </summary>
public sealed record StatementLine(
    Guid PayoutId,
    Guid CareBookingId,
    decimal Gross,
    decimal PlatformFee,
    decimal Net,
    decimal CommissionRate,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,

    /// <summary>
    /// When this may be sent — the close of the family's dispute window.
    ///
    /// Reported because a caregiver is now waiting a fortnight for money they have
    /// already earned, and "pending" with no date is the kind of silence that turns a
    /// deliberate policy into a suspected bug.
    /// </summary>
    DateTime AvailableAtUtc,

    /// <summary>
    /// Held because the family raised a dispute.
    ///
    /// Distinct from simply waiting: one is a date, the other is a decision, and showing
    /// them the same way would tell a caregiver to expect money that is not coming until
    /// someone rules on it.
    /// </summary>
    bool IsHeldForDispute);

public sealed class GetEarningsHandler(
    IPayoutRepository payouts,
    ICaregiverProfileRepository caregiverProfiles,
    ICurrentUser currentUser) : IQueryHandler<GetEarningsQuery, EarningsResponse>
{
    public async Task<EarningsResponse> HandleAsync(
        GetEarningsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var caregiver = await caregiverProfiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("No caregiver profile has been created yet.");

        var all = await payouts.ListForCaregiverAsync(caregiver.Id, cancellationToken);

        // A failed transfer is still owed, so it counts as pending rather than lost.
        var pending = all
            .Where(p => p.Status is PayoutStatus.Queued or PayoutStatus.InTransit or PayoutStatus.Failed)
            .Sum(p => p.Amount);

        var paid = all.Where(p => p.Status == PayoutStatus.Paid).Sum(p => p.Amount);

        return new EarningsResponse(
            Domain.Payments.Money.CanadianDollar,
            paid,
            pending,
            all.Sum(p => p.GrossAmount),
            all.Sum(p => p.PlatformFee),
            all
                .OrderByDescending(p => p.CreatedAtUtc)
                .Select(p => new StatementLine(
                    p.Id,
                    p.CareBookingId,
                    p.GrossAmount,
                    p.PlatformFee,
                    p.Amount,
                    p.CommissionRate,
                    p.Status.ToString(),
                    p.CreatedAtUtc,
                    p.PaidAtUtc,
                    p.AvailableAtUtc,
                    p.IsHeldForDispute))
                .ToList());
    }
}
