using System.Net;
using System.Net.Http.Json;
using GoSit.Domain.TrustAndSafety;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Staff review of messages flagged as possible contact sharing.
///
/// `ContactDetailPolicy` flagged messages from Phase 5 and the schema carried a partial
/// index built for a moderation queue, and nothing ever read the flag. The policy reasons
/// that it can afford to be loose because "a false positive costs a review" — there was no
/// review, so it cost nothing either way.
///
/// This is the one staff surface that reads private messages, so most of what is pinned
/// here is about what it does *not* show.
/// </summary>
[Collection(nameof(ApiCollection))]
public class MessageModerationTests(GoSitApiFactory factory)
{
    private const string ContactSharing =
        "Easier to sort this directly — call me on 416 555 0134 tomorrow.";

    private sealed record FlaggedRow(
        Guid MessageId,
        Guid ConversationId,
        string SenderName,
        string RecipientName,
        bool SentByCaregiver,
        DateTime SentAtUtc);

    private sealed record FlaggedDetail(
        Guid MessageId, string SenderName, string RecipientName, string Body);

    /// <summary>
    /// The response to starting a conversation is the message that started it, so its
    /// id is the message's — not the conversation's.
    /// </summary>
    private sealed record SentMessage(Guid Id);

    /// <summary>
    /// A care requester sending a caregiver something that trips the policy. Returns the
    /// message id.
    /// </summary>
    private async Task<Guid> FlaggedMessageAsync(string body = ContactSharing)
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();

        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();

        var response = await requester.PostAsJsonAsync(
            "/api/v1/conversations", new { caregiverProfileId = caregiverId, body });

        response.EnsureSuccessStatusCode();

        var sent = await response.Content.ReadFromJsonAsync<SentMessage>();

        return sent!.Id;
    }

    private static Task<IReadOnlyList<FlaggedRow>?> QueueAsync(HttpClient admin) =>
        admin.GetFromJsonAsync<IReadOnlyList<FlaggedRow>>("/api/v1/admin/flagged-messages?take=200");

    private async Task<FlaggedRow> QueuedRowAsync(HttpClient admin, Guid messageId)
    {
        var queue = await QueueAsync(admin);
        return Assert.Single(queue!, r => r.MessageId == messageId);
    }

    // ---- the queue ----

    /// <summary>The gap this closes: a flag that appeared on no staff screen at all.</summary>
    [Fact]
    public async Task A_flagged_message_appears_in_the_staff_queue()
    {
        var messageId = await FlaggedMessageAsync();
        var admin = await factory.RegisterAdminAsync();

        var row = await QueuedRowAsync(admin, messageId);

        // Both sides named, and the direction, so staff know who they would be reading.
        Assert.False(string.IsNullOrWhiteSpace(row.SenderName));
        Assert.False(string.IsNullOrWhiteSpace(row.RecipientName));
        Assert.False(row.SentByCaregiver);
    }

    /// <summary>
    /// The rule the whole design rests on. A queue that rendered every flagged body would
    /// make browsing strangers' private conversations the default way to use this screen.
    /// </summary>
    [Fact]
    public async Task The_queue_carries_no_message_bodies()
    {
        var messageId = await FlaggedMessageAsync();
        var admin = await factory.RegisterAdminAsync();

        // Asserted against the raw payload, not a typed model — a body cannot be absent
        // from a record that never declared it, so deserializing would prove nothing.
        var payload = await admin.GetStringAsync("/api/v1/admin/flagged-messages?take=200");

        Assert.Contains(messageId.ToString(), payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("416 555 0134", payload);
        Assert.DoesNotContain("Easier to sort this", payload);
    }

    [Fact]
    public async Task An_ordinary_message_is_never_flagged()
    {
        var messageId = await FlaggedMessageAsync("Are you free on Friday afternoon?");
        var admin = await factory.RegisterAdminAsync();

        var queue = await QueueAsync(admin);

        Assert.DoesNotContain(queue!, r => r.MessageId == messageId);
    }

    [Fact]
    public async Task The_queue_is_staff_only()
    {
        await FlaggedMessageAsync();

        var requester = await factory.RegisterCareRequesterAsync();

        var response = await requester.GetAsync("/api/v1/admin/flagged-messages");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- reading one ----

    [Fact]
    public async Task Reading_one_returns_the_message()
    {
        var messageId = await FlaggedMessageAsync();
        var admin = await factory.RegisterAdminAsync();
        var row = await QueuedRowAsync(admin, messageId);

        var detail = await admin.GetFromJsonAsync<FlaggedDetail>(
            $"/api/v1/admin/flagged-messages/{row.MessageId}");

        Assert.Equal(ContactSharing, detail!.Body);
    }

    /// <summary>
    /// The safeguard that makes the queue accountable rather than a window. Reading a
    /// private message is an act, and who read whose message is recorded.
    /// </summary>
    [Fact]
    public async Task Reading_one_is_audited()
    {
        var messageId = await FlaggedMessageAsync();
        var admin = await factory.RegisterAdminAsync();
        var row = await QueuedRowAsync(admin, messageId);

        (await admin.GetAsync($"/api/v1/admin/flagged-messages/{row.MessageId}"))
            .EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var audited = await context.AuditEvents
            .Where(a => a.EntityId == row.MessageId && a.Action == "FlaggedMessageRead")
            .Select(a => a.ActorUserId)
            .ToListAsync();

        var actor = Assert.Single(audited);
        Assert.NotNull(actor);
    }

    [Fact]
    public async Task A_message_that_was_never_flagged_cannot_be_read()
    {
        var messageId = await FlaggedMessageAsync("Are you free on Friday afternoon?");

        var admin = await factory.RegisterAdminAsync();

        var response = await admin.GetAsync($"/api/v1/admin/flagged-messages/{messageId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- settling it ----

    [Fact]
    public async Task Dismissing_a_flag_clears_it_from_the_queue()
    {
        var messageId = await FlaggedMessageAsync();
        var admin = await factory.RegisterAdminAsync();
        var row = await QueuedRowAsync(admin, messageId);

        (await admin.PostAsJsonAsync(
            $"/api/v1/admin/flagged-messages/{row.MessageId}/moderate",
            new { outcome = "NoConcern", notes = (string?)null }))
            .EnsureSuccessStatusCode();

        var queue = await QueueAsync(admin);

        Assert.DoesNotContain(queue!, r => r.MessageId == row.MessageId);
    }

    /// <summary>
    /// An escalation lands in the incident queue, which already has assignment,
    /// resolution and an audit trail, rather than in a second place where decisions about
    /// someone's account get made.
    /// </summary>
    [Fact]
    public async Task Escalating_raises_an_incident_about_the_sender()
    {
        var messageId = await FlaggedMessageAsync();
        var admin = await factory.RegisterAdminAsync();
        var row = await QueuedRowAsync(admin, messageId);

        const string notes = "Third time this account has pushed to move off-platform.";

        (await admin.PostAsJsonAsync(
            $"/api/v1/admin/flagged-messages/{row.MessageId}/moderate",
            new { outcome = "Escalated", notes }))
            .EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var incident = await context.IncidentReports
            .Where(i => i.Description == notes)
            .SingleAsync();

        Assert.Equal(IncidentCategory.InappropriateConduct, incident.Category);

        // The staff member's account of it, never the message itself: an incident is read
        // by more people and lives longer than this review.
        Assert.DoesNotContain("416 555 0134", incident.Description);
    }

    [Fact]
    public async Task Escalating_without_a_reason_is_refused()
    {
        var messageId = await FlaggedMessageAsync();
        var admin = await factory.RegisterAdminAsync();
        var row = await QueuedRowAsync(admin, messageId);

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/admin/flagged-messages/{row.MessageId}/moderate",
            new { outcome = "Escalated", notes = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Two staff members reaching opposite conclusions is a real event, and silently
    /// keeping the first would hide it.
    /// </summary>
    [Fact]
    public async Task A_message_cannot_be_reviewed_twice()
    {
        var messageId = await FlaggedMessageAsync();
        var admin = await factory.RegisterAdminAsync();
        var row = await QueuedRowAsync(admin, messageId);

        var body = new { outcome = "NoConcern", notes = (string?)null };

        (await admin.PostAsJsonAsync(
            $"/api/v1/admin/flagged-messages/{row.MessageId}/moderate", body))
            .EnsureSuccessStatusCode();

        var second = await admin.PostAsJsonAsync(
            $"/api/v1/admin/flagged-messages/{row.MessageId}/moderate", body);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    /// <summary>
    /// Moderating never edits or withdraws the message. Whether it looked like contact
    /// sharing is a fact about it; whether that mattered is a judgement, and the two are
    /// kept apart.
    /// </summary>
    [Fact]
    public async Task Moderating_leaves_the_message_where_the_participants_can_see_it()
    {
        var messageId = await FlaggedMessageAsync();
        var admin = await factory.RegisterAdminAsync();
        var row = await QueuedRowAsync(admin, messageId);

        (await admin.PostAsJsonAsync(
            $"/api/v1/admin/flagged-messages/{row.MessageId}/moderate",
            new { outcome = "Escalated", notes = "Pushing to move off-platform." }))
            .EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var message = await context.Messages.SingleAsync(m => m.Id == row.MessageId);

        Assert.Equal(ContactSharing, message.Body);
        Assert.True(message.FlaggedForReview);
    }
}
