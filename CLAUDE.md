# CLAUDE.md — GoSit Engineering Instructions

## Project Identity

**Product:** GoSit  
**Legal entity:** 11419056 Canada Inc., operating as GoSit ("GoSit") — a corporation
incorporated under the Canada Business Corporations Act, extra-provincially registered in
Ontario. "GoSit" is a trade name, not a separate legal entity; do not write documentation
implying GoSit is incorporated on its own. See `docs/BUSINESS_MODEL.md`.
**Product type:** Trusted care marketplace for children, seniors, adults, and people requiring specialized non-medical support, plus a separate pet-care vertical.  
**Primary market:** Canada-first, with Ontario as the initial operating market.  
**Design source of truth:** Figma file `GoSitly-Wireframe`  
**Figma:** https://www.figma.com/design/sJQ271UkXOx3Uiklp2Bn41/GoSitly-Wireframe

## Business Model — Marketplace, Not Employer

GoSit is a technology-enabled marketplace connecting a `CareRequester` with an
independent `Caregiver`. This is a legal position, not just a description, and it
constrains how every feature is written and documented:

- Caregivers are independent marketplace providers, never GoSit employees, agents, or
  contractors performing work GoSit directs. GoSit does not supervise the manner or
  means of care, in the way an employer or a home-care agency would.
- GoSit does not directly provide care. It provides discovery, matching, verification,
  booking, scheduling, communication, payments, reviews, check-in/check-out, and trust
  and safety tooling around a relationship the CareRequester and the Caregiver form
  themselves.
- GoSit does not guarantee caregiver performance, quality, or safety. Verification and
  screening reduce risk; they are not a warranty, and documentation and product copy must
  not imply one (no "100% safe," "guaranteed safe caregiver," or similar).
- Caregivers maintain their own insurance; GoSit does not insure caregivers as if they
  were employees or care providers. See "Insurance" below and `docs/BUSINESS_MODEL.md`.
- Every caregiver must electronically sign the current Caregiver Service Agreement before
  they can be listed (`CaregiverProfile.IsListable`, `CaregiverProfile.AcceptServiceAgreement`).
  This is a mechanism, not a claim that the underlying document is finished — see
  `GoSit.Domain.Caregivers.ServiceAgreement.CurrentVersion` and `docs/BUSINESS_MODEL.md`.

Full rationale, the future-subsidiary option, and open legal questions are in
`docs/BUSINESS_MODEL.md` and `docs/ADR-004-marketplace-provider-model.md`.

## Product Positioning

> **Trusted care for every stage of family life.**

GoSit connects care requesters with verified caregivers for:
- Child care
- Senior care
- Special-needs support
- Respite care
- Companionship
- Mobility assistance
- Appointment accompaniment
- Non-medical family support
- Pet care: dog walking, drop-in visits, in-home pet sitting, boarding

MVP remains focused on **non-medical care** unless future legal/regulatory work explicitly enables regulated clinical services.

## Architecture — Option C

- Web: Next.js + React + TypeScript
- Mobile: .NET MAUI Blazor Hybrid
- Backend: ASP.NET Core Web API with Controllers
- Architecture: Feature-oriented modular monolith / Vertical Slice Architecture
- Database: PostgreSQL
- Cache: Redis
- Storage: Azure Blob Storage
- Messaging: Azure Service Bus
- Real-time: SignalR
- Payments/Payouts: Stripe / Stripe Connect boundary
- Cloud: Microsoft Azure
- CI/CD: Azure DevOps
- Observability: OpenTelemetry + Application Insights

The API must remain independent from web/mobile implementation details.

## Core Domain Vocabulary

Use:
- `CareRequester` — person arranging care
- `CareRecipient` — person receiving care
- `Caregiver` — person providing approved care services
- `CareService` — configured service such as babysitting, senior companionship, respite support
- `CareNeedsProfile` — recipient-specific care/support needs
- `CareRequest` — request for care
- `CareBooking` — accepted and managed care engagement
- `Pet` — an animal a CareRequester arranges care for. Not a CareRecipient: a pet has no `CareNeedsProfile` and none of the protections written for people

Avoid designing the domain only around `Parent`, `Child`, and `Babysitter`.

`CareRecipientType`:
- Child
- Adult
- Senior

Special needs are represented through `CareNeedsProfile`, not a separate recipient type.

Pets are not a recipient type either. They are a separate `Pet` entity, and a `CareService` is care for people (it names recipient types) or care for pets (it names species) — never both. A booking names people or pets, never both.

## Feature Structure

```text
Features/
├── Authentication/
├── CareRequesters/
│   ├── Profiles/
│   └── EmergencyContacts/
├── CareRecipients/
│   ├── Profiles/
│   └── CareNeeds/
├── Pets/
├── Caregivers/
│   ├── Profiles/
│   ├── Skills/
│   ├── Certifications/
│   ├── Services/
│   ├── Availability/
│   ├── Verification/
│   ├── Insurance/          # planned — see docs/TRUST_AND_SAFETY.md
│   ├── References/         # planned
│   └── Transportation/     # planned
├── CareServices/
├── Search/
├── Matching/
├── CareRequests/
├── Bookings/
│   ├── Create/
│   ├── Accept/
│   ├── Decline/
│   ├── Cancel/
│   ├── CheckIn/
│   ├── CheckOut/
│   └── Complete/
├── Payments/
├── Payouts/
├── Messaging/
├── Interviews/
├── Reviews/
├── Notifications/
├── TrustAndSafety/
│   ├── Screening/          # identity, background, vulnerable-sector, references
│   ├── Verification/
│   ├── Incidents/
│   ├── Suspensions/
│   └── Appeals/            # planned — see docs/TRUST_AND_SAFETY.md
├── Subscriptions/
└── Administration/
```

`Interviews/` and `TrustAndSafety` at the top level already exist in code. The
sub-bullets above under `Caregivers/` and `TrustAndSafety/` marked "planned" describe the
target shape from `docs/TRUST_AND_SAFETY.md`, not code that exists today — check the
actual `Features/` tree before assuming a folder is there.

## Core Entities

- User
- CareRequesterProfile
- CaregiverProfile
- CareRecipient
- CareNeedsProfile
- Pet
- EmergencyContact
- Address
- CareServiceCategory
- CareService
- CaregiverService
- Skill
- CaregiverSkill
- Certification
- CaregiverCertification
- AvailabilitySlot
- CareRequest
- CareBooking
- BookingRecipient
- BookingPet
- Payment
- Payout
- Review
- Favorite
- Conversation
- Message
- Interview
- InterviewRecording
- Notification
- SubscriptionPlan
- UserSubscription
- VerificationCase
- BackgroundCheck
- IncidentReport
- AuditEvent
- OutboxMessage

### Planned entities (not yet implemented)

From the trust-and-safety and insurance model in `docs/TRUST_AND_SAFETY.md` and
`docs/BUSINESS_MODEL.md`. Do not assume these exist in code — check the actual
`GoSit.Domain` project first.

- CaregiverInsurancePolicy / CaregiverInsuranceVerification
- IdentityVerification, BackgroundScreening, VulnerableSectorVerification (today these are
  folded into the single `VerificationCase` + `BackgroundCheck` pair above; splitting them
  out is a future refinement, not a decision made yet)
- ReferenceCheck
- DriverVerification (for the Transportation capability)
- BookingStatusHistory (today status changes are read from `AuditEvent`, not a dedicated
  history table)

## Initial Care Service Taxonomy

### Child Care
- Babysitting
- Nanny Care
- After-School Care
- Overnight Child Care

### Senior Care
- Senior Companionship
- Meal Support
- Errand Assistance
- Mobility Assistance
- Appointment Companion
- Dementia Companion Care

### Specialized Support
- Autism Support
- Developmental Disability Support
- Special-Needs Respite
- Communication Support
- Mobility Support
- Behavioral Support

### Family Support
- Respite Care
- Transportation Assistance
- Care-related light household support
- Routine/companionship support

### Pet Care
- Dog Walking
- Drop-In Pet Visits
- In-Home Pet Sitting (at the family's home, priced per night, 1–30 nights)
- Pet Boarding (at the caregiver's home, priced per night, 1–30 nights)

Pet Boarding is the only service that happens at the caregiver's home. Pet Boarding and In-Home Pet Sitting are priced per night; everything else by the hour. Care for people must never happen at a caregiver's home — that is the arrangement Ontario licenses as home child care.

Prefer configurable reference data over hard-coded enums where services will evolve.

The implemented `Pet` entity carries name, species, breed, size, feeding instructions,
medication, behaviour, vet clinic, and other notes. A richer `PetProfile` — adding date of
birth, weight, temperament, vaccination status, microchip information, a photo, and a
distinct emergency contact — is under consideration for when the pet-care vertical needs
it (see `docs/TRUST_AND_SAFETY.md`), but is not built and must not be assumed present.

## Care Needs

`CareNeedsProfile` may include:
- MobilityNeeds
- CommunicationStyle
- SensoryNeeds
- BehaviourSupportNotes
- DietaryRestrictions
- Allergies
- ToiletingAssistance
- FeedingAssistance
- RoutinePreferences
- Triggers
- CalmingStrategies
- EmergencyInstructions
- RequiredSkills

This information is sensitive and must not be public.

## Caregiver Qualification

Not every caregiver can provide every service.

Eligibility can depend on:
- Identity verification
- Background check
- CPR
- First Aid
- References
- Service-specific training
- Senior-care/dementia experience
- Autism/special-needs experience
- Mobility support experience
- Languages
- Transportation capability

The backend must evaluate `Caregiver Qualifications` against `Care Service Requirements` before a booking can be accepted.

Screening follows the kind of care offered:
- Care for people: identity verification and a vulnerable sector check
- Pet care only: identity verification and a criminal record check

A caregiver cleared only for pet care must never be eligible for a service for people, whatever services their profile lists.

Background screening and vulnerable-sector checks are performed today by staff reviewing
an uploaded document (`IBackgroundCheckProvider` has no vendor wired up — see
`docs/PLAN.md` Phase 7). Screening vendors under evaluation, and the provider-abstraction
approach for adding one without a rewrite, are documented in `docs/TRUST_AND_SAFETY.md`.
Do not treat any vendor as selected, and do not assume one vendor can perform a Vulnerable
Sector Check nationwide — it typically routes through local police services.

## Insurance

The lawyer-directed position: **caregivers maintain their own insurance; GoSit does not
insure caregivers as if they were employees or care providers.** GoSit insures its own
corporate and platform risk separately. Do not reintroduce an Uber-style "GoSit master
policy covers caregivers during bookings" model anywhere in documentation, product copy,
or code comments.

- GoSit's own coverage (commercial general liability, technology E&O, cyber/privacy,
  crime/fidelity, D&O once there are outside directors, employment practices as the
  company hires staff) should be discussed with a commercial insurance broker — see
  `docs/BUSINESS_MODEL.md` for the checklist, including the abuse/molestation exposure
  question a broker must answer explicitly given the marketplace serves children, seniors,
  and other vulnerable people.
- Caregivers may be required to carry their own coverage (commercial general liability,
  professional liability where applicable, and auto insurance if they transport a care
  recipient or a pet), verified by GoSit before they are eligible for the services that
  require it. A **$2M liability baseline is a planning assumption only**, not a stated
  requirement — it must be confirmed by counsel and an insurance broker before it appears
  anywhere user-facing.
- **Built**: `CaregiverInsurancePolicy` (submit, upload proof, staff approve/reject,
  30-day and lapsed expiry notices — mirrors `CaregiverCertification` exactly).
  **Not yet built**: any per-service insurance *requirement*. `CaregiverProfile.IsEligibleFor`
  and `IsListable` do not check insurance today; a caregiver can be fully listable with no
  insurance on file. Wiring that in is blocked on the coverage-per-service decision in
  `docs/BUSINESS_MODEL.md`, not on engineering.
- The verification workflow, statuses, and expiry handling are in
  `docs/TRUST_AND_SAFETY.md`.

## Booking State Model

```text
Pending -> Accepted
Pending -> Declined
Pending -> Cancelled

Accepted -> InProgress
Accepted -> Cancelled

InProgress -> Completed

Completed -> Disputed
```

All transitions must validate actor authorization, current state, service eligibility, timestamps, audit evidence, and integration events.

## Security & Privacy

Never publicly expose:
- CareNeedsProfile details
- Child/senior sensitive notes
- Exact home address before authorized disclosure
- Private messages
- Background-check documents
- Payment secrets
- Household details on a booking — exact address, the family's notes, pet care notes — to a caregiver who has not accepted it
- A caregiver's home address, except to a family whose boarding booking that caregiver has accepted
- Interview recordings, to anyone but Trust & Safety staff — never to the participants, and every view is audited

Never log health, disability, child, senior, home-address, background-check, or other sensitive personal information.

## Video Interviews

Families and caregivers meet on video inside GoSit, over LiveKit. Every interview is
recorded; joining requires agreeing to that, and a call whose recording cannot start does
not go ahead. Only a family can ask for one; the caregiver accepts or declines. Chat during
a call is ordinary messaging in the pair's conversation. Recordings are deleted after 90
days unless staff put the interview on hold. Nothing about an interview — no link, no
number, no meeting on another service — may be arranged off the platform.

## Medical Boundary

GoSit MVP is a **non-medical care marketplace**.

Do not implement regulated clinical acts, medication administration, nursing procedures, diagnosis, therapy, or other regulated healthcare services unless explicitly approved after legal/compliance review.

## Do Not

These are legal-positioning constraints, not style preferences — violating one in
documentation, product copy, or code comments creates a real liability and misclassification
risk. Do not:

- Describe caregivers as GoSit employees, or GoSit as supervising care the way an
  employer would.
- Describe GoSit as directly delivering care, or as a childcare/home-care agency.
- Say or imply GoSit guarantees caregiver performance or safety.
- Say GoSit provides or insures caregivers' liability, as if they were covered
  employees — caregivers maintain their own insurance (see "Insurance" above).
- Reintroduce an Uber-style caregiver master-insurance policy.
- Hard-code, or write documentation implying selection of, one screening vendor —
  none is selected; see `docs/TRUST_AND_SAFETY.md`.
- Claim a Vulnerable Sector Check is available nationwide from one private vendor.
- Store or document storing raw screening reports beyond what is operationally necessary
  — prefer verification summaries.
- Expose a care recipient's or pet owner's sensitive data publicly.
- Describe GoSit as providing regulated healthcare (see "Medical Boundary" above).
- Reference "11419056 Canada Inc." as if the Articles of Incorporation restrict it to
  babysitting, software, or any single line of business — they do not; there is no
  restriction on business activity in the current Articles.

## Figma

Existing important nodes:
- Landing Page `1:3`
- Login `49:8`
- Sign Up `77:12`
- Parent Dashboard `91:2`
- Search Dropdown `107:257`
- Inbox Empty `127:2`
- Inbox Messages `136:30`
- Message Content `139:56`
- Saved `129:4`
- Caregiver Inbox `140:103`
- Caregiver Stats `148:3`
- Caregiver Earnings `148:31`
- Caregiver Wallet `148:59`
- Caregiver Account `148:87`

Legacy Figma names such as Parent/Sitter/Babysitter are historical wireframe labels, not final domain constraints.

Expand Figma for senior care, special-needs support, care-recipient setup, service selection, caregiver qualification, and service-specific booking flows.
