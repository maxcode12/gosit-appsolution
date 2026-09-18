namespace GoSit.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Verifies a password against a stored hash. <paramref name="rehashNeeded"/> is set
    /// when the stored hash uses outdated parameters and should be upgraded on next login.
    /// </summary>
    bool Verify(string password, string passwordHash, out bool rehashNeeded);
}
