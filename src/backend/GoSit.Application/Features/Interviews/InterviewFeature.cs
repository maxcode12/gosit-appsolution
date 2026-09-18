using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.Messaging.Queries;
using GoSit.Domain.Interviews;
using GoSit.Domain.Messaging;
using GoSit.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Interviews;

/// <summary>
/// An interview as one participant sees it.
///
/// Names the other person the way messaging does — first name and last initial — and
/// carries nothing about the recording beyond the fact that there is one. Recordings are
/// for staff; a participant never gets a link to one.
/// </summary>
public sealed record InterviewResponse(
    Guid Id,
    string Status,
    Guid CareRequesterProfileId,
    Guid CaregiverProfileId,
    Guid ConversationId,
    string OtherParticipantFirstName,
    string OtherParticipantLastInitial,
    DateTime ScheduledStartUtc,
    DateTime ScheduledEndUtc,
    int DurationMinutes,
    DateTime JoinOpensAtUtc,
    DateTime JoinClosesAtUtc,
    bool CanAccept,
    bool CanDecline,
    bool CanCancel,
    bool CanJoin,
    DateTime RequestedAtUtc);

/// <summary>What the browser needs to connect to the call, and where its chat goes.</summary>
public sealed record JoinInterviewResponse(
    Guid InterviewId,
    Guid ConversationId,
    string ServerUrl,
    string Token,
    DateTime ExpiresAtUtc);

public sealed record RequestInterviewCommand(Guid CaregiverProfileId, DateTime ScheduledStartUtc, int DurationMinutes)
    : ICommand<InterviewResponse>;

public sealed record AcceptInterviewCommand(Guid InterviewId) : ICommand<InterviewResponse>;

public sealed record DeclineInterviewCommand(Guid InterviewId) : ICommand<InterviewResponse>;

public sealed record CancelInterviewCommand(Guid InterviewId) : ICommand<InterviewResponse>;

public sealed record JoinInterviewCommand(Guid InterviewId, bool ConsentToRecording) : ICommand<JoinInterviewResponse>;

public sealed record ListInterviewsQuery(Guid? WithProfileId) : IQuery<IReadOnlyList<InterviewResponse>>;

public sealed record GetInterviewQuery(Guid InterviewId) : IQuery<InterviewResponse>;

public sealed class RequestInterviewValidator : AbstractValidator<RequestInterviewCommand>
{
    public RequestInterviewValidator()
    {
        RuleFor(x => x.CaregiverProfileId).NotEmpty();
        RuleFor(x => x.ScheduledStartUtc).NotEmpty();
        RuleFor(x => x.DurationMinutes)
            .Must(minutes => InterviewPolicy.AllowedDurationsMinutes.Contains(minutes))
            .WithMessage("An interview can be 15, 30, 45 or 60 minutes long.");
    }
}

/// <summary>An interview the caller is part of, and which side they are on.</summary>
public sealed record InterviewContext(
    Interview Interview,
    MessagingParticipantResolver.Participant Participant,
    Guid UserId);

/// <summary>
/// Loads an interview for its participants only. Anyone else is told it does not exist,
/// so interview ids cannot be probed.
/// </summary>
public sealed class InterviewAccess(
    IInterviewRepository interviews,
    MessagingParticipantResolver participants,
    ICurrentUser currentUser)
{
    public async Task<InterviewContext> LoadAsync(Guid interviewId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var interview = await interviews.FindByIdAsync(interviewId, cancellationToken)
            ?? throw new NotFoundException("That interview does not exist.");

        var participant = await participants.ResolveAsync(userId, cancellationToken);

        if (participant is null || !interview.IsParticipant(participant.ProfileId))
        {
            throw new NotFoundException("That interview does not exist.");
        }

        return new InterviewContext(interview, participant, userId);
    }
}

public sealed class InterviewPresenter(IProfileDirectory profiles, IDateTimeProvider clock)
{
    public async Task<InterviewResponse> ToResponseAsync(
        Interview interview,
        MessagingParticipantResolver.Participant viewer,
        CancellationToken cancellationToken) =>
        (await ToResponsesAsync([interview], viewer, cancellationToken))[0];

    public async Task<IReadOnlyList<InterviewResponse>> ToResponsesAsync(
        IReadOnlyList<Interview> interviews,
        MessagingParticipantResolver.Participant viewer,
        CancellationToken cancellationToken)
    {
        if (interviews.Count == 0)
        {
            return [];
        }

        var otherIds = interviews.Select(i => OtherOf(i, viewer)).Distinct().ToList();
        var names = await profiles.ListProfileNamesAsync(otherIds, cancellationToken);
        var utcNow = clock.UtcNow;

        return interviews
            .Select(interview =>
            {
                var name = names.GetValueOrDefault(OtherOf(interview, viewer));

                return new InterviewResponse(
                    interview.Id,
                    DisplayStatus(interview, utcNow),
                    interview.CareRequesterProfileId,
                    interview.CaregiverProfileId,
                    interview.ConversationId,
                    name?.FirstName ?? "GoSit user",
                    name is { LastName.Length: > 0 } ? name.LastName[..1] : string.Empty,
                    interview.ScheduledStartUtc,
                    interview.ScheduledEndUtc,
                    interview.DurationMinutes,
                    interview.JoinOpensAtUtc,
                    interview.JoinClosesAtUtc,
                    CanAccept: viewer.IsCaregiver && interview.AwaitsReply(utcNow),
                    CanDecline: viewer.IsCaregiver && interview.AwaitsReply(utcNow),
                    CanCancel: interview.CanBeCancelled(utcNow),
                    CanJoin: interview.CanBeJoined(utcNow),
                    interview.RequestedAtUtc);
            })
            .ToList();
    }

    /// <summary>
    /// "Expired" and "Missed" are what time does to a stored status, worked out here so a
    /// request nobody answered never reads as still waiting.
    /// </summary>
    private static string DisplayStatus(Interview interview, DateTime utcNow) => interview.Status switch
    {
        InterviewStatus.Requested when utcNow >= interview.ScheduledStartUtc => "Expired",
        InterviewStatus.Accepted when utcNow > interview.JoinClosesAtUtc =>
            interview.WasJoined ? nameof(InterviewStatus.Completed) : "Missed",
        _ => interview.Status.ToString(),
    };

    private static Guid OtherOf(Interview interview, MessagingParticipantResolver.Participant viewer) =>
        viewer.IsCaregiver ? interview.CareRequesterProfileId : interview.CaregiverProfileId;
}

/// <summary>
/// Tells the other side something happened to an interview.
///
/// Says who and what, never when: a notification is delivered by email and lands on lock
/// screens, and the time of a stranger's visit to a video call is detail the app, not
/// the lock screen, should show.
/// </summary>
public sealed class InterviewNotifier(INotificationRepository notifications, IProfileDirectory profiles)
{
    public async Task NotifyAsync(
        Interview interview,
        bool toCaregiver,
        NotificationType type,
        string title,
        string body,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var userId = toCaregiver
            ? await profiles.FindUserIdForCaregiverAsync(interview.CaregiverProfileId, cancellationToken)
            : await profiles.FindUserIdForCareRequesterAsync(interview.CareRequesterProfileId, cancellationToken);

        if (userId is { } recipient)
        {
            notifications.Add(Notification.Create(
                recipient, type, title, body, nameof(Interview), interview.Id, utcNow));
        }
    }

    public async Task<string> DisplayNameAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var names = await profiles.ListProfileNamesAsync([profileId], cancellationToken);

        if (!names.TryGetValue(profileId, out var name))
        {
            return "Someone";
        }

        return name.LastName.Length > 0 ? $"{name.FirstName} {name.LastName[..1]}." : name.FirstName;
    }
}

public sealed class RequestInterviewHandler(
    IInterviewRepository interviews,
    IConversationRepository conversations,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverSearchRepository caregivers,
    IVideoMeetingProvider video,
    InterviewNotifier notifier,
    InterviewPresenter presenter,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<RequestInterviewCommand, InterviewResponse>
{
    public async Task<InterviewResponse> HandleAsync(
        RequestInterviewCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var requester = await requesterProfiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Create your care-requester profile first.");

        // Only a listable caregiver can be asked, as with messaging, so this cannot be used
        // to probe whether an arbitrary profile id exists.
        _ = await caregivers.FindListableByIdAsync(command.CaregiverProfileId, cancellationToken)
            ?? throw new NotFoundException("That caregiver is not available.");

        // Refused up front rather than at the door: arranging a call that can never start
        // would waste both people's time.
        if (!video.IsConfigured)
        {
            throw new ConflictException("Video interviews are not available yet.");
        }

        var utcNow = clock.UtcNow;

        if (await interviews.HasOpenInterviewAsync(
                requester.Id, command.CaregiverProfileId, utcNow, cancellationToken))
        {
            throw new ConflictException(
                "You already have an interview arranged with this caregiver. Cancel it to ask for a different time.");
        }

        // The interview's chat is the pair's conversation, so there is always one to write in.
        var conversation = await conversations.FindBetweenAsync(
            requester.Id, command.CaregiverProfileId, cancellationToken);

        if (conversation is null)
        {
            conversation = Conversation.Start(requester.Id, command.CaregiverProfileId, null, utcNow);
            conversations.Add(conversation);
        }

        var interview = Interview.Request(
            requester.Id,
            command.CaregiverProfileId,
            conversation.Id,
            AsUtc(command.ScheduledStartUtc),
            command.DurationMinutes,
            utcNow);

        interviews.Add(interview);

        var name = await notifier.DisplayNameAsync(requester.Id, cancellationToken);

        await notifier.NotifyAsync(
            interview,
            toCaregiver: true,
            NotificationType.InterviewRequested,
            $"{name} would like a video interview",
            "Open GoSit to accept or decline it.",
            utcNow,
            cancellationToken);

        audit.Record(nameof(Interview), interview.Id, "Requested", userId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await presenter.ToResponseAsync(
            interview, new MessagingParticipantResolver.Participant(requester.Id, IsCaregiver: false), cancellationToken);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}

public sealed class AcceptInterviewHandler(
    InterviewAccess access,
    InterviewNotifier notifier,
    InterviewPresenter presenter,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock) : ICommandHandler<AcceptInterviewCommand, InterviewResponse>
{
    public async Task<InterviewResponse> HandleAsync(
        AcceptInterviewCommand command,
        CancellationToken cancellationToken)
    {
        var (interview, participant, userId) = await access.LoadAsync(command.InterviewId, cancellationToken);
        var utcNow = clock.UtcNow;

        interview.Accept(participant.ProfileId, utcNow);

        var name = await notifier.DisplayNameAsync(participant.ProfileId, cancellationToken);

        await notifier.NotifyAsync(
            interview,
            toCaregiver: false,
            NotificationType.InterviewAccepted,
            $"{name} accepted your video interview",
            "Open GoSit to see when you can join.",
            utcNow,
            cancellationToken);

        audit.Record(nameof(Interview), interview.Id, "Accepted", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await presenter.ToResponseAsync(interview, participant, cancellationToken);
    }
}

public sealed class DeclineInterviewHandler(
    InterviewAccess access,
    InterviewNotifier notifier,
    InterviewPresenter presenter,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock) : ICommandHandler<DeclineInterviewCommand, InterviewResponse>
{
    public async Task<InterviewResponse> HandleAsync(
        DeclineInterviewCommand command,
        CancellationToken cancellationToken)
    {
        var (interview, participant, userId) = await access.LoadAsync(command.InterviewId, cancellationToken);
        var utcNow = clock.UtcNow;

        interview.Decline(participant.ProfileId, utcNow);

        var name = await notifier.DisplayNameAsync(participant.ProfileId, cancellationToken);

        await notifier.NotifyAsync(
            interview,
            toCaregiver: false,
            NotificationType.InterviewDeclined,
            $"{name} declined your video interview",
            "You can message them to find another time.",
            utcNow,
            cancellationToken);

        audit.Record(nameof(Interview), interview.Id, "Declined", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await presenter.ToResponseAsync(interview, participant, cancellationToken);
    }
}

public sealed class CancelInterviewHandler(
    InterviewAccess access,
    InterviewNotifier notifier,
    InterviewPresenter presenter,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock) : ICommandHandler<CancelInterviewCommand, InterviewResponse>
{
    public async Task<InterviewResponse> HandleAsync(
        CancelInterviewCommand command,
        CancellationToken cancellationToken)
    {
        var (interview, participant, userId) = await access.LoadAsync(command.InterviewId, cancellationToken);
        var utcNow = clock.UtcNow;

        interview.Cancel(participant.ProfileId, utcNow);

        var name = await notifier.DisplayNameAsync(participant.ProfileId, cancellationToken);

        await notifier.NotifyAsync(
            interview,
            toCaregiver: !participant.IsCaregiver,
            NotificationType.InterviewCancelled,
            $"{name} cancelled your video interview",
            "Open GoSit to see your conversation.",
            utcNow,
            cancellationToken);

        audit.Record(nameof(Interview), interview.Id, "Cancelled", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await presenter.ToResponseAsync(interview, participant, cancellationToken);
    }
}

/// <summary>
/// Lets one participant into the call.
///
/// The order is the guarantee. Consent and the join are saved first; then the recording
/// is confirmed running, or started; only then is a token issued. A request that cannot
/// get the recording going gets an error and no token, so there is no way into an
/// interview that is not being recorded.
/// </summary>
public sealed class JoinInterviewHandler(
    InterviewAccess access,
    IInterviewRepository interviews,
    IVideoMeetingProvider video,
    IRecordingStore recordings,
    InterviewNotifier notifier,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<JoinInterviewHandler> logger) : ICommandHandler<JoinInterviewCommand, JoinInterviewResponse>
{
    /// <summary>Long enough to start a recording, short enough that a crash mid-start does not block the call.</summary>
    private static readonly TimeSpan RecordingLease = TimeSpan.FromSeconds(45);

    /// <summary>How long to wait for someone else's recording start before giving up.</summary>
    private const int WaitForOtherStartAttempts = 8;

    public async Task<JoinInterviewResponse> HandleAsync(
        JoinInterviewCommand command,
        CancellationToken cancellationToken)
    {
        var (interview, participant, userId) = await access.LoadAsync(command.InterviewId, cancellationToken);

        if (!video.IsConfigured || !recordings.IsLive)
        {
            throw new ConflictException("Video interviews are not available yet.");
        }

        interview.RecordJoin(participant.ProfileId, command.ConsentToRecording, clock.UtcNow);

        audit.Record(
            nameof(Interview),
            interview.Id,
            participant.IsCaregiver ? "CaregiverJoinedConsentingToRecording" : "CareRequesterJoinedConsentingToRecording",
            userId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await EnsureRecordingAsync(interview, userId, cancellationToken);

        var displayName = await notifier.DisplayNameAsync(participant.ProfileId, cancellationToken);

        // An opaque identity: which side, and a profile id the video service cannot turn
        // back into a person.
        var identity = $"{(participant.IsCaregiver ? "caregiver" : "family")}-{participant.ProfileId:N}";

        var ticket = video.IssueJoinTicket(
            interview.RoomName, identity, displayName, interview.JoinClosesAtUtc);

        return new JoinInterviewResponse(
            interview.Id, interview.ConversationId, ticket.ServerUrl, ticket.Token, ticket.ExpiresAtUtc);
    }

    private async Task EnsureRecordingAsync(Interview interview, Guid userId, CancellationToken cancellationToken)
    {
        var room = interview.RoomName;

        try
        {
            if (await video.IsRecordingAsync(room, cancellationToken))
            {
                return;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The service cannot even be asked, so nothing about this call can be
            // guaranteed. Same answer as a recording that will not start.
            logger.LogWarning(ex, "Could not reach the video service for interview {InterviewId}", interview.Id);

            throw new ConflictException(
                "The video service could not be reached, so the interview cannot begin. Try again in a moment.");
        }

        var utcNow = clock.UtcNow;

        if (!await interviews.TryClaimRecordingLeaseAsync(
                interview.Id, utcNow, utcNow + RecordingLease, cancellationToken))
        {
            // The other person is starting it at this moment. Wait for theirs rather than
            // starting a second recording of the same call.
            for (var attempt = 0; attempt < WaitForOtherStartAttempts; attempt++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);

                if (await video.IsRecordingAsync(room, cancellationToken))
                {
                    return;
                }
            }

            throw new ConflictException("The recording is still starting. Try joining again in a moment.");
        }

        try
        {
            await recordings.PrepareAsync(cancellationToken);
            await video.PrepareRoomAsync(room, cancellationToken);

            var key = Interview.RecordingKeyFor(interview.Id, utcNow);
            var providerRecordingId = await video.StartRecordingAsync(room, key, cancellationToken);

            interview.StartRecording(providerRecordingId, key, utcNow);
            audit.Record(nameof(Interview), interview.Id, "RecordingStarted", userId);

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Identifiers only: the storage key and the provider's response say nothing a
            // log needs and something about where video of a family is kept.
            logger.LogWarning(ex, "Could not start the recording for interview {InterviewId}", interview.Id);

            await interviews.ReleaseRecordingLeaseAsync(interview.Id, cancellationToken);

            throw new ConflictException(
                "The recording could not be started, so the interview cannot begin. Try again in a moment.");
        }

        // Released once the recording is running: the video service now answers "is this
        // room being recorded", and a lease left behind would only delay a restart.
        await interviews.ReleaseRecordingLeaseAsync(interview.Id, cancellationToken);
    }
}

public sealed class ListInterviewsHandler(
    IInterviewRepository interviews,
    MessagingParticipantResolver participants,
    InterviewPresenter presenter,
    ICurrentUser currentUser) : IQueryHandler<ListInterviewsQuery, IReadOnlyList<InterviewResponse>>
{
    public async Task<IReadOnlyList<InterviewResponse>> HandleAsync(
        ListInterviewsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var participant = await participants.ResolveAsync(userId, cancellationToken);

        if (participant is null)
        {
            return [];
        }

        var found = await interviews.ListForParticipantAsync(
            participant.ProfileId, participant.IsCaregiver, query.WithProfileId, cancellationToken);

        return await presenter.ToResponsesAsync(found, participant, cancellationToken);
    }
}

public sealed class GetInterviewHandler(InterviewAccess access, InterviewPresenter presenter)
    : IQueryHandler<GetInterviewQuery, InterviewResponse>
{
    public async Task<InterviewResponse> HandleAsync(GetInterviewQuery query, CancellationToken cancellationToken)
    {
        var (interview, participant, _) = await access.LoadAsync(query.InterviewId, cancellationToken);

        return await presenter.ToResponseAsync(interview, participant, cancellationToken);
    }
}

/// <summary>A webhook from the video service, verified before anything is read from it.</summary>
public sealed record HandleVideoWebhookCommand(string Payload, string Authorization) : ICommand<Unit>;

/// <summary>
/// Applies what the video service reports: a recording finished or failed, or a room closed.
///
/// A recording that fails while the call is still going ends the call. The alternative is
/// a conversation carrying on unrecorded, which is the one thing joining promised would not
/// happen; whoever rejoins starts a fresh recording.
/// </summary>
public sealed class HandleVideoWebhookHandler(
    IVideoMeetingProvider video,
    IInterviewRepository interviews,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<HandleVideoWebhookHandler> logger) : ICommandHandler<HandleVideoWebhookCommand, Unit>
{
    public async Task<Unit> HandleAsync(HandleVideoWebhookCommand command, CancellationToken cancellationToken)
    {
        var received = await video.VerifyWebhookAsync(command.Payload, command.Authorization, cancellationToken);
        var utcNow = clock.UtcNow;

        switch (received.Kind)
        {
            case VideoWebhookKind.RecordingEnded when received.ProviderRecordingId is { } recordingId:
            {
                var interview = await interviews.FindByProviderRecordingIdAsync(recordingId, cancellationToken);

                if (interview is null || !interview.FinishRecording(recordingId, received.RecordingSucceeded, utcNow))
                {
                    break;
                }

                audit.Record(
                    nameof(Interview),
                    interview.Id,
                    received.RecordingSucceeded ? "RecordingSaved" : "RecordingFailed",
                    actorUserId: null);

                await unitOfWork.SaveChangesAsync(cancellationToken);

                if (!received.RecordingSucceeded && utcNow <= interview.JoinClosesAtUtc)
                {
                    logger.LogWarning(
                        "The recording for interview {InterviewId} failed; ending the call", interview.Id);

                    try
                    {
                        await video.CloseRoomAsync(interview.RoomName, cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Most often the room had already closed, which is what was wanted.
                        logger.LogWarning(ex, "Could not close the room for interview {InterviewId}", interview.Id);
                    }
                }

                break;
            }

            case VideoWebhookKind.RoomFinished when Interview.IdFromRoomName(received.RoomName) is { } interviewId:
            {
                var interview = await interviews.FindByIdAsync(interviewId, cancellationToken);

                if (interview is null)
                {
                    break;
                }

                interview.MarkEnded(utcNow);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                break;
            }
        }

        return Unit.Value;
    }
}
