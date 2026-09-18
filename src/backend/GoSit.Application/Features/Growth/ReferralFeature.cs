using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Growth;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Growth;

/// <summary>The caller's own code, and how their invitations are doing.</summary>
public sealed record GetMyReferralsQuery : IQuery<ReferralSummaryResponse>;

/// <summary>Claims a code someone shared. Recorded once per account, ever.</summary>
public sealed record ApplyReferralCodeCommand(string Code) : ICommand<ReferralResponse>;

public sealed record ReferralSummaryResponse(
    string MyReferralCode,
    int PendingCount,
    int QualifiedCount,
    int RewardedCount,
    IReadOnlyList<ReferralResponse> Referrals);

/// <summary>
/// One referral. Carries no identifying detail about the other person — a referrer sees
/// that an invitation converted, not who the account belongs to.
/// </summary>
public sealed record ReferralResponse(
    Guid Id,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? QualifiedAtUtc,
    decimal? RewardAmount);

public sealed class ApplyReferralCodeValidator : AbstractValidator<ApplyReferralCodeCommand>
{
    public ApplyReferralCodeValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
    }
}

public sealed class GetMyReferralsHandler(
    IGrowthRepository growth,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IQueryHandler<GetMyReferralsQuery, ReferralSummaryResponse>
{
    public async Task<ReferralSummaryResponse> HandleAsync(
        GetMyReferralsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var user = await users.FindTrackedByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("The account no longer exists.");

        // Assigned on first view rather than at registration, so existing accounts pick
        // one up without a backfill. Idempotent, so a shared code never changes.
        var code = user.AssignReferralCode(ReferralCode.Generate);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var referrals = await growth.ListByReferrerAsync(userId, cancellationToken);

        return new ReferralSummaryResponse(
            code,
            referrals.Count(r => r.Status == ReferralStatus.Pending),
            referrals.Count(r => r.Status == ReferralStatus.Qualified),
            referrals.Count(r => r.Status == ReferralStatus.Rewarded),
            referrals
                .OrderByDescending(r => r.CreatedAtUtc)
                .Select(r => new ReferralResponse(
                    r.Id, r.Status.ToString(), r.CreatedAtUtc, r.QualifiedAtUtc, r.RewardAmount))
                .ToList());
    }
}

public sealed class ApplyReferralCodeHandler(
    IGrowthRepository growth,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<ApplyReferralCodeHandler> logger)
    : ICommandHandler<ApplyReferralCodeCommand, ReferralResponse>
{
    public async Task<ReferralResponse> HandleAsync(
        ApplyReferralCodeCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        // Someone can be referred once, ever. Checked here and enforced by a unique
        // index, so a race cannot create two.
        if (await growth.FindByRefereeAsync(userId, cancellationToken) is not null)
        {
            throw new ConflictException("A referral code has already been applied to this account.");
        }

        var normalized = ReferralCode.Normalize(command.Code);

        var referrer = await users.FindByReferralCodeAsync(normalized, cancellationToken)
            ?? throw new NotFoundException("That referral code was not recognized.");

        // The aggregate refuses self-referral; this reports it as the same "not
        // recognized" so someone cannot confirm a code is their own by trying it.
        if (referrer.Id == userId)
        {
            throw new NotFoundException("That referral code was not recognized.");
        }

        var referral = Referral.Record(referrer.Id, userId, normalized, clock.UtcNow);
        growth.Add(referral);

        audit.Record(nameof(Referral), referral.Id, "ReferralApplied", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Referral {ReferralId} recorded", referral.Id);

        return new ReferralResponse(
            referral.Id, referral.Status.ToString(), referral.CreatedAtUtc, null, null);
    }
}

/// <summary>
/// Qualifies a referral when its referee completes their first booking.
///
/// Qualification is deliberately tied to a completed booking rather than to signup:
/// rewarding signups would pay for throwaway accounts, and a referral programme that
/// pays for nothing is a fraud budget.
/// </summary>
public sealed class ReferralQualifier(
    IGrowthRepository growth,
    ILogger<ReferralQualifier> logger)
{
    public async Task QualifyForCompletedBookingAsync(
        Guid refereeUserId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var referral = await growth.FindByRefereeAsync(refereeUserId, cancellationToken);

        if (referral is null)
        {
            return;
        }

        if (referral.Qualify(utcNow))
        {
            logger.LogInformation("Referral {ReferralId} qualified", referral.Id);
        }
    }
}
