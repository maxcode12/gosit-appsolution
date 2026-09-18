using System.Text.RegularExpressions;

namespace GoSit.Domain.Messaging;

/// <summary>
/// Flags messages that look like they are exchanging contact details.
///
/// This does not block or alter the message. Taking care off-platform is a genuine
/// trust and safety concern — it removes the audit trail, the verified identity, and
/// the payment protection — but a caregiver may also have a legitimate reason to share
/// a number. So a message is marked for review rather than censored, and Phase 7 decides
/// what moderation does with the flag.
/// </summary>
public static partial class ContactDetailPolicy
{
    public static bool LooksLikeContactSharing(string body) =>
        PhoneNumberPattern().IsMatch(body) || EmailPattern().IsMatch(body);

    /// <summary>
    /// Ten or more digits, allowing the separators people actually type. Deliberately
    /// loose: a false positive costs a review, a false negative costs the trail.
    /// </summary>
    [GeneratedRegex(@"(\+?\d[\s\-\.\(\)]*){10,}", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneNumberPattern();

    [GeneratedRegex(@"[\w\.\-\+]+\s*(@|\(at\))\s*[\w\-]+\s*(\.|\(dot\))\s*\w{2,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
