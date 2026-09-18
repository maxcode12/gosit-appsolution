namespace GoSit.Domain.Users;

/// <summary>
/// Account roles. A CareRequester arranges care; a Caregiver provides it.
/// "Parent" is a wireframe label, not a domain concept — a parent is one kind of
/// CareRequester, alongside adult children arranging senior care and support workers.
/// </summary>
public enum UserRole
{
    CareRequester = 1,
    Caregiver = 2,
    Admin = 3,
}
