using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRequesters;
using GoSit.Domain.CareRecipients;

namespace GoSit.Application.Features.CareRecipients.AddCareRecipient;

public sealed record AddCareRecipientCommand(
    string FirstName,
    string? LastName,
    DateOnly DateOfBirth,
    CareRecipientType RecipientType) : ICommand<CareRecipientResponse>;

public sealed class AddCareRecipientValidator : AbstractValidator<AddCareRecipientCommand>
{
    public AddCareRecipientValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
        RuleFor(x => x.RecipientType).IsInEnum();
        RuleFor(x => x.DateOfBirth).NotEqual(default(DateOnly));
    }
}

public sealed class AddCareRecipientHandler(
    ICareRequesterProfileRepository profiles,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<AddCareRecipientCommand, CareRecipientResponse>
{
    public async Task<CareRecipientResponse> HandleAsync(
        AddCareRecipientCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(
                "Create your care-requester profile before adding a care recipient.");

        var utcNow = clock.UtcNow;

        var recipient = profile.AddCareRecipient(
            command.FirstName,
            command.LastName,
            command.DateOfBirth,
            command.RecipientType,
            utcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return recipient.ToResponse(DateOnly.FromDateTime(utcNow));
    }
}
