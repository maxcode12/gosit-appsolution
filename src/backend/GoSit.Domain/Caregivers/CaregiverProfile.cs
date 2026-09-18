using GoSit.Domain.CareServices;
using GoSit.Domain.Common;
using GoSit.Domain.Common.ValueObjects;

namespace GoSit.Domain.Caregivers;

/// <summary>
/// Profile of someone providing care. Aggregate root for availability and
/// certifications.
///
/// Nothing here decides on its own whether a caregiver may take a booking — that is
/// <see cref="MeetsRequirements"/> combined with the service's own rules, evaluated
/// server-side before a booking is accepted.
/// </summary>
public sealed class CaregiverProfile : Entity
{
    private const int MaxBioLength = 2000;
    private const int MaxAgreementSignedNameLength = 200;
    // Ontario's general minimum wage from October 1, 2025. It is indexed every October,
    // so this needs checking each year.
    private const decimal MinimumHourlyRate = 17.60m;
    private const decimal MaximumHourlyRate = 200m;
    private const decimal MinimumNightlyRate = 10m;
    private const decimal MaximumNightlyRate = 500m;
    private const int MaxAvailabilitySlots = 60;
    private const int MaxServices = 25;

    private readonly List<AvailabilitySlot> _availability = [];
    private readonly List<CaregiverCertification> _certifications = [];
    private readonly List<CareService> _services = [];
    private readonly List<CaregiverInsurancePolicy> _insurancePolicies = [];

    private CaregiverProfile()
    {
    }

    public Guid UserId { get; private set; }

    public string? Bio { get; private set; }

    public int YearsOfExperience { get; private set; }

    /// <summary>Advertised rate in CAD. Decimal — never floating point for money.</summary>
    public decimal HourlyRate { get; private set; }

    /// <summary>
    /// The price of a night's pet care — boarding or in-home sitting — or null for a caregiver
    /// who offers neither. One rate for both. Booking totals for a stay are computed from this,
    /// never from a client-supplied price.
    /// </summary>
    public decimal? NightlyRate { get; private set; }

    public Address? Address { get; private set; }

    /// <summary>How far the caregiver will travel, in kilometres.</summary>
    public int ServiceRadiusKm { get; private set; } = 10;

    public VerificationStatus VerificationStatus { get; private set; }

    /// <summary>
    /// Whether this caregiver's checks clear them to care for people, as opposed to pets
    /// only.
    ///
    /// Verification used to have one bar — photo ID and a vulnerable sector check — because
    /// every service was care for a person. Pet care is screened with photo ID and a
    /// criminal record check instead, so "verified" now means verified for one of two
    /// things. <see cref="IsEligibleFor"/> tests this for every service for people, which is
    /// what stops a sitter cleared only for pets being booked for a child or a senior after
    /// adding those services to their profile.
    /// </summary>
    public bool ClearedForPeopleCare { get; private set; }

    /// <summary>
    /// Whether the caregiver wants to appear in search. Not sufficient on its own —
    /// see <see cref="IsListable"/>.
    /// </summary>
    public bool AcceptingWork { get; private set; }

    /// <summary>
    /// Whether this caregiver has said they can take a booking starting soon, not just
    /// one that fits their usual weekly schedule.
    ///
    /// Search has no concept of an absolute requested date and time today — availability
    /// filtering works against a recurring weekly window (day, start, end), never against
    /// "book me for 2pm this afternoon". This flag cannot promise a specific caregiver is
    /// free in the next hour; it only says they are open to being asked. See
    /// <c>CaregiverRanking</c> for how it nudges ranking, deliberately within the
    /// same narrow band as a paid promotion rather than overriding distance or rating.
    /// </summary>
    public bool AvailableForLastMinuteRequests { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<AvailabilitySlot> Availability => _availability.AsReadOnly();

    public IReadOnlyCollection<CaregiverCertification> Certifications => _certifications.AsReadOnly();

    public IReadOnlyCollection<CaregiverInsurancePolicy> InsurancePolicies => _insurancePolicies.AsReadOnly();

    /// <summary>
    /// The Caregiver Service Agreement version this caregiver last signed, or null.
    /// See <see cref="ServiceAgreement.CurrentVersion"/> and
    /// <see cref="HasAcceptedCurrentAgreement"/>.
    /// </summary>
    public string? AgreementAcceptedVersion { get; private set; }

    /// <summary>The typed full legal name entered as the electronic signature.</summary>
    public string? AgreementSignedName { get; private set; }

    public DateTime? AgreementAcceptedAtUtc { get; private set; }

    /// <summary>
    /// Whether the caregiver has signed the agreement version in force today. A version
    /// bump (a real revision of the reviewed agreement) makes every existing signature
    /// stale, requiring a fresh one before the caregiver can be listed again.
    /// </summary>
    public bool HasAcceptedCurrentAgreement =>
        AgreementAcceptedVersion == ServiceAgreement.CurrentVersion;

    /// <summary>
    /// Services this caregiver offers. Offering is a claim, not a qualification —
    /// eligibility is decided by <see cref="IsEligibleFor"/>.
    /// </summary>
    public IReadOnlyCollection<CareService> Services => _services.AsReadOnly();

    /// <summary>
    /// Mean review score, or null before any review. Denormalized onto the profile so
    /// search can rank by it without joining reviews; Reviews arrive in Phase 7 and will
    /// call <see cref="UpdateRatingSummary"/>.
    /// </summary>
    public decimal? AverageRating { get; private set; }

    public int ReviewCount { get; private set; }

    /// <summary>
    /// The payout provider's connected-account reference — a Stripe Connect account id.
    ///
    /// Null until the caregiver starts onboarding. GoSit never holds their bank details:
    /// they are given to the provider through its own hosted flow, and all we keep is
    /// this reference.
    /// </summary>
    public string? PayoutAccountId { get; private set; }

    /// <summary>
    /// Whether the provider says money can actually be sent to this account.
    ///
    /// Reported by the provider, never inferred from onboarding having been completed —
    /// an account can submit every detail and still be held for review, and paying out
    /// on our own assumption would strand the money in a failed transfer.
    /// </summary>
    public bool PayoutsEnabled { get; private set; }

    /// <summary>
    /// Whether the onboarding form has been completed. True with
    /// <see cref="PayoutsEnabled"/> false means "submitted, under review", which is a
    /// different thing to tell someone than "you have not started".
    /// </summary>
    public bool PayoutDetailsSubmitted { get; private set; }

    /// <summary>Why the provider is holding the account, when it is. Shown to the caregiver.</summary>
    public string? PayoutDisabledReason { get; private set; }

    /// <summary>The single condition the payout sweep checks before sending money.</summary>
    public bool CanReceivePayouts =>
        !string.IsNullOrWhiteSpace(PayoutAccountId) && PayoutsEnabled;

    /// <summary>
    /// Records the connected account created for this caregiver. Set once: repointing a
    /// live account at a different one would orphan its payout history and could send
    /// someone's earnings to a stranger.
    /// </summary>
    public void LinkPayoutAccount(string payoutAccountId, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(payoutAccountId))
        {
            throw new DomainException("A payout account id is required.");
        }

        if (!string.IsNullOrWhiteSpace(PayoutAccountId) && PayoutAccountId != payoutAccountId)
        {
            throw new DomainException("This caregiver already has a payout account.");
        }

        PayoutAccountId = payoutAccountId.Trim();
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Applies what the provider reports about the account. Idempotent — this arrives on
    /// a webhook that repeats.
    /// </summary>
    public void UpdatePayoutCapability(
        bool payoutsEnabled,
        bool detailsSubmitted,
        string? disabledReason,
        DateTime utcNow)
    {
        PayoutsEnabled = payoutsEnabled;
        PayoutDetailsSubmitted = detailsSubmitted;
        PayoutDisabledReason = string.IsNullOrWhiteSpace(disabledReason)
            ? null
            : disabledReason.Trim();

        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Whether this caregiver may appear in search results. Verification is required:
    /// an unverified caregiver is never surfaced, however complete their profile.
    /// </summary>
    public bool IsListable =>
        AcceptingWork
        && VerificationStatus == VerificationStatus.Verified
        && Address is not null
        && _availability.Count > 0
        && HourlyRate > 0
        && HasAcceptedCurrentAgreement;

    public static CaregiverProfile Create(
        Guid userId,
        decimal hourlyRate,
        int yearsOfExperience,
        DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A user is required.");
        }

        var profile = new CaregiverProfile
        {
            UserId = userId,
            // Every caregiver starts unverified. Only Trust and Safety changes this.
            VerificationStatus = VerificationStatus.Unverified,
            AcceptingWork = false,
            CreatedAtUtc = utcNow,
        };

        profile.SetRate(hourlyRate, utcNow);
        profile.SetExperience(yearsOfExperience, utcNow);
        return profile;
    }

    public void UpdateBio(string? bio, DateTime utcNow)
    {
        if (bio is not null && bio.Trim().Length > MaxBioLength)
        {
            throw new DomainException($"A bio must be {MaxBioLength} characters or fewer.");
        }

        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
        UpdatedAtUtc = utcNow;
    }

    public void SetRate(decimal hourlyRate, DateTime utcNow)
    {
        // The floor is the Ontario general minimum wage. A lower rate is not a pricing
        // choice GoSit can offer. Booking totals are always computed from this value,
        // never from a rate supplied by a client.
        if (hourlyRate < MinimumHourlyRate)
        {
            throw new DomainException(
                $"An hourly rate must be at least {MinimumHourlyRate} CAD.");
        }

        if (hourlyRate > MaximumHourlyRate)
        {
            throw new DomainException(
                $"An hourly rate must be at most {MaximumHourlyRate} CAD.");
        }

        HourlyRate = decimal.Round(hourlyRate, 2, MidpointRounding.ToEven);
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Sets the price of a night's pet care, or clears it for someone who stops offering it.
    ///
    /// Separate from the hourly rate rather than derived from it: a night hosting a dog is
    /// not twenty-four hours of labour, and nobody prices it that way. The bounds are sanity
    /// limits that catch a typo, not a wage floor — the right range is a pricing decision.
    /// </summary>
    public void SetNightlyRate(decimal? nightlyRate, DateTime utcNow)
    {
        if (nightlyRate is { } rate)
        {
            if (rate < MinimumNightlyRate)
            {
                throw new DomainException($"A nightly rate must be at least {MinimumNightlyRate} CAD.");
            }

            if (rate > MaximumNightlyRate)
            {
                throw new DomainException($"A nightly rate must be at most {MaximumNightlyRate} CAD.");
            }

            NightlyRate = decimal.Round(rate, 2, MidpointRounding.ToEven);
        }
        else
        {
            NightlyRate = null;
        }

        UpdatedAtUtc = utcNow;
    }

    public void SetExperience(int yearsOfExperience, DateTime utcNow)
    {
        if (yearsOfExperience is < 0 or > 70)
        {
            throw new DomainException("Years of experience must be between 0 and 70.");
        }

        YearsOfExperience = yearsOfExperience;
        UpdatedAtUtc = utcNow;
    }

    public void SetServiceArea(Address address, int serviceRadiusKm, DateTime utcNow)
    {
        if (serviceRadiusKm is < 1 or > 100)
        {
            throw new DomainException("A service radius must be between 1 and 100 km.");
        }

        Address = address;
        ServiceRadiusKm = serviceRadiusKm;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Replaces the whole weekly schedule. Wholesale replacement keeps the no-overlap
    /// rule checkable in one place.
    /// </summary>
    public void ReplaceAvailability(
        IEnumerable<AvailabilityWindow> windows,
        DateTime utcNow)
    {
        var replacement = new List<AvailabilitySlot>();

        foreach (var window in windows)
        {
            var slot = AvailabilitySlot.Create(Id, window.Day, window.StartTime, window.EndTime);

            if (replacement.Any(existing => existing.OverlapsWith(slot)))
            {
                throw new DomainException(
                    $"Availability on {window.Day} overlaps another slot on the same day.");
            }

            replacement.Add(slot);
        }

        if (replacement.Count > MaxAvailabilitySlots)
        {
            throw new DomainException(
                $"At most {MaxAvailabilitySlots} availability slots can be set.");
        }

        _availability.Clear();
        _availability.AddRange(replacement);
        UpdatedAtUtc = utcNow;
    }

    public CaregiverCertification SubmitCertification(
        Certification certification,
        DateOnly issuedOn,
        DateOnly? expiresOn,
        DateTime utcNow)
    {
        var today = DateOnly.FromDateTime(utcNow);

        var alreadyHeld = _certifications.Any(c =>
            c.CertificationId == certification.Id
            && c.Status != CertificationStatus.Rejected
            && !c.IsExpiredOn(today));

        if (alreadyHeld)
        {
            throw new DomainException($"{certification.Name} has already been submitted.");
        }

        var submitted = CaregiverCertification.Submit(
            Id, certification, issuedOn, expiresOn, utcNow);

        _certifications.Add(submitted);
        UpdatedAtUtc = utcNow;
        return submitted;
    }

    /// <summary>
    /// Records the evidence uploaded for a certification, and sends it back for review.
    ///
    /// Reopening is not optional. A caregiver who could replace the document on an
    /// already-approved certification could be verified on one file and then swap in
    /// another, leaving the Verified badge attesting to something nobody read.
    /// </summary>
    public void AttachCertificationDocument(
        Guid caregiverCertificationId,
        string documentReference,
        DateTime utcNow)
    {
        var certification = RequireCertification(caregiverCertificationId);

        certification.AttachDocument(documentReference);
        certification.ReopenForReview();

        UpdatedAtUtc = utcNow;
    }

    public void ApproveCertification(Guid caregiverCertificationId, DateTime utcNow)
    {
        RequireCertification(caregiverCertificationId).Approve(utcNow);
        UpdatedAtUtc = utcNow;
    }

    public void RejectCertification(Guid caregiverCertificationId, string reason, DateTime utcNow)
    {
        RequireCertification(caregiverCertificationId).Reject(reason, utcNow);
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Records the caregiver's electronic signature on the Caregiver Service Agreement.
    ///
    /// A typed full legal name plus a server-recorded timestamp and version, in the same
    /// tradition as a clickwrap agreement — the caregiver never chooses which version they
    /// are signing; it is always whichever <see cref="ServiceAgreement.CurrentVersion"/>
    /// is at the moment they act, so a version bump cannot be dodged by resubmitting an
    /// old one.
    /// </summary>
    public void AcceptServiceAgreement(string signedName, DateTime utcNow)
    {
        var trimmed = signedName?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new DomainException("Type your full legal name to sign the agreement.");
        }

        if (trimmed.Length > MaxAgreementSignedNameLength)
        {
            throw new DomainException(
                $"A signed name must be {MaxAgreementSignedNameLength} characters or fewer.");
        }

        AgreementAcceptedVersion = ServiceAgreement.CurrentVersion;
        AgreementSignedName = trimmed;
        AgreementAcceptedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public CaregiverInsurancePolicy SubmitInsurance(
        InsuranceCoverageType coverageType,
        string insurer,
        string policyReference,
        decimal liabilityLimit,
        DateOnly effectiveOn,
        DateOnly expiresOn,
        DateTime utcNow)
    {
        var submitted = CaregiverInsurancePolicy.Submit(
            Id, coverageType, insurer, policyReference, liabilityLimit, effectiveOn, expiresOn, utcNow);

        _insurancePolicies.Add(submitted);
        UpdatedAtUtc = utcNow;
        return submitted;
    }

    /// <summary>
    /// Records the evidence uploaded for an insurance policy, and sends it back for
    /// review — the same reopen-on-replace rule as a certification document, for the
    /// same reason: an "Insurance Verified" badge has to mean someone looked at
    /// something, and swapping the file after approval must not keep the approval.
    /// </summary>
    public void AttachInsuranceDocument(Guid caregiverInsurancePolicyId, string documentReference, DateTime utcNow)
    {
        var policy = RequireInsurancePolicy(caregiverInsurancePolicyId);

        policy.AttachDocument(documentReference);
        policy.ReopenForReview();

        UpdatedAtUtc = utcNow;
    }

    public void ApproveInsurance(Guid caregiverInsurancePolicyId, DateTime utcNow)
    {
        RequireInsurancePolicy(caregiverInsurancePolicyId).Approve(utcNow);
        UpdatedAtUtc = utcNow;
    }

    public void RejectInsurance(Guid caregiverInsurancePolicyId, string reason, DateTime utcNow)
    {
        RequireInsurancePolicy(caregiverInsurancePolicyId).Reject(reason, utcNow);
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Insurance warnings owed to this caregiver, marking each as given so it is only
    /// said once. Mirrors <see cref="TakeExpiryNotices"/> for certifications.
    /// </summary>
    public IReadOnlyList<LapsingInsurancePolicy> TakeInsuranceExpiryNotices(DateOnly today, int daysAhead)
    {
        var notices = new List<LapsingInsurancePolicy>();

        foreach (var policy in _insurancePolicies)
        {
            if (policy.NoticeDueOn(today, daysAhead) is not { } stage)
            {
                continue;
            }

            notices.Add(new LapsingInsurancePolicy(policy.Id, policy.CoverageType, policy.ExpiresOn, stage));
            policy.MarkExpiryNoticed(stage);
        }

        return notices;
    }

    /// <summary>
    /// Whether this caregiver currently holds a verified, unexpired policy of the given
    /// coverage type. Not yet wired into booking eligibility — see
    /// docs/TRUST_AND_SAFETY.md for why: which services require which coverage, and at
    /// what limit, is an open insurance decision, not a built rule.
    /// </summary>
    public bool HasValidInsurance(InsuranceCoverageType coverageType, DateOnly date) =>
        _insurancePolicies.Any(p => p.CoverageType == coverageType && p.IsValidOn(date));

    private CaregiverInsurancePolicy RequireInsurancePolicy(Guid caregiverInsurancePolicyId) =>
        _insurancePolicies.SingleOrDefault(p => p.Id == caregiverInsurancePolicyId)
        ?? throw new DomainException("That insurance policy does not exist.");

    /// <summary>
    /// Set by Trust and Safety only, never by the caregiver.
    ///
    /// The clearance has no default, because the default that would have kept every
    /// existing caller compiling — cleared for people — is the dangerous one.
    /// </summary>
    public void MarkVerified(bool clearedForPeopleCare, DateTime utcNow)
    {
        VerificationStatus = VerificationStatus.Verified;
        ClearedForPeopleCare = clearedForPeopleCare;
        UpdatedAtUtc = utcNow;
    }

    public void MarkVerificationPending(DateTime utcNow)
    {
        VerificationStatus = VerificationStatus.Pending;
        UpdatedAtUtc = utcNow;
    }

    public void MarkVerificationRejected(DateTime utcNow)
    {
        VerificationStatus = VerificationStatus.Rejected;
        ClearedForPeopleCare = false;
        // A rejected caregiver must not stay listed.
        AcceptingWork = false;
        UpdatedAtUtc = utcNow;
    }

    public void SetAcceptingWork(bool acceptingWork, DateTime utcNow)
    {
        if (acceptingWork && VerificationStatus != VerificationStatus.Verified)
        {
            throw new DomainException("A caregiver must be verified before accepting work.");
        }

        AcceptingWork = acceptingWork;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Same verification gate as <see cref="SetAcceptingWork"/>, for the same reason: an
    /// unverified caregiver must not be more findable, whether the boost is "accepting
    /// work at all" or "available on short notice".
    /// </summary>
    public void SetAvailableForLastMinuteRequests(bool available, DateTime utcNow)
    {
        if (available && VerificationStatus != VerificationStatus.Verified)
        {
            throw new DomainException("A caregiver must be verified before offering last-minute availability.");
        }

        AvailableForLastMinuteRequests = available;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// The certification codes this caregiver validly holds on a given date. This is
    /// the input to service eligibility — see the qualification rules in CLAUDE.md.
    /// </summary>
    public IReadOnlySet<string> ValidCertificationCodesOn(DateOnly date) =>
        _certifications
            .Where(c => c.IsValidOn(date))
            .Select(c => c.Certification.Code)
            .ToHashSet();

    /// <summary>
    /// Whether the caregiver holds every required certification on the given date.
    /// Booking acceptance calls this; it never trusts a client-supplied claim.
    /// </summary>
    public bool MeetsRequirements(IEnumerable<string> requiredCertificationCodes, DateOnly date)
    {
        var held = ValidCertificationCodesOn(date);

        return requiredCertificationCodes
            .Select(code => code.Trim().ToUpperInvariant())
            .All(held.Contains);
    }

    /// <summary>
    /// Warnings owed to this caregiver about certifications lapsing, marking each as
    /// given so it is only said once.
    ///
    /// A lapsed certification removes the caregiver from search for every service that
    /// requires it, and stops them accepting one — see <see cref="IsEligibleFor"/>. That
    /// happens silently: they were never told it was coming, and afterwards the only sign
    /// is work that stops arriving. This is how they find out in time to renew.
    ///
    /// The affected services are worked out here because the loss is the point. "Your
    /// First Aid certificate expires on the 3rd" is a diary note; "and you will stop
    /// being bookable for After-School Care" is a reason to act.
    /// </summary>
    public IReadOnlyList<LapsingCertification> TakeExpiryNotices(DateOnly today, int daysAhead)
    {
        var notices = new List<LapsingCertification>();

        foreach (var certification in _certifications)
        {
            if (certification.NoticeDueOn(today, daysAhead) is not { } stage)
            {
                continue;
            }

            var code = certification.Certification.Code;

            var affected = _services
                .Where(s => s.RequiredCertificationCodes.Contains(code))
                .Select(s => s.Name)
                .Order()
                .ToList();

            notices.Add(new LapsingCertification(
                certification.Id,
                certification.Certification.Name,
                certification.ExpiresOn!.Value,
                stage,
                affected));

            certification.MarkExpiryNoticed(stage);
        }

        return notices;
    }

    /// <summary>
    /// Replaces the set of services offered. A caregiver may offer a service they are
    /// not yet certified for — the claim is allowed, the eligibility is not granted.
    /// </summary>
    public void ReplaceServices(IEnumerable<CareService> services, DateTime utcNow)
    {
        var replacement = services.DistinctBy(s => s.Id).ToList();

        if (replacement.Count > MaxServices)
        {
            throw new DomainException($"At most {MaxServices} services can be offered.");
        }

        _services.Clear();
        _services.AddRange(replacement);
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Whether this caregiver may be offered for a service: they list it, their checks
    /// clear them for that kind of care, and they hold every certification it requires.
    /// Search filters on this, and booking re-checks it.
    /// </summary>
    public bool IsEligibleFor(CareService service, DateOnly date) =>
        _services.Any(s => s.Id == service.Id)
        && (service.IsPetCare || ClearedForPeopleCare)
        // A service priced by the night cannot be offered by someone with no nightly price.
        && (service.PricingUnit == PricingUnit.Hourly || NightlyRate is not null)
        && MeetsRequirements(service.RequiredCertificationCodes, date);

    /// <summary>
    /// True when everything this caregiver offers is pet care, which decides the checks
    /// they need to be listed. A caregiver offering nothing yet is held to the people-care
    /// bar: before we know, the stricter answer is the safe one.
    /// </summary>
    public bool OffersOnlyPetCare =>
        _services.Count > 0 && _services.All(s => s.IsPetCare);

    /// <summary>
    /// Set from reviews in Phase 7. Kept as a domain method rather than a settable
    /// property so a rating can never be written directly by a request handler.
    /// </summary>
    public void UpdateRatingSummary(decimal? averageRating, int reviewCount, DateTime utcNow)
    {
        if (reviewCount < 0)
        {
            throw new DomainException("A review count cannot be negative.");
        }

        if (averageRating is { } rating && rating is < 1m or > 5m)
        {
            throw new DomainException("An average rating must be between 1 and 5.");
        }

        AverageRating = averageRating;
        ReviewCount = reviewCount;
        UpdatedAtUtc = utcNow;
    }

    private CaregiverCertification RequireCertification(Guid caregiverCertificationId) =>
        _certifications.SingleOrDefault(c => c.Id == caregiverCertificationId)
        ?? throw new DomainException("That certification does not exist.");
}

/// <summary>One weekly availability window, as supplied by the caller.</summary>
public sealed record AvailabilityWindow(DayOfWeek Day, TimeOnly StartTime, TimeOnly EndTime);
