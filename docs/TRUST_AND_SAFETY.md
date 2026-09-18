# TRUST_AND_SAFETY.md — Screening, Verification, and Ongoing Trust

This documents the target trust-and-safety model for GoSit's marketplace, most of which
is **not yet implemented**. Where something exists in code today, it is called out
explicitly; everything else is a documented direction for future phases, following
`docs/PLAN.md`. See `docs/BUSINESS_MODEL.md` for the legal and insurance framing this
model sits inside.

## What exists today

From `docs/PLAN.md` Phase 7 and the current domain model:

- `VerificationCase` — staff-decided identity and background-check cases; a caregiver
  cannot approve their own.
- `IBackgroundCheckProvider` — an abstraction with **no vendor wired up**. Today,
  "verified" means a staff member reviewed an uploaded document, which is materially
  weaker than a real criminal record check and must not be marketed as one.
- Certification submission, document upload, staff review, and expiry —
  `CaregiverCertification`, enforced against `IsValidOn` at both request and accept.
- Two-way reviews, user reporting, an incident triage queue, and account suspension
  (revokes every refresh token).
- Screening already follows the kind of care offered: people care needs identity
  verification and a vulnerable sector check; pet care only needs identity verification
  and a criminal record check (CLAUDE.md, "Caregiver Qualification").
- **Caregiver Service Agreement digital signature.** `CaregiverProfile.AcceptServiceAgreement`
  records a typed full legal name against the version in force
  (`ServiceAgreement.CurrentVersion`) and a timestamp — a clickwrap-style electronic
  signature, not a DocuSign-style flow (no e-signature vendor is selected). A caregiver
  cannot be listed without a current signature: `CaregiverProfile.IsListable` requires it,
  and so does `CareBooking`'s own booking-eligibility check, so an unsigned caregiver
  cannot be booked even if something bypassed the listing check. `POST
  /api/v1/caregivers/me/service-agreement/accept`. The document itself is still an
  unreviewed draft — see docs/BUSINESS_MODEL.md — bumping `CurrentVersion` is how a real,
  lawyer-approved revision would require every caregiver to sign again.
- **Caregiver insurance verification.** `CaregiverInsurancePolicy` mirrors
  `CaregiverCertification` exactly: submit, upload proof, staff approve/reject (approval
  requires a document), 30-day and lapsed expiry notices via `InsuranceExpirySweeper`.
  Routes under `/api/v1/caregivers/me/insurance` and `/api/v1/admin/insurance`. **Not**
  wired into `IsEligibleFor` or `IsListable` — a caregiver can be fully listable with no
  insurance on file, because which services require which coverage, at what limit, is
  still an open decision (see "Open product questions" below), not an engineering gap.

Everything below this line is the target model layered on top of that foundation.

## Screening and verification vendors — candidates, none selected

Do not treat any of these as chosen. They are documented so evaluation has a concrete
starting list, not because a decision has been made.

**1. Certn** — Canadian criminal checks, identity verification, reference checks, motor
vehicle records, API/webhook integration, marketplace-partnership suitability. Currently
the strongest candidate to evaluate first, because of its API-first model.

**2. Sterling Backcheck / First Advantage** — identity verification, criminal checks and
re-checks, credential verification, references, driver abstracts, enterprise
integrations. Vulnerable Sector Checks may still require separate, local police workflows
depending on jurisdiction even through this vendor.

**3. Triton Canada** — Canadian criminal checks, identity-related screening, references,
employment/education checks, API/integration capability. Standard screening through
Triton does not automatically equal a Vulnerable Sector Check.

**4. Mintz Global Screening** — Canadian background screening, enterprise screening,
customized screening programs.

### Vulnerable Sector Checks are not one national product

No single private vendor can perform every Vulnerable Sector Check across Canada — it
typically routes through local police services. The architecture should support:

```text
BackgroundScreeningProvider
        +
Local Police / External VSC
        +
GoSit Verification Engine
```

Store, per verification: type, provider, status, completion date, expiry/renewal date,
provider reference, and verification metadata. Avoid retaining full sensitive reports
unless legally or operationally necessary — prefer minimal verification results.

## Verification provider abstraction (future)

To avoid vendor lock-in, the target shape is a family of narrow interfaces rather than
one wide "screening service." **Do not implement these yet** unless explicitly asked —
this is architectural direction for when a vendor decision is made.

- `IIdentityVerificationProvider`
- `IBackgroundScreeningProvider` (the eventual replacement for today's `IBackgroundCheckProvider`)
- `IVulnerableSectorVerificationProvider`
- `IReferenceCheckProvider`
- `IDriverScreeningProvider`
- `ICredentialVerificationProvider`
- `IInsuranceVerificationProvider`

Implementations would be named per vendor (`CertnBackgroundScreeningProvider`,
`SterlingBackgroundScreeningProvider`, etc.) and registered the same way
`IBackgroundCheckProvider` is today — swappable, and never assumed by a handler to be a
specific vendor.

## Caregiver onboarding workflow (target)

```text
Caregiver Application
        |
        v
Email Verified
        |
        v
Phone Verified
        |
        v
Government ID
        |
        v
Identity Verification
        |
        v
Selfie / Liveness Check
        |
        v
Address Verification
        |
        v
Background Screening
        |
        v
Vulnerable Sector Check (where applicable)
        |
        v
References
        |
        v
Skills / Experience
        |
        v
Service-Specific Certifications
        |
        v
Caregiver Insurance Verification
        |
        v
GoSit Safety Orientation
        |
        v
Terms + Code of Conduct
        |
        v
Approved
        |
        v
Can receive eligible bookings
```

Today's implementation covers identity verification, background-check cases, and
certifications; phone/address verification, liveness checks, references, insurance
verification, and a safety orientation are not built.

## Caregiver statuses (target)

Proposed states — not the current `VerificationStatus`/`AcceptingWork` model, which is
simpler today. Document as direction, implement only when asked.

```text
Applicant
IdentityPending
BackgroundCheckPending
VulnerableSectorPending
QualificationPending
InsurancePending
Approved
Active
NeedsRenewal
Restricted
Suspended
UnderInvestigation
Deactivated
Rejected
```

A caregiver who is Suspended, Deactivated, has expired insurance, or has expired required
screening must not be able to accept new affected bookings. This has to be enforced by
backend domain rules — never by hiding a button in the UI alone.

## Backend business rules (target, backend-enforced)

A caregiver cannot receive or accept a booking unless:

- the account is active;
- identity requirements are satisfied;
- screening requirements are satisfied;
- insurance requirements are satisfied;
- required certifications are current;
- required skills/service eligibility are satisfied;
- there is no active suspension;
- service-specific eligibility is satisfied.

Never rely on frontend checks for any of the above. Today's `IsEligibleFor` already
enforces the subset of this that exists (verification status, certifications, service
match); the insurance and reference conditions are additions for when those features
exist.

## Continuous trust, not a one-time check

```text
Initial Screening
        |
        v
Approved Caregiver
        |
        v
Bookings
        |
        +-- Ratings
        +-- Complaints
        +-- Cancellations
        +-- Incident Reports
        +-- Identity Rechecks
        +-- Insurance Expiry
        +-- Credential Expiry
        +-- Background Re-Screening
        |
        v
Trust Status
```

GoSit should support periodic re-screening and renewal. An annual cadence may be
evaluated, but the interval must stay configurable by jurisdiction, service, and legal
advice — do not hard-code one number.

## Insurance verification workflow (target)

```text
Caregiver applies
        |
        v
Provides insurance details/document
        |
        v
GoSit verifies:
- insurer
- policy/reference
- coverage type
- liability limit
- effective date
- expiry date
        |
        v
InsuranceVerified
        |
        v
Eligible for applicable services
```

Expiry handling:

- **30 days before expiry** — reminder.
- **7 days before expiry** — urgent reminder.
- **Expired** — remove eligibility for new affected bookings; consider hiding or
  restricting the relevant services on the caregiver's profile.

Never publish the full policy publicly. Subject to legal review, families may see a
factual status like **"Insurance Verified"** with a status/date.

## Document / credential hub (target)

```text
Caregiver
  |
  +-- Trust & Verification
         |
         +-- Identity
         +-- Background Screening
         +-- Vulnerable Sector
         +-- CPR
         +-- First Aid
         +-- Insurance
         +-- Driving
         +-- Specialized Training
```

Status model: `Pending`, `UnderReview`, `Verified`, `Rejected`, `Expired`,
`RequiresRenewal`. Store provider, type, issue date, expiry date, verification result,
reference number, and verification timestamp — avoid retaining unnecessary sensitive
document data beyond what that requires.

## Reference checks (target)

```text
Caregiver enters reference
        |
        v
Reference receives secure link
        |
        v
Reference validates:
- relationship
- reliability
- trustworthiness
- caregiving experience
- communication
        |
        v
GoSit records status
```

Reference contact information must never be publicly exposed.

## Transportation (target)

A separate capability — do not assume every caregiver may transport a care recipient or a
pet.

```text
Valid Driver Licence
        |
Driving Record
        |
Vehicle Registration
        |
Appropriate Auto Insurance
        |
GoSit Transportation Approval
```

Possible family-visible badge: "Transportation Verified" (final naming subject to legal
review). If caregivers transport pets, also require appropriate pet restraint/carrier
arrangements where applicable.

## Check-in / check-out safety

Implemented today: `CareBooking` records an optional device location at check-in and
check-out and reports whether it matched the booking address — as a status and a
distance, never a raw coordinate, and it never blocks check-in if location is unavailable.

```text
Caregiver arrives
        |
        v
Check In
        |
        +-- timestamp
        +-- optional location verification
        +-- optional/random identity selfie (target — not built)
        |
        v
Booking becomes InProgress
        |
        v
Care session
        |
        v
Check Out
        |
        v
Booking completion process
```

The optional/random identity-selfie idea needs privacy and consent review before it is
built — it is not in the current implementation.

## Safety / incident reporting

Implemented today: an incident triage queue prioritising safety concerns, closure
requiring an explanation, account suspension, and audit events on every decision (see
PLAN.md Phase 7).

Target categories for "Report Safety Issue" on any active or completed booking:

- Immediate danger (tell the reporter to call emergency services first — this is stated
  in the reporting flow itself, not left implicit)
- Injury
- Abuse allegation
- Neglect
- Harassment
- Inappropriate behaviour
- Theft
- Property damage
- Wrong caregiver arrived
- Unsafe environment
- Care instructions ignored
- Other

Pet-care-specific incident categories to add: lost-pet incident, animal injury incident,
bite/scratch incident, unsafe animal environment, property damage from a pet-care
booking.

```text
Incident Created
        |
        v
Severity Classification
        |
        v
Temporary Restriction if required
        |
        v
Trust & Safety Review
        |
        v
Evidence / Investigation
        |
        v
Outcome
```

Possible outcomes: no action, warning, additional verification, temporary restriction,
suspension, permanent deactivation. Include an audit trail and an appeals process —
appeals for a rejected verification or a suspended account are an explicit open item in
PLAN.md Phase 7.

## Marketplace ratings and reputation

Implemented today: two-way, blind-until-both-submit reviews feeding search ranking, plus
review moderation (a subject can report a review about themselves; staff can hide it,
which also recomputes the rating).

Families rate caregivers on communication, punctuality, professionalism, reliability, and
care experience. Caregivers rate care requesters on communication, accuracy of the care
description, booking experience, work environment, and punctuality.

Avoid using a single low rating as the only automatic deactivation signal. Trust
decisions should weigh complaint severity, incident history, completed bookings,
repeated low ratings, cancellation rate, no-shows, re-screening results, and credential
expiry together.

## GoSit Trust Profile (target)

Potential family-visible information on a caregiver's profile:

```text
GoSit Trust Profile

- Identity Verified
- Background Screening Completed
- Vulnerable Sector Verification
- Insurance Verified
- CPR Verified
- First Aid Verified
- Relevant Service Experience
- GoSit Bookings Completed
- Repeat Families
- Average Rating
- On-Time Rate
- Cancellation Rate
- Last Screening Date
```

Use factual status language only. Never use unsafe marketing language such as "100%
safe," "guaranteed safe caregiver," or "GoSit guarantees this caregiver." Final public
wording needs legal review before it ships.

## Pet care trust & safety

Pet-caregiver qualifications may include: pet-care experience, animal first aid, species
experience, large-dog experience, puppy experience, senior-pet experience, medication
experience, behaviour/aggression experience, pet transportation capability, and
appropriate liability insurance.

Pet care needs the same building blocks as care for people — identity verification,
background screening, insurance verification, references, incident reporting — plus the
pet-specific incident categories listed above under "Safety / incident reporting."

Search filters for pet caregivers may include: species, dog size, availability,
distance, rate, animal first aid, medication experience, overnight availability,
transportation, and verified insurance.

Pet care uses the same marketplace transaction-fee model as other GoSit services unless
pricing is later differentiated (see `docs/BUSINESS_MODEL.md`, "Revenue model").

## Privacy and data minimization

GoSit handles sensitive information: children's and seniors' data, disability-related
data, care needs, allergies, addresses, emergency contacts, background-screening results,
identity documents, caregiver insurance, messages, and location.

Principles to hold to across all of the above:

- Collect only what is necessary.
- Minimum retention, not indefinite retention.
- Role/resource-based authorization on every read.
- No sensitive PII in logs.
- Encrypted transport and storage.
- Private documents never publicly accessible.
- Verification summaries rather than raw reports where practical.
- Consent management for anything requiring it (video interview recording already works
  this way — see CLAUDE.md, "Video Interviews").
- Deletion/retention workflows exist and are documented (interview recordings already
  follow a 90-day sweep with a staff hold override — see PLAN.md Phase 5).

## Open vendor questions

- Which screening vendor, if any, gets evaluated first — Certn is the current front
  runner given its API-first model, but nothing is decided.
- How will Vulnerable Sector Checks route to local police services in each jurisdiction
  GoSit operates in?
- Does the chosen vendor also offer reference checks and driver screening, or do those
  need a second vendor?

## Open product questions

- What does "Approved" actually require before a caregiver can accept a first booking —
  every item in the onboarding workflow above, or a smaller MVP subset?
- Is the Trust Profile shown on every caregiver's public profile, or only surfaced at
  booking time?
- Does an "Insurance Verified" badge ship before or after a real screening vendor is
  wired up?
