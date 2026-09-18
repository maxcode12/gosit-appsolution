namespace GoSit.Application.Abstractions;

/// <summary>The authenticated caller, resolved from the access token.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }
}
