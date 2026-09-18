using GoSit.Application.Abstractions;
using GoSit.Domain.Notifications;
using GoSit.Infrastructure.Persistence;
using GoSit.Infrastructure.Scheduling;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>Records emails instead of sending them, and can be told to fail.</summary>
public sealed class RecordingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    /// <summary>Whether a provider is "configured". Drives the suppression path.</summary>
    public bool IsLive { get; set; } = true;

    public bool FailSends { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (FailSends)
        {
            throw new InvalidOperationException("The relay is unavailable.");
        }

        Sent.Add(message);

        return Task.CompletedTask;
    }
}

public sealed class EmailFactoryFixture : GoSitApiFactory
{
    public RecordingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.AddScoped<IEmailSender>(_ => Emails);
        });
    }
}

/// <summary>
/// Emailing notifications.
///
/// The dispatcher runs against the real database, the real aggregate and the real
/// template; only the outbound SMTP call is replaced.
/// </summary>
public class NotificationEmailTests(EmailFactoryFixture factory)
    : IClassFixture<EmailFactoryFixture>
{
    /// <summary>Creates an account and a notification waiting to be emailed.</summary>
    private async Task<(Guid UserId, Guid NotificationId)> PendingNotificationAsync(
        string title = "Priya confirmed your booking",
        NotificationType type = NotificationType.BookingAccepted)
    {
        var client = await factory.RegisterCareRequesterAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        // The most recently created account is the one just registered.
        var user = await context.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAtUtc)
            .FirstAsync();

        var notification = Notification.Create(
            user.Id, type, title, "Open GoSit for the details.",
            "CareBooking", Guid.NewGuid(), DateTime.UtcNow);

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        client.Dispose();

        return (user.Id, notification.Id);
    }

    private Task<NotificationEmailResult> DispatchAsync() =>
        factory.Services
            .GetRequiredService<NotificationEmailDispatcher>()
            .DispatchAsync(CancellationToken.None);

    private async Task<Notification> ReadAsync(Guid notificationId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        return await context.Notifications.AsNoTracking().SingleAsync(n => n.Id == notificationId);
    }

    [Fact]
    public async Task A_pending_notification_is_emailed_and_marked_sent()
    {
        var (_, notificationId) = await PendingNotificationAsync();

        factory.Emails.Sent.Clear();
        factory.Emails.IsLive = true;

        await DispatchAsync();

        var notification = await ReadAsync(notificationId);

        Assert.NotNull(notification.EmailSentAtUtc);
        Assert.False(notification.EmailSuppressed);

        var email = Assert.Single(
            factory.Emails.Sent, e => e.Subject == "Priya confirmed your booking");

        // Both bodies, always — text-only clients, spam scoring, and screen readers.
        Assert.False(string.IsNullOrWhiteSpace(email.HtmlBody));
        Assert.False(string.IsNullOrWhiteSpace(email.TextBody));
    }

    [Fact]
    public async Task An_email_is_not_sent_twice()
    {
        var (_, notificationId) = await PendingNotificationAsync();

        factory.Emails.IsLive = true;
        await DispatchAsync();

        factory.Emails.Sent.Clear();
        await DispatchAsync();

        Assert.Empty(factory.Emails.Sent);
        Assert.NotNull((await ReadAsync(notificationId)).EmailSentAtUtc);
    }

    /// <summary>
    /// The honesty rule: with no provider, the record must say nothing was sent rather
    /// than quietly claiming delivery.
    /// </summary>
    [Fact]
    public async Task With_no_provider_the_notification_is_suppressed_not_marked_sent()
    {
        var (_, notificationId) = await PendingNotificationAsync();

        factory.Emails.IsLive = false;
        factory.Emails.Sent.Clear();

        try
        {
            await DispatchAsync();
        }
        finally
        {
            factory.Emails.IsLive = true;
        }

        var notification = await ReadAsync(notificationId);

        Assert.True(notification.EmailSuppressed);
        Assert.Null(notification.EmailSentAtUtc);
        Assert.Empty(factory.Emails.Sent);
    }

    /// <summary>A relay outage leaves the notification owed, to be retried.</summary>
    [Fact]
    public async Task A_failed_send_is_retried_rather_than_lost()
    {
        var (_, notificationId) = await PendingNotificationAsync();

        factory.Emails.IsLive = true;
        factory.Emails.FailSends = true;

        try
        {
            await DispatchAsync();
        }
        finally
        {
            factory.Emails.FailSends = false;
        }

        var afterFailure = await ReadAsync(notificationId);

        Assert.Null(afterFailure.EmailSentAtUtc);
        Assert.False(afterFailure.EmailSuppressed);

        // Recorded despite the call throwing, so the backoff applies.
        Assert.Equal(1, afterFailure.EmailAttemptCount);
        Assert.NotNull(afterFailure.LastEmailAttemptAtUtc);
    }

    /// <summary>
    /// A notification title carries a name someone chose for themselves. Interpolating
    /// it into HTML unescaped would break the markup at best, and at worst let one
    /// person put markup into an email GoSit sends to another.
    /// </summary>
    [Fact]
    public async Task Titles_are_escaped_in_the_html_body()
    {
        var (_, notificationId) = await PendingNotificationAsync(
            title: "New message from <script>alert(1)</script>",
            type: NotificationType.MessageReceived);

        factory.Emails.IsLive = true;
        factory.Emails.Sent.Clear();

        await DispatchAsync();

        Assert.NotNull((await ReadAsync(notificationId)).EmailSentAtUtc);

        var email = Assert.Single(factory.Emails.Sent, e => e.Subject.Contains("script"));

        Assert.DoesNotContain("<script>", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", email.HtmlBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// A message notification links to the inbox, not to a booking. Dropping someone on
    /// the wrong screen is the difference between a useful email and a nuisance.
    /// </summary>
    [Fact]
    public async Task The_link_points_at_what_actually_happened()
    {
        await PendingNotificationAsync(
            title: "New message from Priya R.",
            type: NotificationType.MessageReceived);

        factory.Emails.IsLive = true;
        factory.Emails.Sent.Clear();

        await DispatchAsync();

        var email = Assert.Single(
            factory.Emails.Sent, e => e.Subject == "New message from Priya R.");

        Assert.Contains("/messages", email.TextBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// Notification bodies are written to be safe on a lock screen, and the email adds
    /// nothing to them. This guards the boundary rather than the wording.
    /// </summary>
    [Fact]
    public async Task The_email_carries_no_more_than_the_notification_did()
    {
        var (_, notificationId) = await PendingNotificationAsync();

        factory.Emails.IsLive = true;
        factory.Emails.Sent.Clear();

        await DispatchAsync();

        var notification = await ReadAsync(notificationId);
        var email = Assert.Single(factory.Emails.Sent, e => e.Subject == notification.Title);

        // Everything substantive in the email came from the notification itself.
        Assert.Contains(notification.Title, email.TextBody, StringComparison.Ordinal);
        Assert.Contains(notification.Body!, email.TextBody, StringComparison.Ordinal);
    }
}
