using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.Interviews;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Administration.Interviews;

public sealed record AdminInterviewRecording(
    Guid RecordingId,
    string Status,
    bool IsWatchable,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    DateTime? DeleteAfterUtc,
    DateTime? DeletedAtUtc);

/// <summary>
/// A recorded interview as staff see it: who, when, and what is still kept. No video and
/// no link to one — watching is a separate request that is audited against the staff member.
/// </summary>
public sealed record AdminInterview(
    Guid InterviewId,
    string CareRequesterName,
    string CaregiverName,
    DateTime ScheduledStartUtc,
    int DurationMinutes,
    string Status,
    bool RecordingsOnHold,
    string? RecordingHoldReason,
    DateTime? RecordingHeldAtUtc,
    IReadOnlyList<AdminInterviewRecording> Recordings);

public sealed record ListRecordedInterviewsQuery(int Take = 50) : IQuery<IReadOnlyList<AdminInterview>>;

public sealed record ReadInterviewRecordingQuery(Guid RecordingId) : IQuery<StoredDocument>;

public sealed record HoldInterviewRecordingsCommand(Guid InterviewId, string Reason) : ICommand<Unit>;

public sealed record ReleaseInterviewRecordingsCommand(Guid InterviewId) : ICommand<Unit>;

public sealed class HoldInterviewRecordingsValidator : AbstractValidator<HoldInterviewRecordingsCommand>
{
    public HoldInterviewRecordingsValidator()
    {
        RuleFor(x => x.InterviewId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(InterviewPolicy.MaxHoldReasonLength);
    }
}

public sealed class ListRecordedInterviewsHandler(IInterviewRepository interviews, IProfileDirectory profiles)
    : IQueryHandler<ListRecordedInterviewsQuery, IReadOnlyList<AdminInterview>>
{
    private const int MaxTake = 200;

    public async Task<IReadOnlyList<AdminInterview>> HandleAsync(
        ListRecordedInterviewsQuery query,
        CancellationToken cancellationToken)
    {
        var found = await interviews.ListRecordedAsync(Math.Clamp(query.Take, 1, MaxTake), cancellationToken);

        if (found.Count == 0)
        {
            return [];
        }

        var profileIds = found
            .SelectMany(i => new[] { i.CareRequesterProfileId, i.CaregiverProfileId })
            .Distinct()
            .ToList();

        var names = await profiles.ListProfileNamesAsync(profileIds, cancellationToken);

        return found
            .Select(interview => new AdminInterview(
                interview.Id,
                FullName(names, interview.CareRequesterProfileId),
                FullName(names, interview.CaregiverProfileId),
                interview.ScheduledStartUtc,
                interview.DurationMinutes,
                interview.Status.ToString(),
                interview.RecordingsOnHold,
                interview.RecordingHoldReason,
                interview.RecordingHeldAtUtc,
                interview.Recordings
                    .OrderBy(r => r.StartedAtUtc)
                    .Select(r => new AdminInterviewRecording(
                        r.Id,
                        r.Status.ToString(),
                        r.IsWatchable,
                        r.StartedAtUtc,
                        r.EndedAtUtc,
                        r.DeleteAfterUtc,
                        r.DeletedAtUtc))
                    .ToList()))
            .ToList();
    }

    private static string FullName(IReadOnlyDictionary<Guid, CaregiverName> names, Guid profileId) =>
        names.TryGetValue(profileId, out var name) ? $"{name.FirstName} {name.LastName}".Trim() : "Unknown";
}

/// <summary>
/// Streams a recording to the staff member watching it.
///
/// Streamed through the API rather than handed out as a storage link, for the reason
/// certification documents are: a link is a bearer token for video of someone's home,
/// valid for whoever ends up holding the URL. Every view is audited, and the audit row is
/// saved before the first byte is sent.
/// </summary>
public sealed class ReadInterviewRecordingHandler(
    IInterviewRepository interviews,
    IRecordingStore store,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ILogger<ReadInterviewRecordingHandler> logger) : IQueryHandler<ReadInterviewRecordingQuery, StoredDocument>
{
    public async Task<StoredDocument> HandleAsync(ReadInterviewRecordingQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var interview = await interviews.FindByRecordingIdAsync(query.RecordingId, cancellationToken)
            ?? throw new NotFoundException("That recording does not exist.");

        var recording = interview.Recordings.Single(r => r.Id == query.RecordingId);

        if (!recording.IsWatchable || recording.StorageKey is not { } key)
        {
            throw new NotFoundException("That recording is not available.");
        }

        audit.Record(nameof(InterviewRecording), recording.Id, "RecordingViewed", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var document = await store.ReadAsync(key, cancellationToken)
            ?? throw new NotFoundException("That recording is no longer available.");

        logger.LogInformation(
            "Interview recording {RecordingId} viewed by staff {UserId}", recording.Id, userId);

        return document;
    }
}

public sealed class HoldInterviewRecordingsHandler(
    IInterviewRepository interviews,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<HoldInterviewRecordingsCommand, Unit>
{
    public async Task<Unit> HandleAsync(HoldInterviewRecordingsCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var interview = await interviews.FindByIdAsync(command.InterviewId, cancellationToken)
            ?? throw new NotFoundException("That interview does not exist.");

        interview.HoldRecordings(userId, command.Reason, clock.UtcNow);

        // The reason stays on the interview, where staff read it; the audit row names the act.
        audit.Record(nameof(Interview), interview.Id, "RecordingsHeld", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class ReleaseInterviewRecordingsHandler(
    IInterviewRepository interviews,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<ReleaseInterviewRecordingsCommand, Unit>
{
    public async Task<Unit> HandleAsync(ReleaseInterviewRecordingsCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var interview = await interviews.FindByIdAsync(command.InterviewId, cancellationToken)
            ?? throw new NotFoundException("That interview does not exist.");

        interview.ReleaseRecordings();

        audit.Record(nameof(Interview), interview.Id, "RecordingsReleased", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
