using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRequesters;
using GoSit.Domain.CareRecipients;

namespace GoSit.Application.Features.CareRecipients.UpdateCareNeeds;

/// <summary>
/// Replaces the care needs recorded for one recipient.
///
/// The payload is highly sensitive. Nothing in this slice may be logged, and the
/// command itself must never be written to a log message.
/// </summary>
public sealed record UpdateCareNeedsCommand(
    Guid CareRecipientId,
    string? MobilityNeeds,
    string? CommunicationStyle,
    string? SensoryNeeds,
    string? BehaviourSupportNotes,
    string? DietaryRestrictions,
    string? Allergies,
    bool ToiletingAssistance,
    bool FeedingAssistance,
    string? RoutinePreferences,
    string? Triggers,
    string? CalmingStrategies,
    string? EmergencyInstructions,
    IReadOnlyList<string> RequiredSkills) : ICommand<CareRecipientResponse>;

public sealed class UpdateCareNeedsValidator : AbstractValidator<UpdateCareNeedsCommand>
{
    private const int MaxNoteLength = 2000;

    public UpdateCareNeedsValidator()
    {
        RuleFor(x => x.CareRecipientId).NotEmpty();

        RuleFor(x => x.MobilityNeeds).MaximumLength(MaxNoteLength);
        RuleFor(x => x.CommunicationStyle).MaximumLength(MaxNoteLength);
        RuleFor(x => x.SensoryNeeds).MaximumLength(MaxNoteLength);
        RuleFor(x => x.BehaviourSupportNotes).MaximumLength(MaxNoteLength);
        RuleFor(x => x.DietaryRestrictions).MaximumLength(MaxNoteLength);
        RuleFor(x => x.Allergies).MaximumLength(MaxNoteLength);
        RuleFor(x => x.RoutinePreferences).MaximumLength(MaxNoteLength);
        RuleFor(x => x.Triggers).MaximumLength(MaxNoteLength);
        RuleFor(x => x.CalmingStrategies).MaximumLength(MaxNoteLength);
        RuleFor(x => x.EmergencyInstructions).MaximumLength(MaxNoteLength);

        RuleFor(x => x.RequiredSkills).NotNull();
        RuleForEach(x => x.RequiredSkills).NotEmpty().MaximumLength(50);
    }
}

public sealed class UpdateCareNeedsHandler(
    ICareRequesterProfileRepository profiles,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<UpdateCareNeedsCommand, CareRecipientResponse>
{
    public async Task<CareRecipientResponse> HandleAsync(
        UpdateCareNeedsCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("That care recipient does not exist.");

        var utcNow = clock.UtcNow;

        var details = new CareNeedsDetails
        {
            MobilityNeeds = command.MobilityNeeds,
            CommunicationStyle = command.CommunicationStyle,
            SensoryNeeds = command.SensoryNeeds,
            BehaviourSupportNotes = command.BehaviourSupportNotes,
            DietaryRestrictions = command.DietaryRestrictions,
            Allergies = command.Allergies,
            ToiletingAssistance = command.ToiletingAssistance,
            FeedingAssistance = command.FeedingAssistance,
            RoutinePreferences = command.RoutinePreferences,
            Triggers = command.Triggers,
            CalmingStrategies = command.CalmingStrategies,
            EmergencyInstructions = command.EmergencyInstructions,
            RequiredSkills = command.RequiredSkills,
        };

        // Scoped to the caller's own profile; an id owned by someone else is not found.
        // The aggregate raises DomainException, which the API maps to 400 — so re-check
        // here to return 404 and keep the id non-discoverable.
        if (profile.FindCareRecipient(command.CareRecipientId) is null)
        {
            throw new NotFoundException("That care recipient does not exist.");
        }

        profile.UpdateCareNeeds(command.CareRecipientId, details, utcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var recipient = profile.FindCareRecipient(command.CareRecipientId)!;
        return recipient.ToResponse(DateOnly.FromDateTime(utcNow));
    }
}
