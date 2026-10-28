using GoSit.Domain.Common;
using GoSit.Domain.Users.Events;

namespace GoSit.Domain.Users;

/// <summary>
/// Account aggregate. Owns credentials, role, account state, and the set of
/// issued refresh tokens. All state changes go through the methods here — callers
/// must never assign properties directly.
/// </summary>
public sealed class User : Entity
{
    private readonly List<RefreshToken> _refreshTokens = [];

    private User()
    {
    }

    public string Email { get; private set; } = string.Empty;

    /// <summary>Upper-invariant form of <see cref="Email"/>, used for uniqueness and lookup.</summary>
    public string NormalizedEmail { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public UserStatus Status { get; private set; }

    public bool EmailVerified { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? LastLoginAtUtc { get; private set; }

    /// <summary>
    /// This account's own code for inviting others. Random rather than derived from
    /// anything about the account, so it cannot be guessed from an email address and
    /// sharing it reveals nothing.
    /// </summary>
    public string? ReferralCode { get; private set; }

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    public static string Normalize(string email) => email.Trim().ToUpperInvariant();

    public static User Register(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        UserRole role,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("An email address is required.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("A password hash is required.");
        }

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            throw new DomainException("A first and last name are required.");
        }

        // Admin accounts are provisioned internally, never through public registration.
        if (role is not (UserRole.CareRequester or UserRole.Caregiver))
        {
            throw new DomainException("Only care-requester and caregiver accounts can be self-registered.");
        }

        var user = new User
        {
            Email = email.Trim(),
            NormalizedEmail = Normalize(email),
            PasswordHash = passwordHash,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Role = role,
            Status = UserStatus.Active,
            EmailVerified = false,
            CreatedAtUtc = utcNow,
        };

        user.RaiseDomainEvent(new UserRegisteredDomainEvent(user.Id, role, utcNow));
        return user;
    }

    /// <summary>
    /// Google's permanent identifier for this person (the <c>sub</c> claim), set when the
    /// account is created through or linked to Google sign-in. Matched in preference to
    /// the email address, because an email can change hands and a subject does not.
    /// </summary>
    public string? GoogleSubject { get; private set; }

    /// <summary>True when this account has a password it can sign in with.</summary>
    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);

    /// <summary>
    /// Creates an account for someone Google has already identified.
    /// </summary>
    /// <remarks>
    /// No password: an empty hash can never verify, so the account signs in through
    /// Google until the person sets one through the reset flow. The email is marked
    /// verified because the caller only gets here for an address Google reports as
    /// verified. Like <see cref="Register"/>, this cannot create staff accounts.
    /// </remarks>
    public static User RegisterWithGoogle(
        string googleSubject,
        string email,
        string firstName,
        string lastName,
        UserRole role,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(googleSubject))
        {
            throw new DomainException("A Google account identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("An email address is required.");
        }

        if (role is not (UserRole.CareRequester or UserRole.Caregiver))
        {
            throw new DomainException("Only care-requester and caregiver accounts can be self-registered.");
        }

        var user = new User
        {
            Email = email.Trim(),
            NormalizedEmail = Normalize(email),
            PasswordHash = string.Empty,
            GoogleSubject = googleSubject,
            // Google may return no family name; the profile screens collect a real one.
            FirstName = string.IsNullOrWhiteSpace(firstName) ? email.Trim().Split('@')[0] : firstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(lastName) ? "-" : lastName.Trim(),
            Role = role,
            Status = UserStatus.Active,
            EmailVerified = true,
            CreatedAtUtc = utcNow,
        };

        user.RaiseDomainEvent(new UserRegisteredDomainEvent(user.Id, role, utcNow));
        return user;
    }

    /// <summary>
    /// Ties an existing account to a Google identity. Refused if it is already tied to a
    /// different one: repointing would let whoever controls the new Google account in.
    /// Google only vouches for the email, so the address is treated as verified.
    /// </summary>
    public void LinkGoogle(string googleSubject)
    {
        if (string.IsNullOrWhiteSpace(googleSubject))
        {
            throw new DomainException("A Google account identifier is required.");
        }

        if (GoogleSubject is not null && GoogleSubject != googleSubject)
        {
            throw new DomainException("This account is already linked to a different Google account.");
        }

        GoogleSubject = googleSubject;
        EmailVerified = true;
    }

    /// <summary>
    /// Provisions a staff account.
    /// </summary>
    /// <remarks>
    /// Deliberately a separate factory rather than a role argument on
    /// <see cref="Register"/>. Self-registration must never be able to reach this path,
    /// and the surest way to guarantee that is for the two to be different methods —
    /// a future edit to the registration flow cannot widen into admin creation by
    /// accident.
    ///
    /// The email is treated as already verified: a staff account is created by someone
    /// who already controls the mailbox, and there is no self-service flow to confirm it
    /// through.
    /// </remarks>
    public static User ProvisionAdministrator(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("An email address is required.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("A password hash is required.");
        }

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            throw new DomainException("A first and last name are required.");
        }

        var user = new User
        {
            Email = email.Trim(),
            NormalizedEmail = Normalize(email),
            PasswordHash = passwordHash,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Role = UserRole.Admin,
            Status = UserStatus.Active,
            EmailVerified = true,
            CreatedAtUtc = utcNow,
        };

        user.RaiseDomainEvent(new UserRegisteredDomainEvent(user.Id, UserRole.Admin, utcNow));
        return user;
    }

    /// <summary>
    /// Whether this account is permitted to obtain tokens. Suspended and deactivated
    /// accounts are refused even when they present correct credentials.
    /// </summary>
    public bool CanAuthenticate => Status == UserStatus.Active;

    /// <summary>
    /// The billing provider's customer reference — a Stripe Customer id.
    ///
    /// Stored so an account keeps one customer across subscriptions rather than
    /// accumulating a new one each time it subscribes, which would scatter its payment
    /// methods and billing history.
    /// </summary>
    public string? ProviderCustomerId { get; private set; }

    /// <summary>
    /// Records the provider customer created for this account. Set once and never
    /// changed: repointing a live account at a different customer would orphan its
    /// payment methods and its invoice history.
    /// </summary>
    public void LinkProviderCustomer(string providerCustomerId)
    {
        if (string.IsNullOrWhiteSpace(providerCustomerId))
        {
            throw new DomainException("A provider customer id is required.");
        }

        if (!string.IsNullOrWhiteSpace(ProviderCustomerId)
            && ProviderCustomerId != providerCustomerId)
        {
            throw new DomainException("This account is already linked to a billing customer.");
        }

        ProviderCustomerId = providerCustomerId;
    }

    /// <summary>
    /// Consecutive failed sign-ins since the last success. Reset, not decayed — someone
    /// who gets in has demonstrably not been guessing.
    /// </summary>
    public int FailedLoginAttempts { get; private set; }

    /// <summary>When this account stops refusing correct credentials. Null when open.</summary>
    public DateTime? LockedOutUntilUtc { get; private set; }

    /*
     * Per-account lockout.
     *
     * The API already rate-limits credential endpoints per IP, which stops one machine
     * hammering the login. It does nothing about the attack that matters here: a
     * distributed guess at one known email, a few tries from each of a thousand
     * addresses, indefinitely. What is behind a GoSit account — a family's home address,
     * a child's routine, a disabled adult's care plan — makes that worth stopping.
     *
     * Temporary rather than permanent, and this is the load-bearing decision. A lockout
     * anyone can trigger by typing the wrong password is also a way to lock a caregiver
     * out of the app an hour before their shift, or to lock a family out on the evening
     * they need to reach one. Permanent lockout would hand that lever to the attacker.
     * The window escalates so that guessing gets expensive, and it always expires.
     *
     * OPEN BUSINESS DECISION: the numbers below are engineering defaults. They trade a
     * legitimate person's inconvenience against an attacker's throughput, and where that
     * line sits is a product and security call.
     */

    /// <summary>Failures tolerated before the account starts refusing.</summary>
    public const int MaximumFailedAttempts = 10;

    /// <summary>
    /// How long each successive lockout lasts: a minute, then five, and so on to an hour.
    ///
    /// The first is deliberately trivial. Ten wrong passwords is far more often someone
    /// with an old password in their manager than an attack, and the cost of the first
    /// lockout should land accordingly — while still cutting a distributed guess from
    /// thousands of attempts an hour to a handful.
    /// </summary>
    private static readonly TimeSpan[] LockoutBackoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
    ];

    public bool IsLockedOutAt(DateTime utcNow) =>
        LockedOutUntilUtc is { } until && utcNow < until;

    /// <summary>
    /// Records a wrong password, locking the account once the tolerance is spent.
    ///
    /// The count keeps climbing past the threshold rather than resetting, which is what
    /// makes the backoff escalate: an attacker who waits out a one-minute lockout and
    /// resumes finds the next one is five.
    /// </summary>
    public void RecordFailedLogin(DateTime utcNow)
    {
        FailedLoginAttempts++;

        if (FailedLoginAttempts < MaximumFailedAttempts)
        {
            return;
        }

        var step = (FailedLoginAttempts - MaximumFailedAttempts) / MaximumFailedAttempts;
        var window = LockoutBackoff[Math.Min(step, LockoutBackoff.Length - 1)];

        LockedOutUntilUtc = utcNow + window;
    }

    public void RecordSuccessfulLogin(DateTime utcNow)
    {
        LastLoginAtUtc = utcNow;
        ClearLockout();
    }

    /// <summary>
    /// Lets an account back in without waiting.
    ///
    /// Called after a password reset, because proving control of the email is stronger
    /// evidence than the password ever was — and because someone locked out is told
    /// nothing about why, so the reset is the only route back they can find on their own.
    /// </summary>
    public void ClearLockout()
    {
        FailedLoginAttempts = 0;
        LockedOutUntilUtc = null;
    }

    public void VerifyEmail() => EmailVerified = true;

    /// <summary>
    /// Assigns this account's referral code the first time it is needed. Generated
    /// lazily so existing accounts pick one up without a backfill, and idempotent so a
    /// code never changes once someone has shared it.
    /// </summary>
    public string AssignReferralCode(Func<string> generate)
    {
        ReferralCode ??= generate();
        return ReferralCode;
    }

    public void ChangePassword(string newPasswordHash, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new DomainException("A password hash is required.");
        }

        PasswordHash = newPasswordHash;

        // A credential change invalidates every existing session.
        RevokeAllRefreshTokens(utcNow);
    }

    /// <summary>
    /// Re-hashes the same password with current parameters. Unlike
    /// <see cref="ChangePassword"/> the credential itself is unchanged, so existing
    /// sessions stay valid.
    /// </summary>
    public void UpgradePasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new DomainException("A password hash is required.");
        }

        PasswordHash = newPasswordHash;
    }

    public void Suspend(DateTime utcNow)
    {
        Status = UserStatus.Suspended;
        RevokeAllRefreshTokens(utcNow);
    }

    public void Reactivate()
    {
        if (Status == UserStatus.Deactivated)
        {
            throw new DomainException("A deactivated account cannot be reactivated.");
        }

        Status = UserStatus.Active;
    }

    /*
     * ---- Emailed tokens ----------------------------------------------------
     *
     * Verification and password reset. Both live on the user aggregate so the rules
     * about superseding and consuming them cannot be applied inconsistently from two
     * different call sites.
     */

    private readonly List<UserToken> _tokens = [];

    public IReadOnlyCollection<UserToken> Tokens => _tokens.AsReadOnly();

    /// <summary>
    /// Issues a token, invalidating any earlier unspent one for the same purpose.
    ///
    /// Superseding matters: requesting a second reset link must kill the first. Someone
    /// who requests a reset because they suspect their mailbox was seen should not leave
    /// a live link behind them, and without this every request would add another working
    /// key to the account.
    /// </summary>
    public UserToken IssueToken(
        UserTokenPurpose purpose,
        string tokenHash,
        DateTime utcNow,
        TimeSpan lifetime)
    {
        foreach (var existing in _tokens.Where(t => t.Purpose == purpose && t.IsUsableAt(utcNow)))
        {
            existing.Consume(utcNow);
        }

        var token = UserToken.Issue(Id, purpose, tokenHash, utcNow, lifetime);
        _tokens.Add(token);

        return token;
    }

    /// <summary>
    /// Redeems a token, or returns false.
    ///
    /// Returns a plain false rather than throwing per reason, because the caller must
    /// say the same thing whatever went wrong — "this link is no longer valid". Telling
    /// someone whether a token was wrong, expired or already used tells an attacker the
    /// same thing.
    /// </summary>
    public bool TryConsumeToken(UserTokenPurpose purpose, string tokenHash, DateTime utcNow)
    {
        var token = _tokens.SingleOrDefault(
            t => t.Purpose == purpose && t.TokenHash == tokenHash);

        if (token is null || !token.IsUsableAt(utcNow))
        {
            return false;
        }

        token.Consume(utcNow);
        return true;
    }

    public RefreshToken IssueRefreshToken(string tokenHash, DateTime utcNow, TimeSpan lifetime)
    {
        if (!CanAuthenticate)
        {
            throw new DomainException("This account cannot be issued tokens.");
        }

        var token = RefreshToken.Issue(Id, tokenHash, utcNow, lifetime);
        _refreshTokens.Add(token);
        return token;
    }

    /// <summary>
    /// Exchanges an active refresh token for a new one.
    /// If the presented token exists but is no longer active, it is treated as a
    /// replay of a stolen token: every token for this user is revoked and the
    /// rotation is refused.
    /// </summary>
    public RefreshToken RotateRefreshToken(
        string presentedTokenHash,
        string newTokenHash,
        DateTime utcNow,
        TimeSpan lifetime)
    {
        var existing = _refreshTokens.SingleOrDefault(t => t.TokenHash == presentedTokenHash)
            ?? throw new DomainException("The refresh token is not recognized.");

        if (!existing.IsActiveAt(utcNow))
        {
            RevokeAllRefreshTokens(utcNow);
            throw new DomainException("The refresh token is no longer valid.");
        }

        var replacement = RefreshToken.Issue(Id, newTokenHash, utcNow, lifetime);
        existing.Revoke(utcNow, newTokenHash);
        _refreshTokens.Add(replacement);
        return replacement;
    }

    /// <summary>Revokes a single token. Logging out an unknown token is a no-op.</summary>
    public void RevokeRefreshToken(string tokenHash, DateTime utcNow) =>
        _refreshTokens.SingleOrDefault(t => t.TokenHash == tokenHash)?.Revoke(utcNow);

    public void RevokeAllRefreshTokens(DateTime utcNow)
    {
        foreach (var token in _refreshTokens.Where(t => t.IsActiveAt(utcNow)))
        {
            token.Revoke(utcNow);
        }
    }
}
