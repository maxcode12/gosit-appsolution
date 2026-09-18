namespace GoSit.Domain.Common;

/// <summary>
/// Raised when an operation would violate a domain invariant.
/// Messages are safe to surface to clients — never put personal data in them.
/// </summary>
public class DomainException(string message) : Exception(message);
