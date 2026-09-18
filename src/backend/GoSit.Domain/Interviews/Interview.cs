using GoSit.Domain.Common;

namespace GoSit.Domain.Interviews;

/// <summary>
/// A video interview between a family and a caregiver, held inside GoSit.
///
/// Interviews exist so that getting to know a caregiver never needs a phone number or a
/// video link from another service — the step where care most often leaves a marketplace,
/// and with it the verified identity and the record of what was agreed.
///
/// Every interview is recorded. Joining requires agreeing to that, and a call whose
/// recording cannot start does not go ahead. Recordings are evidence for Trust &amp;
/// Safety: only staff can watch them, and they are deleted after
/// <see cref="InterviewPolicy.RecordingRetention"/> unless staff hold them.
///
/// Like messaging, only the family can ask for one. The caregiver accepts or declines.
/// </summary>
public sealed class Interview : Entity
{
    private readonly List<InterviewRecording> _recordings = [];

    private Interview()
    {
    }

    public Guid CareRequesterProfileId { get; private set; }

    public Guid CaregiverProfileId { get; private set; }

    /// <summary>The pair's conversation. Chat during the call is written there.</summary>
    public Guid ConversationId { get; private set; }

    public DateTime ScheduledStartUtc { get; private set; }

    public int DurationMinutes { get; private set; }

    public InterviewStatus Status { get; private set; }

    public DateTime RequestedAtUtc { get; private set; }

    public DateTime? RespondedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public bool? CancelledByCaregiver { get; private set; }

    /// <summary>When each side first agreed to the recording. Every join asks again.</summary>
    public DateTime? CareRequesterConsentedAtUtc { get; private set; }

    public DateTime? CaregiverConsentedAtUtc { get; private set; }

    public DateTime? CareRequesterJoinedAtUtc { get; private set; }

    public DateTime? CaregiverJoinedAtUtc { get; private set; }

    /// <summary>When the video room last closed.</summary>
    public DateTime? EndedAtUtc { get; private set; }

    /// <summary>
    /// A short claim on starting the recording, so two people joining at the same moment
    /// start one recording rather than two. Set and cleared by the repository with a
    /// conditional update, never by this class.
    /// </summary>
    public DateTime? RecordingLeaseUntilUtc { get; private set; }

    /// <summary>
    /// Set by staff when the recordings are needed for a dispute or incident. Nothing on
    /// hold is deleted, however old it is.
    /// </summary>
    public bool RecordingsOnHold { get; private set; }

    public string? RecordingHoldReason { get; private set; }

    public Guid? RecordingHeldByUserId { get; private set; }

    public DateTime? RecordingHeldAtUtc { get; private set; }

    public IReadOnlyCollection<InterviewRecording> Recordings => _recordings.AsReadOnly();

    public DateTime ScheduledEndUtc => ScheduledStartUtc.AddMinutes(DurationMinutes);

    public DateTime JoinOpensAtUtc => ScheduledStartUtc - InterviewPolicy.JoinOpensBefore;

    public DateTime JoinClosesAtUtc => ScheduledEndUtc + InterviewPolicy.JoinClosesAfterEnd;

    public bool WasJoined => CareRequesterJoinedAtUtc is not null || CaregiverJoinedAtUtc is not null;

    /// <summary>The video room's name. Derived from the id, so a webhook can find its interview.</summary>
    public string RoomName => RoomNameFor(Id);

    private const string RoomNamePrefix = "interview-";

    public static string RoomNameFor(Guid interviewId) => $"{RoomNamePrefix}{interviewId:N}";

    public static Guid? IdFromRoomName(string? roomName) =>
        roomName is not null
        && roomName.StartsWith(RoomNamePrefix, StringComparison.Ordinal)
        && Guid.TryParseExact(roomName[RoomNamePrefix.Length..], "N", out var id)
            ? id
            : null;

    /// <summary>Where a recording that starts now is stored. Unique per start.</summary>
    public static string RecordingKeyFor(Guid interviewId, DateTime utcNow) =>
        $"{utcNow:yyyy/MM}/{interviewId:N}-{utcNow:yyyyMMddHHmmss}.mp4";

    public static Interview Request(
        Guid careRequesterProfileId,
        Guid caregiverProfileId,
        Guid conversationId,
        DateTime scheduledStartUtc,
        int durationMinutes,
        DateTime utcNow)
    {
        if (careRequesterProfileId == Guid.Empty
            || caregiverProfileId == Guid.Empty
            || conversationId == Guid.Empty)
        {
            throw new DomainException("Both participants are required.");
        }

        if (!InterviewPolicy.AllowedDurationsMinutes.Contains(durationMinutes))
        {
            throw new DomainException("An interview can be 15, 30, 45 or 60 minutes long.");
        }

        if (scheduledStartUtc < utcNow + InterviewPolicy.MinimumNotice)
        {
            throw new DomainException(
                "An interview must start at least an hour from now, so the caregiver has time to reply.");
        }

        if (scheduledStartUtc > utcNow + InterviewPolicy.MaximumHorizon)
        {
            throw new DomainException("An interview can be at most 60 days ahead.");
        }

        return new Interview
        {
            CareRequesterProfileId = careRequesterProfileId,
            CaregiverProfileId = caregiverProfileId,
            ConversationId = conversationId,
            ScheduledStartUtc = scheduledStartUtc,
            DurationMinutes = durationMinutes,
            Status = InterviewStatus.Requested,
            RequestedAtUtc = utcNow,
        };
    }

    public bool IsParticipant(Guid profileId) =>
        profileId == CareRequesterProfileId || profileId == CaregiverProfileId;

    public bool AwaitsReply(DateTime utcNow) =>
        Status == InterviewStatus.Requested && utcNow < ScheduledStartUtc;

    public bool CanBeCancelled(DateTime utcNow) =>
        Status is InterviewStatus.Requested or InterviewStatus.Accepted && utcNow < ScheduledStartUtc;

    public bool CanBeJoined(DateTime utcNow) =>
        Status is InterviewStatus.Accepted or InterviewStatus.Completed
        && utcNow >= JoinOpensAtUtc
        && utcNow <= JoinClosesAtUtc;

    public void Accept(Guid caregiverProfileId, DateTime utcNow)
    {
        EnsureCaregiver(caregiverProfileId, "accept");
        EnsureAwaitingReply(utcNow);

        Status = InterviewStatus.Accepted;
        RespondedAtUtc = utcNow;
    }

    public void Decline(Guid caregiverProfileId, DateTime utcNow)
    {
        EnsureCaregiver(caregiverProfileId, "decline");
        EnsureAwaitingReply(utcNow);

        Status = InterviewStatus.Declined;
        RespondedAtUtc = utcNow;
    }

    public void Cancel(Guid profileId, DateTime utcNow)
    {
        if (!IsParticipant(profileId))
        {
            throw new DomainException("Only a participant can cancel this interview.");
        }

        if (Status is not (InterviewStatus.Requested or InterviewStatus.Accepted))
        {
            throw new DomainException("This interview can no longer be cancelled.");
        }

        if (utcNow >= ScheduledStartUtc)
        {
            throw new DomainException("An interview that has already started cannot be cancelled.");
        }

        Status = InterviewStatus.Cancelled;
        CancelledAtUtc = utcNow;
        CancelledByCaregiver = profileId == CaregiverProfileId;
    }

    /// <summary>
    /// Records that one side is joining the call.
    ///
    /// Consent is asked on every join rather than once: someone who agreed last week to a
    /// call being recorded has not agreed to this one. The first agreement is kept as the
    /// timestamp; each join also writes an audit row.
    /// </summary>
    public void RecordJoin(Guid profileId, bool consentsToRecording, DateTime utcNow)
    {
        if (!IsParticipant(profileId))
        {
            throw new DomainException("Only a participant can join this interview.");
        }

        if (Status is not (InterviewStatus.Accepted or InterviewStatus.Completed))
        {
            throw new DomainException("Only an accepted interview can be joined.");
        }

        if (utcNow < JoinOpensAtUtc)
        {
            throw new DomainException(
                $"You can join from {InterviewPolicy.JoinOpensBefore.TotalMinutes:0} minutes before the interview.");
        }

        if (utcNow > JoinClosesAtUtc)
        {
            throw new DomainException("This interview has ended.");
        }

        if (!consentsToRecording)
        {
            throw new DomainException(
                "Interviews on GoSit are recorded. Agree to the recording to join.");
        }

        if (profileId == CaregiverProfileId)
        {
            CaregiverConsentedAtUtc ??= utcNow;
            CaregiverJoinedAtUtc ??= utcNow;
        }
        else
        {
            CareRequesterConsentedAtUtc ??= utcNow;
            CareRequesterJoinedAtUtc ??= utcNow;
        }
    }

    public InterviewRecording StartRecording(string providerRecordingId, string storageKey, DateTime utcNow)
    {
        if (Status is not (InterviewStatus.Accepted or InterviewStatus.Completed))
        {
            throw new DomainException("Only an accepted interview can be recorded.");
        }

        var recording = InterviewRecording.Start(Id, providerRecordingId, storageKey, utcNow);
        _recordings.Add(recording);

        return recording;
    }

    /// <summary>
    /// The video service reports that a recording stopped.
    ///
    /// Returns false for a recording this interview does not know or has already closed —
    /// webhooks are retried, and a retry must not move a recording's deletion date.
    /// </summary>
    public bool FinishRecording(string providerRecordingId, bool succeeded, DateTime utcNow)
    {
        var recording = _recordings.SingleOrDefault(r => r.ProviderRecordingId == providerRecordingId);

        if (recording is null || recording.Status != InterviewRecordingStatus.Recording)
        {
            return false;
        }

        recording.Finish(succeeded, utcNow);
        return true;
    }

    /// <summary>The video room closed. A call someone joined is now complete.</summary>
    public void MarkEnded(DateTime utcNow)
    {
        EndedAtUtc = utcNow;

        if (Status == InterviewStatus.Accepted && WasJoined)
        {
            Status = InterviewStatus.Completed;
        }
    }

    public void HoldRecordings(Guid staffUserId, string reason, DateTime utcNow)
    {
        if (staffUserId == Guid.Empty)
        {
            throw new DomainException("A staff member is required.");
        }

        if (_recordings.Count == 0)
        {
            throw new DomainException("This interview has no recordings to keep.");
        }

        var trimmed = reason?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new DomainException("Say why these recordings need to be kept.");
        }

        if (trimmed.Length > InterviewPolicy.MaxHoldReasonLength)
        {
            throw new DomainException(
                $"The reason must be {InterviewPolicy.MaxHoldReasonLength} characters or fewer.");
        }

        RecordingsOnHold = true;
        RecordingHoldReason = trimmed;
        RecordingHeldByUserId = staffUserId;
        RecordingHeldAtUtc = utcNow;
    }

    /// <summary>
    /// Lifts a hold. Recordings already past their deletion date are deleted on the next
    /// sweep — the hold was the only thing keeping them.
    /// </summary>
    public void ReleaseRecordings()
    {
        if (!RecordingsOnHold)
        {
            throw new DomainException("These recordings are not on hold.");
        }

        RecordingsOnHold = false;
        RecordingHoldReason = null;
        RecordingHeldByUserId = null;
        RecordingHeldAtUtc = null;
    }

    public IReadOnlyList<InterviewRecording> RecordingsDueForDeletion(DateTime utcNow) =>
        RecordingsOnHold ? [] : _recordings.Where(r => r.IsDueForDeletion(utcNow)).ToList();

    public void DeleteRecording(Guid recordingId, DateTime utcNow)
    {
        if (RecordingsOnHold)
        {
            throw new DomainException("These recordings are on hold and cannot be deleted.");
        }

        var recording = _recordings.SingleOrDefault(r => r.Id == recordingId)
            ?? throw new DomainException("That recording does not belong to this interview.");

        recording.MarkDeleted(utcNow);
    }

    private void EnsureCaregiver(Guid profileId, string action)
    {
        if (profileId != CaregiverProfileId)
        {
            throw new DomainException($"Only the caregiver can {action} this interview.");
        }
    }

    private void EnsureAwaitingReply(DateTime utcNow)
    {
        if (Status != InterviewStatus.Requested)
        {
            throw new DomainException("This interview has already been answered.");
        }

        if (utcNow >= ScheduledStartUtc)
        {
            throw new DomainException("This interview's time has passed. The family can ask for a new one.");
        }
    }
}
