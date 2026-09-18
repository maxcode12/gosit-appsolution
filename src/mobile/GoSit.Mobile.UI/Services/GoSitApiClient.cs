using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GoSit.Mobile.UI.Abstractions;

namespace GoSit.Mobile.UI.Services;

/// <summary>The request was refused because there is no usable session.</summary>
public sealed class SessionExpiredException() : Exception("The session has expired.");

/// <summary>The API refused the request and said why.</summary>
public sealed class ApiException(HttpStatusCode status, string message)
    : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

/// <summary>
/// Talks to the GoSit API on behalf of the signed-in account.
///
/// The one thing this does that a plain HttpClient does not: when a request comes back
/// 401 it spends the refresh token once and retries, so a fifteen-minute access token
/// never surfaces to the person using the app. On a phone that matters more than on the
/// web — the app sits in the background for hours and is opened at a doorstep, which is
/// precisely when being bounced to a login screen is most costly.
///
/// A refresh that fails clears the session rather than retrying. The API rotates refresh
/// tokens and treats a replayed one as theft, so a client that retries a failed refresh
/// would revoke every session the account has.
/// </summary>
public sealed class GoSitApiClient(HttpClient http, SessionStore session)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Guards the refresh so a burst of 401s spends the token once.</summary>
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Get, path, null, cancellationToken);

    public Task<T?> PostAsync<T>(
        string path,
        object? body = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Post, path, body, cancellationToken);

    public Task<T?> PutAsync<T>(
        string path,
        object? body = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Put, path, body, cancellationToken);

    private async Task<T?> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        var response = await SendOnceAsync(method, path, body, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();

            if (!await TryRefreshAsync(cancellationToken))
            {
                await session.ClearAsync(cancellationToken);
                throw new SessionExpiredException();
            }

            response = await SendOnceAsync(method, path, body, cancellationToken);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await session.ClearAsync(cancellationToken);
                throw new SessionExpiredException();
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException(response.StatusCode, await ProblemTitleAsync(response));
            }

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        if (await session.GetAccessTokenAsync(cancellationToken) is { } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await http.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Exchanges the refresh token for a new pair. Single-flight: several screens loading
    /// at once would otherwise each present the same token, and the API treats the second
    /// presentation as a replay and revokes everything.
    /// </summary>
    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken);

        try
        {
            if (await session.GetRefreshTokenAsync(cancellationToken) is not { } refreshToken)
            {
                return false;
            }

            using var response = await http.PostAsJsonAsync(
                "/api/v1/auth/refresh", new { refreshToken }, Json, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var tokens = await response.Content.ReadFromJsonAsync<AuthTokens>(Json, cancellationToken);

            if (tokens is null)
            {
                return false;
            }

            await session.SaveAsync(tokens, cancellationToken);
            return true;
        }
        catch (HttpRequestException)
        {
            // Offline. Not a reason to throw the session away — the token may be fine.
            return false;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>
    /// The API's own message where it gave one. Its problem titles are written to be
    /// read by a person, so passing them through beats inventing a generic sentence.
    /// </summary>
    private static async Task<string> ProblemTitleAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json);

            if (!string.IsNullOrWhiteSpace(problem?.Title))
            {
                return problem.Title;
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or HttpRequestException)
        {
            // No problem body. Fall through.
        }

        return "Something went wrong. Please try again.";
    }

    private sealed record ProblemDetails(string? Title);
}
