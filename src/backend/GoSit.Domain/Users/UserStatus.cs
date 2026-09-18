namespace GoSit.Domain.Users;

public enum UserStatus
{
    Active = 1,

    /// <summary>Suspended by Trust and Safety. Cannot authenticate.</summary>
    Suspended = 2,

    /// <summary>Closed by the user. Cannot authenticate.</summary>
    Deactivated = 3,
}
