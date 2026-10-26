using GoSit.Application.Abstractions;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.TrustAndSafety;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.TrustAndSafety;

/// <summary>
/// The screening provider has news about a check.
/// </summary>
/// <param name="ProviderReference">The provider's reference, which is all a message needs to carry.</param>
public sealed record ApplyProviderUpdateCommand(string ProviderReference) : ICommand<Unit>;

/// <summary>
/// Records what the provider reports on the case, for staff to see.
///
/// It does not take the provider's word for what has changed: the message only says which
/// check to look at, and the current state is asked for directly. That makes a repeated
/// message harmless, and a message that arrives out of order cannot move a case backwards,
/// because every one of them ends in the same up-to-date answer.
///
/// And it never approves anyone. A clear result is evidence; a staff member still decides.
/// </summary>
public sealed class ApplyProviderUpdateHandler(
    IVerificationRepository verifications,
    IBackgroundCheckProvider provider,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    IDateTimeProvider clock,
    ILogger<ApplyProviderUpdateHandler> logger) : ICommandHandler<ApplyProviderUpdateCommand, Unit>
{
    public async Task<Unit> HandleAsync(ApplyProviderUpdateCommand command, CancellationToken cancellationToken)
    {
        var verificationCase = await verifications.FindByProviderReferenceAsync(
            command.ProviderReference, cancellationToken);

        if (verificationCase is null)
        {
            // Not an error. A provider account can hold cases that are not ours, and telling
            // the sender which references we recognise would be telling them more than they
            // need to know.
            logger.LogInformation("A provider update named a case this system does not hold.");
            return Unit.Value;
        }

        var outcome = await provider.GetOutcomeAsync(command.ProviderReference, cancellationToken);

        if (outcome is null)
        {
            return Unit.Value;
        }

        if (!verificationCase.RecordProviderUpdate(outcome.Status, outcome.Score, clock.UtcNow))
        {
            logger.LogInformation(
                "Provider update for case {CaseId} ignored: it has already been decided",
                verificationCase.Id);
            return Unit.Value;
        }

        // Identifiers and the provider's status words only. Never the person.
        audit.Record(nameof(VerificationCase), verificationCase.Id, "ProviderUpdateRecorded", null);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Case {CaseId} updated by the provider: {Status} {Score}",
            verificationCase.Id, outcome.Status, outcome.Score ?? "(no score yet)");

        return Unit.Value;
    }
}
