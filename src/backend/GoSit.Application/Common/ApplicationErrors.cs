namespace GoSit.Application.Common;

/// <summary>Request failed input validation. Maps to 400 with per-field detail.</summary>
public sealed class RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>
/// Credentials were rejected, or the account may not authenticate. Maps to 401.
/// The message is deliberately identical for every cause so the endpoint cannot be
/// used to discover which email addresses are registered.
/// </summary>
public sealed class AuthenticationFailedException()
    : Exception("The email address or password is incorrect.");

/// <summary>The caller is authenticated but not permitted. Maps to 403.</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>The request conflicts with existing state. Maps to 409.</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>The requested resource does not exist, or is not visible to the caller. Maps to 404.</summary>
public sealed class NotFoundException(string message) : Exception(message);
