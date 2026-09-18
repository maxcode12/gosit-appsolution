using GoSit.Domain.Common;
using GoSit.Domain.Interviews;

namespace GoSit.UnitTests.Domain;

/// <summary>
/// The rules of a video interview: who may do what, when the room is open, and that
/// nobody gets in without agreeing to the recording.
/// </summary>
public class InterviewTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 14, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Family = Guid.NewGuid();
    private static readonly Guid Caregiver = Guid.NewGuid();
    private static readonly Guid Thread = Guid.NewGuid();

    private static Interview Requested(DateTime? start = null, int minutes = 30) =>
        Interview.Request(Family, Caregiver, Thread, start ?? Now.AddDays(1), minutes, Now);

    private static Interview Accepted(DateTime? start = null)
    {
        var interview = Requested(start);
        interview.Accept(Caregiver, Now.AddMinutes(5));
        return interview;
    }

    // ---- requesting ----

    [Fact]
    public void A_request_needs_an_hour_of_notice()
    {
        var ex = Assert.Throws<DomainException>(() => Requested(Now.AddMinutes(30)));

        Assert.Contains("at least an hour", ex.Message);
    }

    [Fact]
    public void A_request_cannot_be_more_than_sixty_days_out()
    {
        Assert.Throws<DomainException>(() => Requested(Now.AddDays(61)));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(90)]
    public void Only_the_listed_lengths_are_allowed(int minutes)
    {
        Assert.Throws<DomainException>(() => Requested(minutes: minutes));
    }

    [Fact]
    public void The_room_opens_ten_minutes_before_and_stays_open_half_an_hour_after()
    {
        var start = Now.AddDays(1);
        var interview = Requested(start);

        Assert.Equal(start.AddMinutes(-10), interview.JoinOpensAtUtc);
        Assert.Equal(start.AddMinutes(30 + 30), interview.JoinClosesAtUtc);
    }

    // ---- answering ----

    [Fact]
    public void Only_the_caregiver_can_accept()
    {
        var interview = Requested();

        var ex = Assert.Throws<DomainException>(() => interview.Accept(Family, Now));

        Assert.Contains("Only the caregiver", ex.Message);
    }

    [Fact]
    public void A_request_whose_time_has_passed_cannot_be_accepted()
    {
        var interview = Requested(Now.AddHours(2));

        Assert.Throws<DomainException>(() => interview.Accept(Caregiver, Now.AddHours(3)));
    }

    [Fact]
    public void An_answered_request_cannot_be_answered_again()
    {
        var interview = Accepted();

        Assert.Throws<DomainException>(() => interview.Decline(Caregiver, Now));
    }

    [Fact]
    public void Either_side_can_cancel_before_it_starts_and_the_record_says_who()
    {
        var byFamily = Accepted();
        byFamily.Cancel(Family, Now.AddHours(1));

        var byCaregiver = Accepted();
        byCaregiver.Cancel(Caregiver, Now.AddHours(1));

        Assert.Equal(InterviewStatus.Cancelled, byFamily.Status);
        Assert.False(byFamily.CancelledByCaregiver);
        Assert.True(byCaregiver.CancelledByCaregiver);
    }

    [Fact]
    public void Nothing_can_be_cancelled_once_it_has_started()
    {
        var interview = Accepted(Now.AddHours(2));

        Assert.Throws<DomainException>(() => interview.Cancel(Family, Now.AddHours(2)));
    }

    // ---- joining ----

    [Fact]
    public void Joining_requires_agreeing_to_the_recording()
    {
        var interview = Accepted();

        var ex = Assert.Throws<DomainException>(
            () => interview.RecordJoin(Family, consentsToRecording: false, interview.ScheduledStartUtc));

        Assert.Contains("recorded", ex.Message);
        Assert.Null(interview.CareRequesterJoinedAtUtc);
    }

    [Fact]
    public void Joining_records_consent_and_the_first_arrival_only()
    {
        var interview = Accepted();
        var first = interview.ScheduledStartUtc.AddMinutes(-5);

        interview.RecordJoin(Caregiver, true, first);
        interview.RecordJoin(Caregiver, true, first.AddMinutes(10));

        Assert.Equal(first, interview.CaregiverConsentedAtUtc);
        Assert.Equal(first, interview.CaregiverJoinedAtUtc);
        Assert.Null(interview.CareRequesterJoinedAtUtc);
    }

    [Fact]
    public void The_room_is_closed_before_it_opens_and_after_it_closes()
    {
        var interview = Accepted();

        Assert.Throws<DomainException>(
            () => interview.RecordJoin(Family, true, interview.JoinOpensAtUtc.AddMinutes(-1)));
        Assert.Throws<DomainException>(
            () => interview.RecordJoin(Family, true, interview.JoinClosesAtUtc.AddMinutes(1)));
    }

    [Fact]
    public void A_request_nobody_accepted_cannot_be_joined()
    {
        var interview = Requested();

        Assert.False(interview.CanBeJoined(interview.ScheduledStartUtc));
        Assert.Throws<DomainException>(() => interview.RecordJoin(Family, true, interview.ScheduledStartUtc));
    }

    // ---- the call and its recordings ----

    [Fact]
    public void A_call_someone_joined_is_complete_when_the_room_closes()
    {
        var interview = Accepted();
        interview.RecordJoin(Family, true, interview.ScheduledStartUtc);

        interview.MarkEnded(interview.ScheduledEndUtc);

        Assert.Equal(InterviewStatus.Completed, interview.Status);
    }

    [Fact]
    public void A_room_that_closed_with_nobody_in_it_is_not_complete()
    {
        var interview = Accepted();

        interview.MarkEnded(interview.ScheduledEndUtc);

        Assert.Equal(InterviewStatus.Accepted, interview.Status);
    }

    [Fact]
    public void A_finished_recording_is_kept_for_ninety_days()
    {
        var interview = Accepted();
        var started = interview.ScheduledStartUtc;
        interview.StartRecording("EG_1", "2026/03/key.mp4", started);

        Assert.True(interview.FinishRecording("EG_1", succeeded: true, started.AddMinutes(30)));

        var recording = Assert.Single(interview.Recordings);
        Assert.True(recording.IsWatchable);
        Assert.Equal(started.AddMinutes(30).AddDays(90), recording.DeleteAfterUtc);
    }

    [Fact]
    public void A_retried_webhook_does_not_move_the_deletion_date()
    {
        var interview = Accepted();
        var started = interview.ScheduledStartUtc;
        interview.StartRecording("EG_1", "2026/03/key.mp4", started);
        interview.FinishRecording("EG_1", true, started.AddMinutes(30));

        Assert.False(interview.FinishRecording("EG_1", true, started.AddHours(5)));
        Assert.Equal(started.AddMinutes(30).AddDays(90), interview.Recordings.Single().DeleteAfterUtc);
    }

    [Fact]
    public void A_failed_recording_is_not_watchable_but_is_still_deleted_on_schedule()
    {
        var interview = Accepted();
        var started = interview.ScheduledStartUtc;
        interview.StartRecording("EG_1", "2026/03/key.mp4", started);
        interview.FinishRecording("EG_1", succeeded: false, started.AddMinutes(1));

        var recording = interview.Recordings.Single();

        Assert.False(recording.IsWatchable);
        Assert.Single(interview.RecordingsDueForDeletion(started.AddDays(91)));
    }

    [Fact]
    public void Recordings_on_hold_are_never_due_for_deletion()
    {
        var interview = Accepted();
        var started = interview.ScheduledStartUtc;
        interview.StartRecording("EG_1", "2026/03/key.mp4", started);
        interview.FinishRecording("EG_1", true, started.AddMinutes(30));

        interview.HoldRecordings(Guid.NewGuid(), "Dispute on a later booking.", started.AddDays(2));

        Assert.Empty(interview.RecordingsDueForDeletion(started.AddDays(400)));
        Assert.Throws<DomainException>(
            () => interview.DeleteRecording(interview.Recordings.Single().Id, started.AddDays(400)));
    }

    [Fact]
    public void A_hold_needs_a_reason_and_something_to_hold()
    {
        var nothing = Accepted();
        Assert.Throws<DomainException>(() => nothing.HoldRecordings(Guid.NewGuid(), "Dispute.", Now));

        var recorded = Accepted();
        recorded.StartRecording("EG_1", "k", recorded.ScheduledStartUtc);
        Assert.Throws<DomainException>(() => recorded.HoldRecordings(Guid.NewGuid(), "  ", Now));
    }

    [Fact]
    public void Deleting_a_recording_clears_where_it_was_and_keeps_that_it_existed()
    {
        var interview = Accepted();
        var started = interview.ScheduledStartUtc;
        interview.StartRecording("EG_1", "2026/03/key.mp4", started);
        interview.FinishRecording("EG_1", true, started.AddMinutes(30));

        interview.DeleteRecording(interview.Recordings.Single().Id, started.AddDays(91));

        var recording = Assert.Single(interview.Recordings);
        Assert.Equal(InterviewRecordingStatus.Deleted, recording.Status);
        Assert.Null(recording.StorageKey);
        Assert.False(recording.IsWatchable);
    }

    [Fact]
    public void The_room_name_round_trips_to_the_interview_id()
    {
        var interview = Requested();

        Assert.Equal(interview.Id, Interview.IdFromRoomName(interview.RoomName));
        Assert.Null(Interview.IdFromRoomName("not-an-interview"));
        Assert.Null(Interview.IdFromRoomName(null));
    }
}
