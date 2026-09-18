using GoSit.Application.Abstractions;
using GoSit.Application.Common;

namespace GoSit.Application.Features.CareRecipients.RemoveCareRecipient;

public sealed record RemoveCareRecipientCommand(Guid CareRecipientId) : ICommand<Unit>;

/// <summary>Empty response for commands that return nothing.</summary>
public sealed record Unit
{
    public static readonly Unit Value = new();
}

public sealed class RemoveCareRecipientHandler(
    ICareRequesterProfileRepository profiles,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<RemoveCareRecipientCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        RemoveCareRecipientCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("That care recipient does not exist.");

        if (profile.FindCareRecipient(command.CareRecipientId) is null)
        {
            throw new NotFoundException("That care recipient does not exist.");
        }

        // Removing the recipient cascades to their care needs, which is the intent:
        // deleting a recipient must not leave their sensitive notes behind.
        profile.RemoveCareRecipient(command.CareRecipientId, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
