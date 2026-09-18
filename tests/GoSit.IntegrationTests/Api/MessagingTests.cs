using System.Net;
using System.Net.Http.Json;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

[Collection(nameof(ApiCollection))]
public class MessagingTests(GoSitApiFactory factory)
{
    private sealed record MessageBody(
        Guid Id, Guid SenderProfileId, bool SentByMe, string Body, DateTime SentAtUtc);

    private sealed record ConversationSummaryBody(
        Guid ConversationId,
        Guid OtherParticipantProfileId,
        string OtherParticipantFirstName,
        string OtherParticipantLastInitial,
        string? LastMessagePreview,
        DateTime? LastMessageAtUtc,
        int UnreadCount);

    private sealed record ConversationDetailBody(
        Guid ConversationId,
        Guid OtherParticipantProfileId,
        string OtherParticipantFirstName,
        string OtherParticipantLastInitial,
        IReadOnlyList<MessageBody> Messages,
        bool HasMore);

    private async Task<HttpClient> RequesterAsync()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();
        return client;
    }

    private static async Task<MessageBody> SendAsync(
        HttpClient requester, Guid caregiverProfileId, string body)
    {
        var response = await requester.PostAsJsonAsync("/api/v1/conversations", new
        {
            caregiverProfileId,
            body,
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MessageBody>())!;
    }

    [Fact]
    public async Task A_requester_can_start_a_conversation_with_a_caregiver()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();

        var message = await SendAsync(requester, caregiverId, "Are you free Friday?");

        Assert.Equal("Are you free Friday?", message.Body);
        Assert.True(message.SentByMe);
    }

    [Fact]
    public async Task Messaging_the_same_caregiver_twice_reuses_one_thread()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();

        await SendAsync(requester, caregiverId, "First");
        await SendAsync(requester, caregiverId, "Second");

        var inbox = await requester.GetFromJsonAsync<List<ConversationSummaryBody>>(
            "/api/v1/conversations");

        // One thread per pair, so history stays in one place across repeat bookings.
        var thread = Assert.Single(inbox ?? []);
        Assert.Equal("Second", thread.LastMessagePreview);
    }

    [Fact]
    public async Task A_caregiver_sees_the_thread_and_can_reply()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();
        await SendAsync(requester, caregiverId, "Are you free Friday?");

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var inbox = await caregiver.GetFromJsonAsync<List<ConversationSummaryBody>>(
            "/api/v1/conversations");

        var thread = Assert.Single(inbox ?? []);
        Assert.Equal(1, thread.UnreadCount);

        var reply = await caregiver.PostAsJsonAsync(
            $"/api/v1/conversations/{thread.ConversationId}/messages",
            new { body = "Yes, I am." });

        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
    }

    /// <summary>
    /// A caregiver cannot open a thread with a family that has not contacted them, so
    /// the marketplace never becomes a channel for unsolicited approaches.
    /// </summary>
    [Fact]
    public async Task A_caregiver_cannot_start_a_conversation()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var response = await caregiver.PostAsJsonAsync("/api/v1/conversations", new
        {
            caregiverProfileId = caregiverId,
            body = "Hello, would you like care?",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_unverified_caregiver_cannot_be_messaged()
    {
        var caregiver = await factory.RegisterCaregiverAsync();
        var created = await caregiver.PutAsJsonAsync("/api/v1/caregivers/me", new
        {
            bio = "Ready.",
            yearsOfExperience = 2,
            hourlyRate = 22m,
            address = new
            {
                line1 = "1 Yonge St",
                line2 = (string?)null,
                city = "Toronto",
                province = "ON",
                postalCode = "M5E 1E5",
                country = "CA",
            },
            serviceRadiusKm = 20,
        });
        var unverifiedId = (await created.Content.ReadFromJsonAsync<SearchTestFixtures.CaregiverBody>())!.Id;

        var requester = await RequesterAsync();

        var response = await requester.PostAsJsonAsync("/api/v1/conversations", new
        {
            caregiverProfileId = unverifiedId,
            body = "Hello",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The central privacy rule for messaging: a private thread must be unreadable by
    /// anyone outside it, and the response must not confirm the id exists.
    /// </summary>
    [Fact]
    public async Task An_outsider_cannot_read_or_write_a_conversation()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var owner = await RequesterAsync();
        await SendAsync(owner, caregiverId, "This is private.");

        var inbox = await owner.GetFromJsonAsync<List<ConversationSummaryBody>>("/api/v1/conversations");
        var conversationId = inbox!.Single().ConversationId;

        var stranger = await RequesterAsync();

        var read = await stranger.GetAsync($"/api/v1/conversations/{conversationId}");
        var write = await stranger.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages", new { body = "Intruding." });
        var markRead = await stranger.PostAsync($"/api/v1/conversations/{conversationId}/read", null);

        // 404 rather than 403 — a 403 would confirm the conversation exists.
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, markRead.StatusCode);

        Assert.DoesNotContain("This is private.", await read.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_unrelated_caregiver_cannot_read_a_conversation()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var otherCaregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();
        await SendAsync(requester, caregiverId, "This is private.");

        var inbox = await requester.GetFromJsonAsync<List<ConversationSummaryBody>>("/api/v1/conversations");
        var conversationId = inbox!.Single().ConversationId;

        var stranger = await factory.CaregiverClientForAsync(otherCaregiverId);
        var response = await stranger.GetAsync($"/api/v1/conversations/{conversationId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_inbox_only_shows_the_callers_own_conversations()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var owner = await RequesterAsync();
        await SendAsync(owner, caregiverId, "Mine.");

        var stranger = await RequesterAsync();

        Assert.Empty(await stranger.GetFromJsonAsync<List<ConversationSummaryBody>>(
            "/api/v1/conversations") ?? []);
    }

    /// <summary>
    /// Messaging shows a first name and last initial, never an email or phone number.
    /// </summary>
    [Fact]
    public async Task Conversations_never_expose_contact_details()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();
        await SendAsync(requester, caregiverId, "Hello");

        var inboxBody = await requester.GetStringAsync("/api/v1/conversations");

        Assert.DoesNotContain("@example.com", inboxBody);
        Assert.DoesNotContain("+1416", inboxBody);
        Assert.DoesNotContain("Bloor", inboxBody);

        var inbox = await requester.GetFromJsonAsync<List<ConversationSummaryBody>>(
            "/api/v1/conversations");
        var thread = inbox!.Single();
        Assert.Equal("Ada", thread.OtherParticipantFirstName);
        Assert.Equal("L", thread.OtherParticipantLastInitial);
    }

    [Fact]
    public async Task Reading_a_thread_clears_its_unread_count()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();
        await SendAsync(requester, caregiverId, "Are you free Friday?");

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var before = await caregiver.GetFromJsonAsync<List<ConversationSummaryBody>>(
            "/api/v1/conversations");
        var conversationId = before!.Single().ConversationId;
        Assert.Equal(1, before.Single().UnreadCount);

        await caregiver.GetFromJsonAsync<ConversationDetailBody>(
            $"/api/v1/conversations/{conversationId}");

        var after = await caregiver.GetFromJsonAsync<List<ConversationSummaryBody>>(
            "/api/v1/conversations");
        Assert.Equal(0, after!.Single().UnreadCount);
    }

    [Fact]
    public async Task Messages_are_returned_in_order_and_marked_by_sender()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();
        await SendAsync(requester, caregiverId, "First");

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var inbox = await caregiver.GetFromJsonAsync<List<ConversationSummaryBody>>(
            "/api/v1/conversations");
        var conversationId = inbox!.Single().ConversationId;

        await caregiver.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages", new { body = "Second" });

        var detail = await caregiver.GetFromJsonAsync<ConversationDetailBody>(
            $"/api/v1/conversations/{conversationId}");

        Assert.Equal(["First", "Second"], detail!.Messages.Select(m => m.Body));
        Assert.False(detail.Messages[0].SentByMe);
        Assert.True(detail.Messages[1].SentByMe);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_message_is_rejected(string body)
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();

        var response = await requester.PostAsJsonAsync("/api/v1/conversations", new
        {
            caregiverProfileId = caregiverId,
            body,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Messaging_requires_authentication()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/conversations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Sharing contact details is flagged for review but still delivered — moderation,
    /// not censorship.
    /// </summary>
    [Fact]
    public async Task A_message_sharing_contact_details_is_delivered_and_flagged()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();

        var message = await SendAsync(requester, caregiverId, "Call me on 416-555-0142");

        Assert.Equal("Call me on 416-555-0142", message.Body);
        Assert.True(await factory.IsMessageFlaggedAsync(message.Id));
    }

    [Fact]
    public async Task An_ordinary_message_is_not_flagged()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();

        var message = await SendAsync(requester, caregiverId, "Are you free Friday at 6pm?");

        Assert.False(await factory.IsMessageFlaggedAsync(message.Id));
    }
}
