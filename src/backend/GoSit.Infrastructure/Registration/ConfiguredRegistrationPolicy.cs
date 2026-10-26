using GoSit.Application.Abstractions;
using GoSit.Domain.Users;
using Microsoft.Extensions.Options;

namespace GoSit.Infrastructure.Registration;

public sealed class RegistrationOptions
{
    public const string SectionName = "Registration";

    /// <summary>
    /// Closed unless explicitly opened. Development settings open it, so local runs and the
    /// test suite are unaffected; a deployment that forgets to configure this stays closed
    /// rather than quietly open.
    /// </summary>
    public bool Open { get; set; }

    /// <summary>
    /// Addresses allowed to register while sign-up is closed, separated by commas,
    /// semicolons or spaces. A single string rather than an array so it can be set from one
    /// environment variable.
    /// </summary>
    public string InvitedEmails { get; set; } = string.Empty;
}

public sealed class ConfiguredRegistrationPolicy : IRegistrationPolicy
{
    private readonly bool _open;
    private readonly HashSet<string> _invited;

    public ConfiguredRegistrationPolicy(IOptions<RegistrationOptions> options)
    {
        _open = options.Value.Open;

        _invited = options.Value.InvitedEmails
            .Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(User.Normalize)
            .ToHashSet(StringComparer.Ordinal);
    }

    public bool MayRegister(string normalizedEmail) => _open || _invited.Contains(normalizedEmail);
}
