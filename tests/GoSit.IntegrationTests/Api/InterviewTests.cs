using System.Net;
using System.Net.Http.Json;
using GoSit.Application.Abstractions;
using GoSit.Domain.Interviews;
using GoSit.Infrastructure.Persistence;
using GoSit.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Video interviews over HTTP: the scheduling, the door, the recording, and what staff can
/// and cannot do with it.
/// </summary>
[Collection(nameof(ApiCollection))]
public class InterviewTests(GoSitApiFactory factory)
{
    private sealed record InterviewBody(
        Guid Id,
        string Status,
        Guid ConversationId,
        string OtherParticipantFirstName,
        DateTime ScheduledStartUtc,
        int DurationMinutes,
        bool CanAccept,
        bool CanDecline,
        bool CanCancel,
        bool CanJoin);

    private sealed record TicketBody(Guid InterviewId, Guid ConversationId, string ServerUrl, string Token);

    private sealed record MessageBody(Guid Id, string Body, bool SentByMe);

    private sealed record ConversationBody(Guid ConversationId, IReadOnlyList<MessageBody> Messages);

    private sealed record RecordingBody(Guid RecordingId, string Status, bool IsWatchable);

    private sealed record AdminInterviewBody(
        Guid InterviewId,
        string CareRequesterName,
        string CaregiverName,
        bool RecordingsOnHold,
        string? RecordingHoldReason,
        IReadOnlyList<RecordingBody> Recordings);

    private FakeVideoMeetingProvider Video => factory.Services.GetRequiredService<FakeVideoMeetingProvider>();

    private InMemoryRecordingStore Store => factory.Services.GetRequiredService<InMemoryRecordingStore>();

    // ---- helpers ----

    private async Task<(HttpClient Family, Guid CaregiverId)> PairAsync()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var family = await factory.RegisterCareRequesterAsync();
        await family.CreateCareRequesterProfileAsync();

        return (family, caregiverId);
    }

    private static Task<HttpResponseMessage> RequestAsync(HttpClient family, Guid caregiverId, DateTime? start = null) =>
        family.PostAsJsonAsync("/api/v1/interviews", new
        {
            caregiverProfileId = caregiverId,
            scheduledStartUtc = start ?? DateTime.UtcNow.AddDays(1),
            durationMinutes = 30,
        });

    private static async Task<InterviewBody> RequestedAsync(HttpClient family, Guid caregiverId)
    {
        var response = await RequestAsync(family, caregiverId);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InterviewBody>())!;
    }

    private async Task<(HttpClient Family, HttpClient Caregiver, InterviewBody Interview)> AcceptedAsync()
    {
        var (family, caregiverId) = await PairAsync();
        var interview = await RequestedAsync(family, caregiverId);
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        (await caregiver.PostAsync($"/api/v1/interviews/{interview.Id}/accept", null)).EnsureSuccessStatusCode();

        return (family, caregiver, interview);
    }

    /// <summary>Moves the interview so the room is open now.</summary>
    private async Task OpenTheDoorAsync(Guid interviewId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        await context.Interviews
            .Where(i => i.Id == interviewId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ScheduledStartUtc, DateTime.UtcNow.AddMinutes(-2)));
    }

    private static Task<HttpResponseMessage> JoinAsync(HttpClient client, Guid interviewId, bool consent = true) =>
        client.PostAsJsonAsync($"/api/v1/interviews/{interviewId}/join", new { consentToRecording = consent });

    private async Task<HttpResponseMessage> WebhookAsync(VideoWebhookEvent webhook)
    {
        Video.NextWebhook = webhook;

        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/livekit")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "signed-by-test");

        return await client.SendAsync(request);
    }

    private async Task<Interview> ReadAsync(Guid interviewId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        return await context.Interviews.AsNoTracking().Include(i => i.Recordings).SingleAsync(i => i.Id == interviewId);
    }

    // ---- scheduling ----

    [Fact]
    public async Task A_family_asks_and_the_caregiver_is_told()
    {
        var (family, caregiverId) = await PairAsync();

        var response = await RequestAsync(family, caregiverId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var interview = await response.Content.ReadFromJsonAsync<InterviewBody>();
        Assert.Equal("Requested", interview!.Status);
        Assert.True(interview.CanCancel);
        Assert.False(interview.CanJoin);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var seen = await caregiver.GetFromJsonAsync<InterviewBody>($"/api/v1/interviews/{interview.Id}");
        Assert.True(seen!.CanAccept);
        Assert.True(seen.CanDecline);
        Assert.Equal("Ada", seen.OtherParticipantFirstName);

        var notifications = await caregiver.GetFromJsonAsync<NotificationsBody>("/api/v1/notifications");
        Assert.Contains(notifications!.Notifications, n => n.Type == "InterviewRequested");
    }

    private sealed record NotificationsBody(IReadOnlyList<NotificationBody> Notifications);

    private sealed record NotificationBody(string Type, string Title, string? Body);

    [Fact]
    public async Task A_caregiver_cannot_ask_for_one()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var response = await RequestAsync(caregiver, caregiverId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task One_open_interview_per_pair()
    {
        var (family, caregiverId) = await PairAsync();
        await RequestedAsync(family, caregiverId);

        var second = await RequestAsync(family, caregiverId);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Too_little_notice_is_refused()
    {
        var (family, caregiverId) = await PairAsync();

        var response = await RequestAsync(family, caregiverId, DateTime.UtcNow.AddMinutes(20));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Someone_outside_the_interview_is_told_it_does_not_exist()
    {
        var (family, caregiverId) = await PairAsync();
        var interview = await RequestedAsync(family, caregiverId);

        var stranger = await factory.RegisterCareRequesterAsync();
        await stranger.CreateCareRequesterProfileAsync();

        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.GetAsync($"/api/v1/interviews/{interview.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.PostAsync($"/api/v1/interviews/{interview.Id}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task Declining_tells_the_family()
    {
        var (family, caregiverId) = await PairAsync();
        var interview = await RequestedAsync(family, caregiverId);
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var declined = await caregiver.PostAsync($"/api/v1/interviews/{interview.Id}/decline", null);
        declined.EnsureSuccessStatusCode();

        var notifications = await family.GetFromJsonAsync<NotificationsBody>("/api/v1/notifications");
        Assert.Contains(notifications!.Notifications, n => n.Type == "InterviewDeclined");
    }

    // ---- the door ----

    [Fact]
    public async Task Nobody_joins_before_the_room_opens()
    {
        var (family, _, interview) = await AcceptedAsync();

        var response = await JoinAsync(family, interview.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Video.RecordingsStarted.Where(r => r.Room == Interview.RoomNameFor(interview.Id)));
    }

    [Fact]
    public async Task Nobody_joins_without_agreeing_to_the_recording()
    {
        var (family, _, interview) = await AcceptedAsync();
        await OpenTheDoorAsync(interview.Id);

        var response = await JoinAsync(family, interview.Id, consent: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(Video.RecordingFor(Interview.RoomNameFor(interview.Id)));
    }

    [Fact]
    public async Task Joining_starts_the_recording_once_and_issues_a_token_for_this_room_only()
    {
        var (family, caregiver, interview) = await AcceptedAsync();
        await OpenTheDoorAsync(interview.Id);
        var room = Interview.RoomNameFor(interview.Id);

        var first = await JoinAsync(family, interview.Id);
        var second = await JoinAsync(caregiver, interview.Id);

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();

        var ticket = await first.Content.ReadFromJsonAsync<TicketBody>();
        Assert.StartsWith($"token:{room}:family-", ticket!.Token);
        Assert.Equal(interview.ConversationId, ticket.ConversationId);
        Assert.Equal("no-store, private", first.Headers.CacheControl?.ToString());

        Assert.Single(Video.RecordingsStarted.Where(r => r.Room == room));

        var saved = await ReadAsync(interview.Id);
        Assert.NotNull(saved.CareRequesterConsentedAtUtc);
        Assert.NotNull(saved.CaregiverConsentedAtUtc);
        Assert.Equal(InterviewRecordingStatus.Recording, Assert.Single(saved.Recordings).Status);
        Assert.Contains("CareRequesterJoinedConsentingToRecording", await factory.ReadAuditActionsAsync(interview.Id));
        Assert.Contains("RecordingStarted", await factory.ReadAuditActionsAsync(interview.Id));
    }

    [Fact]
    public async Task No_recording_means_no_token()
    {
        var (family, _, interview) = await AcceptedAsync();
        await OpenTheDoorAsync(interview.Id);

        Video.RefuseToRecord = true;
        try
        {
            var response = await JoinAsync(family, interview.Id);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            Video.RefuseToRecord = false;
        }

        // The lease was released, so a retry once the service is back can start it.
        var retry = await JoinAsync(family, interview.Id);
        retry.EnsureSuccessStatusCode();
    }

    /// <summary>Found on a local walkthrough: an unreachable service used to surface as a raw 500.</summary>
    [Fact]
    public async Task An_unreachable_video_service_is_a_clear_refusal_not_a_crash()
    {
        var (family, _, interview) = await AcceptedAsync();
        await OpenTheDoorAsync(interview.Id);

        Video.Unreachable = true;
        try
        {
            var response = await JoinAsync(family, interview.Id);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("could not be reached", await response.Content.ReadAsStringAsync());
        }
        finally
        {
            Video.Unreachable = false;
        }
    }

    // ---- what the video service reports ----

    [Fact]
    public async Task A_finished_recording_is_saved_and_the_closed_room_completes_the_interview()
    {
        var (family, _, interview) = await AcceptedAsync();
        await OpenTheDoorAsync(interview.Id);
        (await JoinAsync(family, interview.Id)).EnsureSuccessStatusCode();
        var room = Interview.RoomNameFor(interview.Id);
        var recordingId = Video.RecordingFor(room)!;

        Video.StopRecording(room);
        (await WebhookAsync(new VideoWebhookEvent(VideoWebhookKind.RecordingEnded, room, recordingId, true)))
            .EnsureSuccessStatusCode();
        (await WebhookAsync(new VideoWebhookEvent(VideoWebhookKind.RoomFinished, room, null, false)))
            .EnsureSuccessStatusCode();

        var saved = await ReadAsync(interview.Id);
        Assert.Equal(InterviewStatus.Completed, saved.Status);
        var recording = Assert.Single(saved.Recordings);
        Assert.Equal(InterviewRecordingStatus.Available, recording.Status);
        Assert.NotNull(recording.DeleteAfterUtc);
        Assert.Contains("RecordingSaved", await factory.ReadAuditActionsAsync(interview.Id));
    }

    [Fact]
    public async Task A_recording_that_fails_mid_call_ends_the_call()
    {
        var (family, _, interview) = await AcceptedAsync();
        await OpenTheDoorAsync(interview.Id);
        (await JoinAsync(family, interview.Id)).EnsureSuccessStatusCode();
        var room = Interview.RoomNameFor(interview.Id);
        var recordingId = Video.RecordingFor(room)!;

        (await WebhookAsync(new VideoWebhookEvent(VideoWebhookKind.RecordingEnded, room, recordingId, false)))
            .EnsureSuccessStatusCode();

        Assert.Contains(room, Video.RoomsClosed);
        Assert.Equal(InterviewRecordingStatus.Failed, (await ReadAsync(interview.Id)).Recordings.Single().Status);
    }

    [Fact]
    public async Task An_unsigned_webhook_changes_nothing()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/webhooks/livekit", new { @event = "room_finished" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- staff and recordings ----

    private async Task<(InterviewBody Interview, Guid RecordingId, string StorageKey)> RecordedInterviewAsync()
    {
        var (family, _, interview) = await AcceptedAsync();
        await OpenTheDoorAsync(interview.Id);
        (await JoinAsync(family, interview.Id)).EnsureSuccessStatusCode();
        var room = Interview.RoomNameFor(interview.Id);
        var providerId = Video.RecordingFor(room)!;

        Video.StopRecording(room);
        (await WebhookAsync(new VideoWebhookEvent(VideoWebhookKind.RecordingEnded, room, providerId, true)))
            .EnsureSuccessStatusCode();

        var recording = (await ReadAsync(interview.Id)).Recordings.Single();
        Store.Files[recording.StorageKey!] = [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70];

        return (interview, recording.Id, recording.StorageKey!);
    }

    [Fact]
    public async Task Participants_never_get_the_recording()
    {
        var (interview, recordingId, _) = await RecordedInterviewAsync();
        var family = await factory.RegisterCareRequesterAsync();

        // The participant's own view says nothing about where a recording is.
        var response = await family.GetAsync($"/api/v1/admin/interviews/recordings/{recordingId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await (await factory.RegisterCareRequesterAsync()).GetAsync($"/api/v1/interviews/{interview.Id}");
        Assert.Equal(HttpStatusCode.NotFound, body.StatusCode);
    }

    [Fact]
    public async Task Staff_see_who_met_whom_and_every_view_is_audited()
    {
        var (interview, recordingId, _) = await RecordedInterviewAsync();
        var admin = await factory.RegisterAdminAsync();

        var listed = await admin.GetFromJsonAsync<IReadOnlyList<AdminInterviewBody>>("/api/v1/admin/interviews");
        var row = Assert.Single(listed!, i => i.InterviewId == interview.Id);
        Assert.Equal("Ada Lovelace", row.CareRequesterName);
        Assert.True(Assert.Single(row.Recordings).IsWatchable);

        var watched = await admin.GetAsync($"/api/v1/admin/interviews/recordings/{recordingId}");
        watched.EnsureSuccessStatusCode();
        Assert.Equal("video/mp4", watched.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store, private", watched.Headers.CacheControl?.ToString());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();
        Assert.Contains(await context.AuditEvents.Where(a => a.EntityId == recordingId).Select(a => a.Action).ToListAsync(),
            action => action == "RecordingViewed");
    }

    [Fact]
    public async Task Recordings_are_deleted_after_retention_unless_held()
    {
        var (held, heldRecordingId, heldKey) = await RecordedInterviewAsync();
        var (expired, _, expiredKey) = await RecordedInterviewAsync();
        var admin = await factory.RegisterAdminAsync();

        (await admin.PostAsJsonAsync($"/api/v1/admin/interviews/{held.Id}/hold", new { reason = "Open dispute." }))
            .EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();
            await context.InterviewRecordings
                .Where(r => r.InterviewId == held.Id || r.InterviewId == expired.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.DeleteAfterUtc, DateTime.UtcNow.AddDays(-1)));
        }

        await factory.Services.GetRequiredService<InterviewRecordingRetentionSweeper>().SweepAsync(CancellationToken.None);

        Assert.Contains(expiredKey, Store.Deleted);
        Assert.DoesNotContain(heldKey, Store.Deleted);
        Assert.Equal(InterviewRecordingStatus.Deleted, (await ReadAsync(expired.Id)).Recordings.Single().Status);

        var gone = await admin.GetAsync($"/api/v1/admin/interviews/recordings/{(await ReadAsync(expired.Id)).Recordings.Single().Id}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

        // Releasing the hold lets the next sweep take it.
        (await admin.PostAsync($"/api/v1/admin/interviews/{held.Id}/release", null)).EnsureSuccessStatusCode();
        await factory.Services.GetRequiredService<InterviewRecordingRetentionSweeper>().SweepAsync(CancellationToken.None);
        Assert.Contains(heldKey, Store.Deleted);
        _ = heldRecordingId;
    }

    // ---- chat ----

    [Fact]
    public async Task The_interview_shares_the_pairs_conversation()
    {
        var (family, caregiver, interview) = await AcceptedAsync();

        (await family.PostAsJsonAsync($"/api/v1/conversations/{interview.ConversationId}/messages",
            new { body = "See you at seven." })).EnsureSuccessStatusCode();

        var thread = await caregiver.GetFromJsonAsync<ConversationBody>($"/api/v1/conversations/{interview.ConversationId}");

        Assert.Contains(thread!.Messages, m => m.Body == "See you at seven." && !m.SentByMe);
    }
}
