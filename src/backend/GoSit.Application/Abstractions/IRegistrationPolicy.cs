namespace GoSit.Application.Abstractions;

/// <summary>
/// Whether sign-up is open, and to whom.
///
/// Exists so launch is a deliberate act: a deployed API refuses new accounts until
/// someone turns sign-up on, instead of accepting whoever finds the address first. People
/// on an invite list can still register while it is closed, so testers and the first
/// partners do not need it opened to everyone.
/// </summary>
public interface IRegistrationPolicy
{
    /// <param name="normalizedEmail">The address as stored, already normalised.</param>
    bool MayRegister(string normalizedEmail);
}
