namespace GoSit.Mobile.UI.Services;

/// <summary>An interview as either side of it needs it.</summary>
public sealed record MobileInterview(
    Guid Id,
    string Status,
    Guid CareRequesterProfileId,
    Guid CaregiverProfileId,
    Guid ConversationId,
    string OtherParticipantFirstName,
    string? OtherParticipantLastInitial,
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

/// <summary>What a joined call needs: LiveKit's own server and a token good for it alone.</summary>
public sealed record MobileJoinTicket(
    Guid InterviewId, Guid ConversationId, string ServerUrl, string Token, DateTime ExpiresAtUtc);

/// <summary>
/// Video interviews, for whichever side of the marketplace is asking.
///
/// One service for both a caregiver and a care requester because the API already draws
/// that line — <c>GET /interviews</c> answers from the caller's own identity, never a
/// role parameter this client could get wrong.
/// </summary>
public sealed class InterviewService(GoSitApiClient api)
{
    public async Task<IReadOnlyList<MobileInterview>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await api.GetAsync<List<MobileInterview>>("/api/v1/interviews", cancellationToken) ?? [];

    public Task<MobileInterview?> GetAsync(Guid interviewId, CancellationToken cancellationToken = default) =>
        api.GetAsync<MobileInterview>($"/api/v1/interviews/{interviewId}", cancellationToken);

    public Task<MobileInterview?> AcceptAsync(Guid interviewId, CancellationToken cancellationToken = default) =>
        api.PostAsync<MobileInterview>($"/api/v1/interviews/{interviewId}/accept", null, cancellationToken);

    public Task<MobileInterview?> DeclineAsync(Guid interviewId, CancellationToken cancellationToken = default) =>
        api.PostAsync<MobileInterview>($"/api/v1/interviews/{interviewId}/decline", null, cancellationToken);

    public Task<MobileInterview?> CancelAsync(Guid interviewId, CancellationToken cancellationToken = default) =>
        api.PostAsync<MobileInterview>($"/api/v1/interviews/{interviewId}/cancel", null, cancellationToken);

    /// <summary>
    /// Joins the call. <paramref name="consentToRecording"/> is not a saved preference —
    /// the API asks for it on every call, and a caregiver or family who declines gets a
    /// clear refusal rather than a token, matching the rule that a call whose recording
    /// cannot start does not go ahead.
    /// </summary>
    public Task<MobileJoinTicket?> JoinAsync(
        Guid interviewId, bool consentToRecording, CancellationToken cancellationToken = default) =>
        api.PostAsync<MobileJoinTicket>(
            $"/api/v1/interviews/{interviewId}/join",
            new { consentToRecording },
            cancellationToken);
}
