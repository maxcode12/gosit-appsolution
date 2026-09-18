using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Growth;
using GoSit.Domain.Subscriptions;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Administration.PromoCodes;

/// <summary>
/// Staff management of promo codes.
///
/// A promo code is a way of giving money away, so the surface is deliberately narrow:
/// create one, look at what they are costing, and switch one off. There is no edit.
///
/// That is the load-bearing decision here. Once a code has been handed out — printed on
/// a flyer, sent in an email, read out on a call — the offer it carries is a deal
/// already made with everyone holding it. Editing the discount, the audience or the
/// expiry afterwards silently changes that deal for people who cannot see the change,
/// and the ones who lose are the ones who were slowest to redeem. Switching a code off
/// and issuing a new one is the same amount of work and leaves both offers legible in
/// the record.
///
/// Deactivation is likewise not deletion: redemptions point at the code, and a campaign
/// that cost real money should not be removable from the accounting by a click.
/// </summary>
public sealed record ListPromoCodesQuery : IQuery<IReadOnlyList<AdminPromoCode>>;

public sealed record CreatePromoCodeCommand(
    string Code,
    string Description,
    DiscountType DiscountType,
    decimal Value,
    DateTime? ValidFromUtc,
    DateTime? ValidUntilUtc,
    int? MaxRedemptions,
    PlanAudience? Audience) : ICommand<AdminPromoCode>;

public sealed record DeactivatePromoCodeCommand(Guid PromoCodeId) : ICommand<AdminPromoCode>;

/// <summary>
/// One code as staff need to see it.
///
/// Carries <see cref="RedemptionCount"/> against <see cref="MaxRedemptions"/> because
/// the question someone actually arrives with is "how much of this campaign is left",
/// and <see cref="Status"/> because a code can be inactive for four different reasons
/// and a bare <c>IsActive</c> flag would show "active" on a code that expired last week.
/// </summary>
public sealed record AdminPromoCode(
    Guid Id,
    string Code,
    string Description,
    string DiscountType,
    decimal Value,
    string? Audience,
    DateTime ValidFromUtc,
    DateTime? ValidUntilUtc,
    int? MaxRedemptions,
    int RedemptionCount,
    bool IsActive,
    string Status);

public sealed class CreatePromoCodeValidator : AbstractValidator<CreatePromoCodeCommand>
{
    public CreatePromoCodeValidator()
    {
        // Matches the column and the referral-code normaliser this shares.
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DiscountType).IsInEnum();
        RuleFor(x => x.Value).GreaterThan(0);

        RuleFor(x => x.Value)
            .LessThanOrEqualTo(100)
            .When(x => x.DiscountType == DiscountType.Percentage)
            .WithMessage("A percentage discount cannot exceed 100.");

        RuleFor(x => x.MaxRedemptions)
            .GreaterThan(0)
            .When(x => x.MaxRedemptions.HasValue);

        RuleFor(x => x.ValidUntilUtc)
            .GreaterThan(x => x.ValidFromUtc!.Value)
            .When(x => x.ValidFromUtc.HasValue && x.ValidUntilUtc.HasValue)
            .WithMessage("A promo code must expire after it becomes valid.");

        RuleFor(x => x.Audience).IsInEnum().When(x => x.Audience.HasValue);
    }
}

public sealed class ListPromoCodesHandler(IGrowthRepository growth, IDateTimeProvider clock)
    : IQueryHandler<ListPromoCodesQuery, IReadOnlyList<AdminPromoCode>>
{
    public async Task<IReadOnlyList<AdminPromoCode>> HandleAsync(
        ListPromoCodesQuery query,
        CancellationToken cancellationToken)
    {
        var codes = await growth.ListPromosAsync(cancellationToken);
        var utcNow = clock.UtcNow;

        return codes.Select(c => Project(c, utcNow)).ToList();
    }

    /// <summary>
    /// Shared by all three handlers so a code reads the same however it was reached.
    /// </summary>
    internal static AdminPromoCode Project(PromoCode code, DateTime utcNow) =>
        new(
            code.Id,
            code.Code,
            code.Description,
            code.DiscountType.ToString(),
            code.Value,
            code.Audience?.ToString(),
            code.ValidFromUtc,
            code.ValidUntilUtc,
            code.MaxRedemptions,
            code.RedemptionCount,
            code.IsActive,
            StatusOf(code, utcNow));

    /// <summary>
    /// Why a code is or is not usable right now, in one word.
    ///
    /// The aggregate already knows every reason a code can be refused, but it answers
    /// for a specific audience, and staff are looking at codes aimed at both sides at
    /// once. Asking it with an audience would report a caregiver-only code as unusable
    /// on a screen that is not buying anything.
    /// </summary>
    private static string StatusOf(PromoCode code, DateTime utcNow)
    {
        if (!code.IsActive)
        {
            return "Deactivated";
        }

        if (utcNow < code.ValidFromUtc)
        {
            return "Scheduled";
        }

        if (code.ValidUntilUtc is { } until && utcNow > until)
        {
            return "Expired";
        }

        if (code.MaxRedemptions is { } cap && code.RedemptionCount >= cap)
        {
            return "FullyRedeemed";
        }

        return "Live";
    }
}

public sealed class CreatePromoCodeHandler(
    IGrowthRepository growth,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<CreatePromoCodeHandler> logger)
    : ICommandHandler<CreatePromoCodeCommand, AdminPromoCode>
{
    public async Task<AdminPromoCode> HandleAsync(
        CreatePromoCodeCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();
        var utcNow = clock.UtcNow;

        /*
         * Checked here as well as by the unique index.
         *
         * The index is what guarantees it, but a race is not the case worth designing
         * for — someone reissuing last year's campaign is. Without this they get a
         * database error, and the reason they cannot use the code they typed is that
         * the *old* one is still on file, possibly still live.
         */
        var normalized = ReferralCode.Normalize(command.Code);

        if (await growth.FindPromoByCodeAsync(normalized, cancellationToken) is not null)
        {
            throw new ConflictException(
                $"The code {normalized} already exists. Deactivate it before reusing the code, "
                + "or choose another.");
        }

        // Defaults to now rather than refusing: "start it today" is the common case, and
        // making someone type today's date to express it is friction for nothing.
        var validFrom = command.ValidFromUtc ?? utcNow;

        // Every remaining rule lives on the aggregate, which throws DomainException and
        // is translated to a 400 — the validator above only spares a round trip.
        var promo = PromoCode.Create(
            normalized,
            command.Description,
            command.DiscountType,
            command.Value,
            validFrom,
            command.ValidUntilUtc,
            command.MaxRedemptions,
            command.Audience);

        growth.Add(promo);

        audit.Record(nameof(PromoCode), promo.Id, "PromoCodeCreated", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // The code itself, not the discount: a log line is not the place to record what
        // a campaign is worth, and the id is enough to find the rest.
        logger.LogInformation(
            "Promo code {PromoCodeId} created by {UserId}", promo.Id, userId);

        return ListPromoCodesHandler.Project(promo, utcNow);
    }
}

public sealed class DeactivatePromoCodeHandler(
    IGrowthRepository growth,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<DeactivatePromoCodeHandler> logger)
    : ICommandHandler<DeactivatePromoCodeCommand, AdminPromoCode>
{
    public async Task<AdminPromoCode> HandleAsync(
        DeactivatePromoCodeCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var promo = await growth.FindPromoByIdAsync(command.PromoCodeId, cancellationToken)
            ?? throw new NotFoundException("That promo code does not exist.");

        // Deactivating twice is not an error — the desired end state already holds, and
        // two people switching off the same campaign should not see a failure.
        if (promo.IsActive)
        {
            promo.Deactivate();

            audit.Record(nameof(PromoCode), promo.Id, "PromoCodeDeactivated", userId);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Promo code {PromoCodeId} deactivated by {UserId}", promo.Id, userId);
        }

        return ListPromoCodesHandler.Project(promo, clock.UtcNow);
    }
}
