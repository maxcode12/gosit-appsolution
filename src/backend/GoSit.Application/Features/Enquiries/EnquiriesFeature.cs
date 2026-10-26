using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.Enquiries;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Enquiries;

// ---- the public contact form ----

public sealed record SubmitEnquiryCommand(
    string Name,
    string Email,
    EnquiryKind Kind,
    string Message,
    bool ConsentGiven,
    string Source) : ICommand<Unit>;

public sealed class SubmitEnquiryValidator : AbstractValidator<SubmitEnquiryCommand>
{
    public SubmitEnquiryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Enquiry.MaxNameLength);

        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(Enquiry.MaxEmailLength)
            .EmailAddress();

        RuleFor(x => x.Kind).IsInEnum();

        RuleFor(x => x.Message)
            .NotEmpty()
            .MinimumLength(Enquiry.MinMessageLength)
            .MaximumLength(Enquiry.MaxMessageLength);

        RuleFor(x => x.ConsentGiven)
            .Equal(true)
            .WithMessage("Please agree to be contacted so we can reply.");
    }
}

public sealed class SubmitEnquiryHandler(
    IEnquiryRepository enquiries,
    IUnitOfWork unitOfWork,
    IEnquiryAlert alert,
    IDateTimeProvider clock,
    ILogger<SubmitEnquiryHandler> logger) : ICommandHandler<SubmitEnquiryCommand, Unit>
{
    public async Task<Unit> HandleAsync(SubmitEnquiryCommand command, CancellationToken cancellationToken)
    {
        var enquiry = Enquiry.Submit(
            command.Name,
            command.Email,
            command.Kind,
            command.Message,
            command.Source,
            command.ConsentGiven,
            clock.UtcNow);

        enquiries.Add(enquiry);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // The sender's name, address and message are personal information: identifiers and
        // the kind only.
        logger.LogInformation("Enquiry {EnquiryId} received ({Kind})", enquiry.Id, enquiry.Kind);

        await alert.NotifyAsync(enquiry.Id, enquiry.Kind, cancellationToken);

        return Unit.Value;
    }
}

// ---- the staff queue ----

public sealed record AdminEnquiry(
    Guid EnquiryId,
    string Name,
    string Email,
    string Kind,
    string Message,
    string Source,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? HandledAtUtc);

public sealed record ListEnquiriesQuery(bool IncludeClosed = false, int Take = 100)
    : IQuery<IReadOnlyList<AdminEnquiry>>;

public sealed class ListEnquiriesHandler(IEnquiryRepository enquiries)
    : IQueryHandler<ListEnquiriesQuery, IReadOnlyList<AdminEnquiry>>
{
    public async Task<IReadOnlyList<AdminEnquiry>> HandleAsync(
        ListEnquiriesQuery query,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(query.Take, 1, 200);

        var rows = await enquiries.ListAsync(query.IncludeClosed, take, cancellationToken);

        return rows
            .Select(e => new AdminEnquiry(
                e.Id,
                e.Name,
                e.Email,
                e.Kind.ToString(),
                e.Message,
                e.Source,
                e.Status.ToString(),
                e.CreatedAtUtc,
                e.HandledAtUtc))
            .ToList();
    }
}

public sealed record CloseEnquiryCommand(Guid EnquiryId, bool IsSpam) : ICommand<Unit>;

public sealed class CloseEnquiryHandler(
    IEnquiryRepository enquiries,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<CloseEnquiryCommand, Unit>
{
    public async Task<Unit> HandleAsync(CloseEnquiryCommand command, CancellationToken cancellationToken)
    {
        var staffUserId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var enquiry = await enquiries.FindByIdAsync(command.EnquiryId, cancellationToken)
            ?? throw new NotFoundException("That enquiry does not exist.");

        try
        {
            if (command.IsSpam)
            {
                enquiry.MarkSpam(staffUserId, clock.UtcNow);
            }
            else
            {
                enquiry.MarkHandled(staffUserId, clock.UtcNow);
            }
        }
        catch (GoSit.Domain.Common.DomainException ex)
        {
            throw new ConflictException(ex.Message);
        }

        audit.Record(
            nameof(Enquiry),
            enquiry.Id,
            command.IsSpam ? "EnquiryMarkedSpam" : "EnquiryHandled",
            staffUserId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
