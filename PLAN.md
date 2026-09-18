# PLAN.md — GoSit Product & Engineering Plan

## Goal

Build GoSit as a trusted multi-service care marketplace for:
- children
- seniors
- adults
- people requiring specialized non-medical support
- families arranging care on behalf of loved ones

## Positioning

> **GoSit — trusted care through every stage of family life.**

## MVP Care Verticals

1. Child Care
2. Senior Care
3. Specialized Support
4. Respite & Family Support
5. Pet Care — dog walking, drop-in visits, in-home pet sitting, boarding; built in Phase 3

GoSit is a marketplace connecting a `CareRequester` with an independent `Caregiver`, not
an employer or a home-care agency — see `docs/BUSINESS_MODEL.md` and
`docs/ADR-004-marketplace-provider-model.md`. Trust, screening, verification, and
insurance direction (including candidate screening vendors, none selected) are in
`docs/TRUST_AND_SAFETY.md`.

## Technology

- Next.js + React + TypeScript — web
- .NET MAUI Blazor Hybrid — iOS/Android
- ASP.NET Core Web API — backend
- PostgreSQL — transactional database
- Redis — caching
- Azure — cloud
- Azure DevOps — Scrum + CI/CD

## Phase 0 — Discovery & Product Definition

- [ ] Finalize MVP service taxonomy
- [ ] Finalize CareRequester journeys
- [ ] Finalize Child recipient journey
- [ ] Finalize Senior recipient journey
- [ ] Finalize special-needs/support journey
- [ ] Finalize Caregiver journey
- [ ] Finalize Admin/Support journey
- [ ] Define service qualification matrix
- [ ] Confirm non-medical/regulated-care boundary
- [ ] Research background-check/identity vendors
- [ ] Finalize payment/payout model
- [ ] Review privacy/legal/insurance requirements
- [ ] Expand Figma screen inventory
- [ ] Finalize design tokens
- [ ] Finalize ERD
- [ ] Draft OpenAPI v1
- [ ] Update Azure DevOps backlog

## Phase 1 — Foundation

- [x] Repository structure
- [x] ASP.NET Core solution
- [x] Next.js app
- [x] MAUI Blazor Hybrid app
- [x] PostgreSQL local environment
- [x] Redis local environment
- [x] Swagger/OpenAPI
- [x] Logging/tracing
- [x] Error handling
- [x] Azure DevOps pipelines
- [x] Bicep/Terraform baseline
- [x] Figma design system

## Phase 2 — Authentication & Onboarding

- [x] Register/login/logout/refresh
- [x] CareRequester profile
- [x] Caregiver profile
- [x] CareRecipient profiles
- [x] CareNeedsProfile
- [x] **Emergency contacts — now recordable, and disclosed to the caregiver.** The
      aggregate had `AddEmergencyContact` and `RemoveEmergencyContact` from the start, and
      the profile response carried an `emergencyContacts` array, and no endpoint could put
      anything in it — so the array was permanently empty for every family. The domain
      class said contact details are "released only to a caregiver on a confirmed booking",
      and nothing released them to a caregiver at all. Someone alone in a house with a
      child and an unanswered phone had the written emergency *instructions* and no person
      to call.

      Both halves now exist: the family records contacts on their account screen, and
      `GET /bookings/{id}/emergency-contacts` discloses them under the same four rules as
      the care-needs route — participant only, accepted or later, audited, and not-found
      rather than forbidden. A pending request discloses nothing, or a caregiver could
      harvest families' phone numbers by receiving requests they never intend to accept.

      Two deliberate differences from care needs. The disclosure stays open through
      `Completed`, because a caregiver may need to reach someone about what just happened
      and cutting them off at check-out would remove the number at the moment it is wanted.
      And rather than add a `FindByIdAsync` to the care-requester repository — whose whole
      design is that profiles are only ever loaded by user id, so a handler cannot reach a
      stranger's — the repository grew a narrow `ListEmergencyContactsAsync` that returns
      the contacts alone and cannot become a general profile read.
- [ ] Caregiver skills
- [ ] Caregiver services
- [x] Certifications — reference data, submission, review states
- [x] Availability
- [x] Verification status — read-only; Trust and Safety sets it in Phase 7

Domain vocabulary is settled in `docs/ADR-003-care-first-domain-vocabulary.md`.
Identity approach is in `docs/ADR-002-identity-approach.md`.

Carried forward:
- [x] **Email verification and password reset.** Single-use hashed tokens, request
      endpoints that reveal nothing about whether an account exists, superseding on
      re-request, and a reset that revokes every session and verifies the address.
      Not yet sent through Brevo — needs the SMTP key.
- [x] **Per-account lockout.** Ten consecutive failures lock an account, with the window
      escalating from a minute to an hour and always expiring. This closes the attack
      per-IP rate limiting cannot see: a distributed guess at one known address, a few
      tries from each of a thousand IPs.

      Two decisions carry the weight. It is temporary, because a lockout anyone can
      trigger by typing badly is also a way to lock a caregiver out an hour before a
      shift — permanent lockout would hand that lever to the attacker. And the endpoint
      gives nothing away: a locked account, a wrong password, and an address that was
      never registered are indistinguishable in status and body, or the protection would
      become a way to discover who has a GoSit account. The password reset clears a
      lockout, since it is the only route back someone can find unaided.

      The thresholds are engineering defaults; where the line sits between a legitimate
      person's inconvenience and an attacker's throughput is a security call.
- [x] **Sign out.** A form posting to `logoutAction` in the shared `DashboardShell`, so
      one control covers all 26 signed-in screens across the requester, caregiver and
      admin rails. Set apart from the navigation rail rather than listed in it — signing
      out is not a destination, and the session is revoked server-side, so a mis-tap
      costs a fresh login. Previously the action existed and nothing called it: there was
      no way to sign out of GoSit at all.
- [x] **Theme toggle.** Light, dark, or follow the device — one cycling control in the
      marketing header and the signed-in rail. The dark palette is now keyed on an
      explicit `data-theme` attribute rather than `prefers-color-scheme`, because
      someone on a light machine who wants dark has to be able to get it; an inline
      script resolves "system" before first paint, so nothing flashes. Trade-off stated
      in tokens.css: with JavaScript off, everyone gets light.
- [ ] Social sign-in — controls render on the auth screens but are disabled
- [ ] Onboarding and profile **screens** — no Figma nodes exist for these yet
- [x] **Certification document upload.** A caregiver uploads the certificate; staff read
      it through an authorised endpoint; and a certification with no document **cannot be
      approved**.

      This closed a safety hole rather than adding a feature. Staff were approving
      certifications — the thing that lets someone work with children and vulnerable
      adults — from a code and two dates the caregiver had typed, and the queue did not
      even show whether evidence existed. The Verified badge rested on self-declaration.

      Two things were wrong beyond the missing upload. `DocumentReference` was a free-text
      string the caregiver supplied, so one caregiver could cite another's record check;
      the key is now assigned by the store and the field is gone from the submit contract.
      And replacing a document after approval used to keep the approval, so a blurry photo
      could be swapped for anything — it now reopens the review.

      The admin screen shows the certificate behind an "Open the certificate" link and
      says plainly when none was uploaded, with approval disabled and the reason given —
      the server refuses either way, so the button only saves someone pressing it to be
      told no. The link goes through a Next route rather than to the API, because the
      staff token lives in an httpOnly cookie the browser cannot read; the route forwards
      the session and the API still authorises and audits.

      There is no method anywhere that returns a URL for one of these. Reads are streamed
      through the API so every one passes authorisation and is audited against the staff
      member; a shared access signature would be a bearer token for a vulnerable-sector
      check, valid for whoever ends up holding the link. Tested against real blob storage
      (Azurite in a container), not a stub.

## Phase 3 — Services, Search & Discovery

- [x] Service/category selection — 4 categories, 20 services, seeded reference data
- [x] Availability filter
- [x] Rate filter
- [x] Skills/certification filter
- [x] Verification filter
- [x] Caregiver detail
- [x] Favorites
- [x] Geographic search — city and postal-prefix filters, and distance ranking fed by
      Azure Maps geocoding on address save
- [x] **Pet care: dog walking, drop-in visits and in-home pet sitting.** A fifth category.
      Pets are their own entity on the family's profile rather than a new care-recipient
      type — a care recipient carries a date of birth, a care-needs record and the rules
      that protect children and vulnerable adults, none of which describes a dog. A service
      is for people or for pets and a booking names one or the other, never both. Pet care
      can be booked from 30 minutes; dog walking is offered for dogs only. One household
      per booking, so no group walks.

      Screening now follows what a caregiver offers: people care needs photo ID and a
      vulnerable sector check, pet care only needs photo ID and a criminal record check.
      That split is only safe because every eligibility check — search and booking — asks
      whether the caregiver is cleared for people, so a pet sitter who adds Babysitting to
      their profile still cannot be booked for a child. Everyone verified before the change
      passed a vulnerable sector check, and the migration marks them cleared for people;
      without that backfill every current caregiver would have become unbookable.
- [x] **A booking request no longer discloses the household.** The product told families
      "accepting shares their address", and the API sent the exact address and the
      family's notes with every request. A caregiver now gets the area — city, province,
      postal prefix — until they accept; a declined or lapsed request never discloses the
      rest. House sitting is what made this urgent: booking notes are where an owner types
      the door code and the dates the house will be empty.
- [x] **Pet boarding: a stay at the caregiver's home, priced per night.** Services now
      carry a pricing unit and a care location. Boarding is the one service at the
      caregiver's home, and a service for people cannot be configured there — care for
      people at a caregiver's home is the arrangement Ontario licenses as home child care.

      Caregivers set an optional nightly rate; without one they are not eligible for
      boarding. A stay is 1 to 30 nights, counted in Toronto local dates, and billed for the
      nights it actually lasted — at least one. It is not tested against the caregiver's
      weekly hours, which cannot express "hosting a dog all weekend"; the caregiver decides
      by accepting, and a stay still cannot overlap their other bookings. Check-in location
      is not compared, since the caregiver is at their own home.

      The address rule runs the other way. The booking carries the caregiver's address:
      theirs to see, and the family's once the caregiver accepts. The family's notes and pet
      care notes still reach the caregiver only on acceptance. The services screen tells a
      caregiver this before they tick boarding.

      Two things would have broken quietly. The overrun sweep closes hourly care fourteen
      hours after check-in, which would have closed every stay on its first evening — stays
      now overrun a day after scheduled pick-up. And the migration's default for the new
      enum columns was 0, which is neither pricing unit; existing rows now default to hourly,
      at the family's home.
- [x] **In-home pet sitting priced per night.** A sitter staying over at the family's home,
      1 to 30 nights, on the same nightly rate and stay rules as boarding — but at the
      family's address, so the usual rule holds: the sitter sees it, and the door code, only
      once they accept. Short evening sits are booked as drop-in visits, by the hour. A data
      migration switches the existing service row, since the seeder only inserts missing
      services; bookings already made keep the hourly price they were made at.
- [ ] One nightly rate covers both boarding and in-home sitting. A caregiver who would price
      hosting at home differently from staying at a family's home cannot yet.
- [ ] Nightly rate bounds ($10–$500) are sanity limits, not a pricing decision.

Backend complete and tested. **The screens are not built**: Figma nodes `91:2`
(Parent Dashboard), `107:257` (Search Dropdown) and `129:4` (Saved) could not be
inspected because the Figma MCP seat hit its tool-call limit, and CLAUDE.md requires
inspecting the node before implementing a screen. The web data layer
(`src/features/search`, `src/features/favorites`) is done and design-independent.

Open decisions:
- [x] **Geocoding via Azure Maps.** Addresses resolve on save behind `IGeocoder`, with a
      confidence floor so a guess is never stored, the key in a header rather than the
      URL, and an hourly backfill for anything that failed — the inline failure has no
      symptom otherwise.
- [ ] **Not yet called against a real Azure Maps account.** Needs a subscription key;
      the client is verified against a local stub. See docs/DEVELOPMENT.md.
- [ ] Review the per-service certification requirements in `ReferenceDataSeeder` —
      they are an engineering starting position, not a Trust and Safety determination
- [ ] Confirm caregiver search should stay authenticated-only rather than public/SEO

Fixed while demonstrating the screens:
- **"Add an address to your profile" was shown to people who had one.** The search
  response reported a single `distanceUnavailable` flag, which could not tell "no address"
  from "we could not place the address you gave" — so the dashboard sent someone hunting
  for a field they had already filled in. The response now carries a reason, and the
  screen says something true for each of the three.

MVP ranking:
1. Service eligibility
2. Verification
3. Distance
4. Availability
5. Rating
6. Experience
7. Price

## Phase 4 — Care Requests & Bookings

- [x] Create care request — a booking in `Pending`; open/broadcast requests are post-MVP
- [x] Select recipient(s) — ownership and service/age fit both enforced
- [x] Select service
- [x] Date/time/location
- [x] Care-needs summary — booking lists recipients and whether needs exist, not content
- [x] Qualification validation — checked at request **and** re-checked at accept
- [x] Rate snapshot — server-computed; the client never sends a price
- [x] Accept/decline
- [x] Cancel — either party; canceller and late-window flag recorded
- [x] Check-in
- [x] Check-out — completes the booking and bills hours actually worked
- [x] Complete — reached by check-out, or by the platform sweep when nobody checks out
- [x] Auto-complete for bookings a caregiver never checks out of — `BookingSweeper`
- [x] Unanswered requests — cancelled by `CancellationParty.Platform` once the start passes

Also delivered: `AuditEvent`, `OutboxMessage`, and the outbox pattern writing events in
the same transaction as the state change, drained by a hosted publisher; and
`BookingSweeper`, a hosted service that closes the two states no participant can leave
on their own.

Open items carried forward:
- [x] **Overnight bookings.** The weekly schedule now treats the week as a circle: an
      availability window whose end is earlier in the day than its start runs past
      midnight, and a booking is matched as an interval against it. This closed a broken
      promise rather than adding a feature — "Overnight Child Care" has been in the seeded
      service catalogue since day one and could never be booked, and the refusal said
      "this caregiver is not available at that time", blaming the caregiver for a gap in
      our own model.

      The same defect ran through the stack: the availability editor and the booking form
      both refused an end before a start, and the booking form's estimate showed $0.00 for
      an overnight. The caregiver's editor now labels a wrapping window "Overnight" as it
      is typed, so nobody has to guess whether they have said overnight or made a typo.

      Two rules survive the change, both tested: a window past the end of a caregiver's
      night is still refused, and a daytime schedule does not wrap round to cover the
      small hours
- [ ] Per-caregiver time zone; availability currently assumes America/Toronto
- [ ] Azure Service Bus transport for the outbox (a logging publisher stands in)
- [x] **Payouts wait out the dispute window.** A payout is now held until the family can
      no longer dispute the booking, and an open dispute holds it indefinitely.

      This was a money-ordering flaw rather than a missing feature. Payouts went out
      within fifteen minutes of the payment settling, while a family had fourteen days to
      dispute — so by the time anyone disputed, the money was already with the caregiver
      and there was nothing to refund from. The dispute window existed and could not be
      honoured.

      **OPEN BUSINESS DECISION — how long caregivers wait.** The hold is the full
      fourteen-day dispute window, which is the safe default and a fortnight's wait for
      someone doing shift work. Shortening it means either a shorter dispute window or
      accepting that some disputes arrive after the money has gone. That is a cash-flow
      and risk call, not an engineering one. Both figures live on `BookingPolicy`.

      Money already handed to the provider cannot be held — that is a reversal, a
      different operation. A dispute raised at that point is still recorded; it just
      cannot stop a transfer that has left.
- [x] **Dispute resolution.** `GET /api/v1/admin/disputes` and
      `POST .../{id}/resolve`, behind AdminOnly and audited.

      This finishes what holding payouts started. A disputed booking appeared on no staff
      screen at all — not the incident queue, not anywhere — while holding a caregiver's
      money indefinitely. Nobody could see it and nobody could end it.

      The decision is one number: how much of what the family paid goes back to them.
      Everything else follows. Nothing refunded releases the payout in full; everything
      refunded cancels it; in between, the payout is recomputed on what the family kept
      paying for — **including GoSit's commission**, because the platform should not keep
      its cut of money it handed back.

      The refund is idempotent on the booking id, so a staff member pressing twice or a
      retry after a timeout cannot refund a family twice. A resolution note is required:
      both parties are told the outcome, and "your dispute was closed" with no reason is
      worse than no message.

      The booking stays `Disputed` rather than reverting to `Completed` — it was
      disputed, and a status that erased that would hide the one thing anyone looking back
      needs to know.

      The **/admin/disputes screen** shows each dispute with what the family paid, what the
      caregiver is owed, and — as the amount is typed — what that answer actually does:
      "the dispute is dismissed and Priya is paid in full", or "the family gets $60 back;
      Priya's payout is recalculated on the $60 they keep". A number in a box is not a
      decision anyone can check.

      Still open: telling both parties the outcome. The notes are recorded and required,
      and nothing sends them yet.

## Phase 5 — Messaging & Notifications

- [x] Conversations — one per requester/caregiver pair; only a requester can start one
- [x] Messages — with contact-sharing flagged for review, not blocked
- [x] Unread/read state — a timestamp per participant
- [x] SignalR — `/hubs/messaging`, authorized, grouped per user
- [x] In-app notifications — summaries only, never message content
- [x] **Notification email.** Brevo SMTP behind `IEmailSender`, dispatched out of band by
      `NotificationEmailDispatcher` with backoff. Sent, suppressed and gave-up stay three
      distinguishable states, so the record never claims someone was told something they
      were not. Emails carry no more than the notification did.
- [ ] **No email has actually been sent through Brevo.** Needs the SMTP key, and the
      sending machine's IP on Brevo's allowlist — the account has SMTP IP restriction
      switched on. Consider Brevo's HTTP API instead for deployment; see
      docs/DEVELOPMENT.md.
- [x] **Booking notifications.** `NotificationType` had declared BookingRequested,
      Accepted, Declined, Cancelled and Completed since this phase, and not one of them was
      ever raised — only messaging created a notification. A caregiver was never told a
      family had booked them, and a family whose caregiver cancelled the night before found
      out by opening the app. `BookingNotifier` now raises all of them, plus
      BookingDisputed and DisputeResolved, from the transition handlers themselves.
      Whoever performed the transition is not notified about their own action; a
      cancellation goes to whichever side did not cancel; a dispute goes to both, because
      it stops the caregiver's payout and money that silently stops arriving reads as a bug.
      No notification carries the reason, the decline note or the dispute's contents —
      they travel by email and land on lock screens.
- [x] **A settled dispute reaches the two people waiting.** The admin form makes the
      resolution note mandatory and told the staff member that both parties would read it;
      the note was recorded, carried by no response, and read by nobody. `BookingResponse`
      now exposes it to both sides, one `DisputeOutcome` component renders the same words
      on the family's and the caregiver's booking, and the notification says only that a
      decision was made and where to read it.
- [x] **Video interviews, on GoSit, recorded.** Meeting a caregiver before booking is
      where care most often left the platform — a phone number, a link to someone else's
      video service. It now happens here. A family proposes a time (15 to 60 minutes, an
      hour's notice, up to 60 days out) from a caregiver's profile or their conversation;
      the caregiver accepts or declines; both join from ten minutes before. The call runs
      on LiveKit with microphone, camera and leave — no screen sharing, which is the easy
      way to hold a phone number up to a camera.

      Every interview is recorded, and the order of operations is the guarantee: consent
      and the join are saved, the recording is confirmed running or started, and only then
      is a token issued. A recording that cannot start means no token; one that fails
      mid-call closes the room. Two people joining at once start one recording, not two,
      by a conditional lease on the interview row. Chat during the call is a GoSit
      message in the pair's conversation — same record, same flagging, same moderation —
      and the video room's data channel carries only a "reload" nudge, never the text.

      Recordings go straight from LiveKit to a private Azure container of their own. Only
      Trust & Safety staff can watch one, through the API, with an audit row saved before
      the first byte; participants are never given a link. They are deleted after 90 days
      by a sweep unless staff put the interview on hold for a dispute or incident. A
      participant's own view of an interview says nothing about where a recording is.
- [ ] Interviews need a LiveKit project and a real Azure Storage account (LiveKit Cloud
      cannot write to Azurite). Unconfigured, requests are refused with "not available yet".
      The webhook URL must be registered in the LiveKit project: `/api/v1/webhooks/livekit`.
- [ ] Recording playback streams from the start; seeking needs range requests on the
      streaming route, which are not built.
- [ ] Interviews are web-only. The mobile app does not join calls yet.
- [ ] The recording notice and consent wording, and the 90-day retention, need legal review
      (see the legal pack, privacy questions).
- [ ] Push notifications — needs the MAUI work in Phase 10 and a provider decision

Backend complete and tested. **The screens are not built**: Figma nodes `127:2`
(Inbox Empty), `136:30` (Inbox Messages), `139:56` (Message Content) and `140:103`
(Caregiver Inbox) could not be inspected — the Figma MCP seat is still at its tool-call
limit, and CLAUDE.md requires inspecting the node before implementing a screen.

Open decisions:
- [ ] Email and push providers, and which notification types warrant each
- [ ] Whether a booking should get its own thread rather than sharing the pair thread
- [x] Moderation workflow for messages flagged by `ContactDetailPolicy` — built in Phase 7;
      see the moderation queue there

## Phase 6 — Payments & Payouts

- [x] Stripe payment intent — amount derived server-side; the client never sends a price
- [x] Webhooks — signature-verified, replay-protected, deduplicated by provider event id
- [x] Refunds — full and partial, reconciled against the provider's running total
- [x] Receipts — visible to both participants, carrying no card or provider secret
- [x] Caregiver earnings — paid, pending, lifetime gross and fees
- [x] Wallet — earnings summary per caregiver
- [x] Payouts — queued only after the payment settles; commission rate captured per payout
- [x] Statements — per-booking lines showing gross, fee, net, and rate

### OPEN BUSINESS DECISION — commission rate

`PlatformFeePolicy.DefaultCommissionRate` is **15%, a placeholder chosen to make the code
work, not an agreed price**. It determines what caregivers actually earn and needs a
decision from whoever owns pricing before launch. It is configurable via
`Payments:CommissionRate`, and each payout captures the rate in force when it was created,
so changing it never alters an already-earned payout.

Open items carried forward:
- [x] **Stripe Connect onboarding and the payout sweep.** Express accounts with hosted
      onboarding, `account.updated` to learn when an account is ready, and a sweep that
      sends queued and failed payouts with exponential backoff. Money goes only to an
      account the provider says is ready; the idempotency key is the payout id so a
      crash cannot pay twice; only `transfer.paid` marks a payout settled.
- [ ] **Connect has never run against a real Stripe account.** Same gap as subscription
      billing — needs a test-mode key. Also needs the `transfers` capability enabled on
      the platform account, and an available balance (test card `4000000000000077`).
- [ ] Verify the Stripe path against a real test-mode account with the Stripe CLI; only
      the webhook parser is currently exercised by tests
- [ ] Cancellation fees — `IsLateCancellation` is recorded on bookings but nothing
      charges for it yet
- [ ] Statement PDFs (listed as "Should Have" in the MVP scope)
- [ ] Tax handling (GST/HST) — not modelled at all; needs a finance decision

## Phase 7 — Trust, Safety & Reviews

- [x] Identity verification — `VerificationCase`; staff decision only, caregivers cannot
      approve their own
- [x] Background checks — workflow and `IBackgroundCheckProvider` abstraction; **no
      vendor selected**, so cases await a manual decision
- [x] Certification expiry — `IsValidOn` already gates eligibility (Phase 2); verification
      approvals now carry their own expiry too
- [x] Service-specific qualification — `IsEligibleFor`, checked at request and re-checked
      at accept (Phase 4)
- [x] Verified badges — requires **every** check in `RequiredForListing`, not just one
- [x] Two-way reviews — blind until both submit or the window closes; ratings feed search
- [x] User reporting — open to any signed-in user
- [x] Incident workflow — triage queue, safety concerns prioritised, closure requires an
      explanation
- [x] Account suspension — revokes every refresh token; admins cannot suspend themselves
- [x] Audit events — already written on every booking transition (Phase 4); now also on
      verification decisions, suspensions, incident closures, and reviews

### OPEN DECISIONS

- **Background-check vendor.** Nothing is wired up. Until one is chosen, "verified"
  means a staff member looked at an uploaded document — which is a materially weaker
  guarantee than a real criminal record check, and should not be marketed as one.
  Candidate vendors (none selected) and the provider-abstraction approach are documented
  in `docs/TRUST_AND_SAFETY.md`.
- **Caregiver insurance.** Direction from the founder's lawyer: caregivers carry their
  own insurance; GoSit does not insure them. A $2M liability baseline is a planning
  assumption only, unconfirmed by counsel or a broker — see `docs/BUSINESS_MODEL.md`.
  **Built**: `CaregiverInsurancePolicy` (submit, upload proof, staff approve/reject,
  30-day and lapsed expiry notices) — mirrors `CaregiverCertification` exactly. See
  `docs/TRUST_AND_SAFETY.md`. **Not built**: any per-service insurance *requirement* — a
  caregiver can be fully listable with no insurance on file, pending the
  coverage-per-service decision.
- **Caregiver Service Agreement digital signature.** Built: a typed-name electronic
  signature (`CaregiverProfile.AcceptServiceAgreement`), gating both `IsListable` and
  `CareBooking`'s own booking-eligibility check. `POST
  /api/v1/caregivers/me/service-agreement/accept`. The agreement text itself is still an
  unreviewed draft (`docs/legal/Go-Sit-Caregiver-Service-Agreement-DRAFT.docx`) — see
  `docs/TRUST_AND_SAFETY.md`.
- **Who counts as staff.** Admin accounts cannot be self-registered, but there is no
  provisioning flow either. Someone has to decide how the first admin is created in a
  deployed environment.
- **Auto-suspension policy.** A critical safety report currently flags for urgent review
  but does not suspend anyone automatically. That is deliberate — an automatic suspension
  on an unverified accusation is its own harm — but it needs a documented policy.

Open items carried forward:
- [x] **Certification-expiry notifications.** `NotificationType.CertificationExpiring`
      existed and nothing raised it, while expiry was fully enforced: a lapsed certificate
      drops a caregiver out of search for every service requiring it and stops them
      accepting a booking. The whole thing happened in silence — their work stopped
      arriving and nothing connected the two for them. `CertificationExpirySweeper` now
      warns 30 days out and again on the day it lapses, naming the certificate, the date,
      and the services it costs them. The stage is recorded on the certification so a
      six-hourly sweep says each thing once. The caregiver's own screen was showing an
      expired certificate as a muted grey pill; it now reads as the loss it is, and a
      valid certificate inside the window shows the countdown.
- [x] **Moderation queue for messages flagged by `ContactDetailPolicy`.** Messages have
      been flagged since Phase 5 and the schema carried a partial index built for this
      queue; nothing ever read the flag but a log line. The policy is deliberately loose
      because "a false positive costs a review" — there was no review, so neither a false
      positive nor a real one cost anything.

      Shaped throughout by the fact that this is the one staff surface that reads private
      messages. The queue carries **no message bodies**: it shows who, which direction, and
      when. Reading one is a separate request that writes an audit row naming the staff
      member before the body is returned, and the screen says so before anyone clicks.
      Settling is either "nothing wrong" or an escalation, which raises an ordinary
      `IncidentReport` about the sender rather than inventing a second place where
      decisions about someone's account get made; the staff note is the description and
      the message is not copied into it. The flag is never unset — whether a message looked
      like contact sharing is a fact, whether it mattered is a judgement, and overwriting
      the first with the second would lose the evidence.
- [ ] Appeals process for a rejected verification or a suspended account
- [x] **Review moderation.** A published review was permanent: nothing could take one
      down and nothing could tell staff about it. A caregiver's rating is how they get
      work, so a retaliatory one-star after a dispute, a comment naming a child, or a slur
      reached nobody who could act — `POST /reports` reports a *user*, which leaves the
      words on the profile.

      Both halves shipped together, because a takedown nobody can ask for only helps the
      people staff happen to notice, and a report queue nothing can act on is the dispute
      queue before it had a screen. The subject — and only the subject — reports a review
      about them; staff either hide it or let it stand.

      Hiding removes it from the profile **and recomputes the rating**. Without the second
      half the words would vanish while the one star went on dragging the average down,
      which removes only the evidence. The review is never deleted or edited: it is
      evidence about its author as much as its subject, and `PublishedAtUtc` is left alone
      because clearing it would make a decided review look like one still waiting on the
      other side and the publication sweep would put it straight back.

      Letting it stand is the plain button on the staff screen and reporting is a small
      text link on the caregiver's, both deliberately: a queue full of "three stars is
      unfair" is a queue where the review naming a child waits behind forty others.
- [ ] Police-check expiry rules per Ontario requirements (a compliance question)

## Phase 8 — Subscription & Growth

- [x] Free/Plus requester plans — entitlements resolved server-side only
- [x] Caregiver premium features — Pro plan; feature codes gate behaviour
- [x] Referrals — qualify on a completed booking, not on signup. Qualification now
      actually fires: `BookingCompletionEffects` runs on both routes to `Completed`
      (check-out and the platform sweep), so a caregiver forgetting to check out cannot
      cost a family their referral. Previously `ReferralQualifier` was registered but
      had no caller, and every invitation stayed `Pending` forever.
- [x] Promo codes — expiry, redemption cap, audience, one use per account
- [x] Analytics — `BusinessMetrics`, derived from domain events, no personal data
- [ ] SEO service/location pages — **not built**; needs design and a web app that has
      caught up (see the UI gap below)

### OPEN BUSINESS DECISIONS

- **Plan pricing and packaging.** `SubscriptionPlanSeed` ships Plus at 12/month and Pro
  at 19/month with a placeholder feature split. Both the prices and which features sit
  behind them are product decisions, not engineering ones.
- **Whether these are the right features at all.** `SubscriptionFeature` defines six
  codes so entitlement checks resolve against something concrete. Only
  `REDUCED_COMMISSION` has any real consequence today, and even that is not yet applied
  by `PlatformFeePolicy` — see below.
- **Referral reward.** `Referral.Reward` records an amount, but nothing decides what it
  should be or grants it. There is no reward-granting flow.

Open items carried forward:
- [x] **Subscription billing is wired to Stripe.** Hosted Checkout plus Stripe Billing,
      behind `ISubscriptionBillingProvider`. With billing configured, subscribing grants
      nothing and returns a checkout URL; the plan becomes real only on a signed webhook.
      Renewals, failed payments and cancellations all flow from the provider.
- [ ] **Not yet verified against a real Stripe account.** Everything automatable is
      covered — including the real SDK's outgoing requests, asserted against a local
      stub — but nobody has put a card through. Needs a test-mode key, then
      `scripts/setup-stripe-test.ps1` and `scripts/verify-stripe-e2e.ps1`. Creating the
      Stripe account and supplying the key are the two steps that need a person.
- [x] **Five of the six feature codes are now enforced**, each on the server, from the
      stored subscription:
      `UNLIMITED_FAVOURITES` caps the free plan in `AddFavoriteHandler`;
      `ADVANCED_SEARCH_FILTERS` refuses the certification and experience filters —
      refused, not silently dropped, so unfiltered results are never shown as filtered;
      `PROFILE_PROMOTION` ranks a paid caregiver ahead of equals *within a distance
      band*, never above eligibility, verification or proximity;
      `PRIORITY_BOOKING_REQUESTS` reorders a caregiver's pending requests only, leaving
      accepted work in schedule order;
      `REDUCED_COMMISSION` is resolved in `PayoutFactory` and stamped on the payout, so
      a later plan change cannot rewrite what someone earned for work already done.
      The numbers behind the codes live in `EntitlementPolicy` and remain placeholders.
      `REDUCED_COMMISSION` is now also proven through the webhook path that creates a
      payout, not only in the fee policy: the policy could be correct while the lookup
      feeding it answered false for everyone, and the only symptom would be caregivers
      quietly paying full commission on a plan bought to avoid it.
- [x] **`CAREGIVER_INSIGHTS` now has something to gate.** `GET /api/v1/caregivers/me/insights`
      plus a `/caregiver/insights` screen: requests received, reply and acceptance rates,
      typical time to accept, completed work, and families who came back. Every rate is
      null rather than zero when there is nothing to divide by, and a request the platform
      closed unanswered is counted as its own outcome rather than as a decline — telling a
      caregiver they turned down work they never saw would be worse than showing nothing.
      Figma node `148:3` was still not inspectable (seat at its tool-call limit), so the
      layout follows the caregiver screens already in the repo.
- [x] **Search performance is measured.** `caregiver_exposures` counts, per caregiver per
      day, how often they appeared on a page of results someone was shown and how often
      the profile was opened; both feed the insights screen over the same ninety-day
      window as the booking figures.

      The shape is the privacy decision. A row is (caregiver, day, two counts) and
      carries **no searcher** — the obvious design, a row per view naming who looked,
      would be a standing record of which families are considering which caregivers,
      which on a care marketplace is close to a record of who needs help with a child or
      a parent. A test asserts three different families searching leave one row, so it
      fails the moment the table stops being anonymous.

      Counts are advanced by an `ON CONFLICT DO UPDATE` upsert rather than through the
      change tracker: concurrent searches would otherwise lose increments on a contended
      row, and one statement covers a whole page. Recording runs inline and every failure
      is swallowed — a statistics row must never stop a family seeing caregivers.
- [x] **Exposure retention.** `ExposureRetentionSweeper` deletes counts older than
      `Insights:ExposureRetention:Days`, daily, in batches so the first sweep after a gap
      does not lock a table every search writes to. The rows are already anonymous, so
      this is the other half of the same decision rather than damage control: data that
      exists can be subpoenaed, breached or repurposed, and the cheapest protection for a
      count nobody needs is not to have it.
- [ ] The retention period itself — 180 days by default, twice the reported window — is a
      placeholder like the other numbers of its kind. It is floored at 90 by a test, since
      anything less would have the sweeper deleting data the screen is still reading.
- [ ] Promo codes apply to subscriptions only; applying one to a booking needs a change
      in `CreatePaymentIntent`, where the amount is derived strictly from the booking
- [x] **Admin promo-code surface.** `GET`/`POST /api/v1/admin/promo-codes` and
      `POST .../{id}/deactivate`, behind AdminOnly and audited, with a
      `/admin/promo-codes` screen: create a code, see what each campaign has cost
      against its cap, switch one off. Deliberately no edit — a code that has been handed
      out carries an offer already made to everyone holding it, and changing the terms
      afterwards penalises whoever was slowest to redeem. Deactivation is not deletion
      either: redemptions point at the code, and a campaign that cost money stays in the
      record. The listing reports a status rather than a bare active flag, because a code
      stops working four different ways and "active" on an expired one is a lie.

Fixed while closing out enforcement:
- **The free favourite cap was refused in words nobody saw.** The API returns a 409 whose
  title names the limit and what lifts it; the web action collapsed every non-404 into
  "we could not update your saved caregivers", turning a rule someone can act on into
  what reads as a bug. The refusal now reaches the person who hit it.
- **`revalidatePath("/search")` pointed at a route that does not exist** — the search
  lives at `/dashboard`, so saving a caregiver left a stale heart on the screen it was
  pressed from.

### The UI gap

Figma's MCP tool-call limit still blocks node inspection. The product owner has
authorised designing screens in-repo where no wireframe exists, so the UI is being built
against the wireframes' visual language and the design tokens rather than exported specs.

Screens are marked below with their source: **[F]** a Figma wireframe informed it,
**[D]** designed in-repo.

Built:
- [x] Landing page — placeholder, not the designed node `1:3`
- [x] Log in `49:8` **[F]**
- [x] Sign up `77:12` **[F]**
- [x] Find care / dashboard — layout from `91:2`, cards and filters **[D]**
- [x] Caregiver profile **[D]**
- [x] Saved `129:4` **[F]** layout, card **[D]**
- [x] Bookings list and booking detail **[D]** — Upcoming/Past tabs; detail carries
      cancel, pay and review
- [x] Create-booking flow **[D]** — guards on profile, address and recipients; only
      offers services the caregiver is certified for
- [x] Account and profile management **[D]**
- [x] Care recipients and care needs **[D]** — the most privacy-sensitive screens

- [x] Messages / inbox `127:2`, `136:30`, `139:56` **[F]** layout, designed in-repo —
      Figma's tool-call limit still blocks node inspection, so these follow the
      wireframes' visual language rather than exported specs
- [x] Caregiver profile actions — "Request a booking" and "Message", both guarded on the
      requester having a profile; before this the booking flow was reachable only by URL

- [x] Caregiver side — Today `148:3`, Work list and job detail, Earnings and wallet
      `148:31`/`148:59` merged into one screen, Account `148:87`, inbox `140:103`
- [x] Role-aware routing — `src/proxy.ts` keeps each side on its own screens before a
      page renders; route-group layouts repeat the check as a backstop

- [x] Admin portal — overview, background-check queue, certificate queue, incident
      triage with suspend and reinstate
- [x] Landing page — designed in-repo; node `1:3` is still not inspectable on this seat
- [x] **Photography.** Five Unsplash photographs in `public/images`, self-hosted rather
      than hotlinked, each checked for `premium`/`plus` first — Unsplash+ and Getty
      images on the same search results carry a paid licence and none are used.
      `public/images/CREDITS.md` records photographer, source and the rule that matters:
      nobody in these photographs is a GoSit caregiver, so none may be captioned as one
      or placed beside a testimonial. The licence covers the photograph, not the likeness.
- [x] **A working search box on the hero**, in the care.com pattern. It cannot show
      results — caregiver search is authenticated — so it carries the postal code through
      sign-up and seeds the search someone lands on. Validated server-side before it goes
      anywhere near a redirect.
- [x] Visual refresh — see below
- [x] **Theme toggle.** Light, dark, or follow the device, in both the marketing header
      and the signed-in rail. The dark palette used to hang off `prefers-color-scheme`
      alone, so dark on a light machine — or light on a dark one — was unreachable. It is
      now keyed on `data-theme`, written before first paint by an inline script that
      resolves "follow the device" against the OS. Following the OS is still the default;
      it just arrives by a route that an explicit choice can override. With JavaScript
      disabled the attribute is never written and everyone gets the light theme, which is
      a preference lost rather than a function.

- [x] Reviews — on the caregiver's public profile, and a caregiver's own `/caregiver/reviews`
- [x] Referrals — code, invitation list and code entry, on both sides
- [x] Plans — plan picker on both sides, **labelled a preview**; see below

Every destination in all three navigation rails now resolves, and every role has a
surface.

### The plan screens are honest about not working

Building these surfaced how little of Phase 8 is real, and the screens say so rather
than implying otherwise:

- ~~**No subscription feature is enforced anywhere.**~~ Five of the six are now enforced
  server-side — see Phase 8. `CAREGIVER_INSIGHTS` still is not, because the capability
  it names does not exist. The plan screen still describes what a plan *includes*, and
  its "not yet applied anywhere" notice now overstates the gap; it needs rewording to
  name the one feature that is still inert rather than implying all six are.
- ~~**Nothing is charged and nothing renews.**~~ Stripe is now wired up. With
  `Stripe:SecretKey` and the plan prices configured, switching plan sends someone to
  hosted checkout and grants nothing until payment is confirmed. With no key — local
  development — the old behaviour remains and the screen still says so.
- **Referral rewards are not granted.** Qualification fires now, so invitations do reach
  `Qualified` — but `Referral.Reward` records an amount nothing decides or grants, and
  `Rewarded` is a state no code path reaches. The referral screen promises no figure and
  now says outright that nothing has been paid out, rather than leaving a "Rewarded"
  column reading like a payment on its way.

A pricing page that omitted any of this would take a decision from someone by describing
benefits that do not exist. The disclosure sits at the top of the screen, not in small
print, and there is an e2e test asserting it stays there.

**These remain open business decisions** (unchanged from Phase 8): plan prices, which
features sit behind them, and the referral reward. Engineering cannot answer them.

Fixed while building: `GetMySubscriptionHandler` fell back to the plan *code* when there
was no live subscription, putting "FREE_REQUESTER" on screen where a person expects
"GoSit".

### Visual refresh

The product owner asked for a more modern, public-facing look. Because every screen
already referenced semantic tokens and shared primitives, this was largely a change to
`src/styles/tokens.css` plus the primitives, rather than a sweep through the screens.

What changed:
- **Two fonts, not three.** Plus Jakarta Sans for headings, Inter for everything else.
  Roboto + Raleway + Open Sans gave one product three voices.
- **Warm neutrals** in place of pure greys — the difference between "hospital" and
  "someone's home" for a care product.
- **A full brand ramp** so tints carry meaning without opacity hacks.
- **Layered, brand-tinted shadows** replacing the single 30%-black drop shadow.
- **A dark theme**, which the product had none of.
- **Contrast measured, not eyeballed.** `--color-text-muted` was failing AA at 3.32:1
  and was darkened until it passed; every sampled pair now clears 4.5:1 in both themes.

Two things that did not survive the refresh, deliberately: the auth screens' decorative
crossing bars (ornament costs credibility on a screen whose job is "does this look like
somewhere I would type my password") and placeholder-as-label, which vanishes exactly
when someone wants to check what a field was for.

### Fixed while building the admin portal

Three defects that made a staff portal impossible rather than merely awkward:

- **No admin account could exist.** Self-registration is correctly refused, but nothing
  could create one either, so every staff route was unreachable and verification
  approval was a SQL statement. Now provisioned from configuration — see
  `docs/DEVELOPMENT.md` for the safeguards.
- **No route could approve a certification.** The domain had `ApproveCertification` and
  `RejectCertification` all along with nothing calling them, so every service with a
  certification requirement was unbookable in practice.
- **The staff queues returned GUIDs, not people.** `PendingVerification` named a profile
  id; `AdminIncident` named two user ids. Nobody can decide whether to let a stranger
  into a family's home from a GUID.

Also fixed on the way: `ICaregiverProfileRepository.FindByIdAsync` did not load
certifications, so the aggregate reported a certificate missing when staff tried to
decide on it.

### Gaps the booking UI exposed in the API

Found while walking the flow end to end; none are blockers for the screens, all are real:
- **`PUT /caregivers/me/availability` with a wrong-shaped body returns 200 and clears the
  schedule.** The controller substitutes `[]` for a missing `Windows`, so a client typo
  silently costs a caregiver every slot. Defensible as PUT-replace semantics; still worth
  a guard.
- **Subscription plans are not honoured anywhere in booking or search** — carried forward
  from Phase 8 above.

### Fixed while building the caregiver side

Three defects the caregiver screens could not have been built around:

- **No caregiver could ever become listable.** `AcceptingWork` was exposed read-only with
  no endpoint to set it, and `IsListable` requires it. `PUT /caregivers/me/accepting-work`
  now exists; opening requires verification, closing never does. The search fixtures had
  hidden this by calling the domain directly — `CaregiverAcceptingWorkTests` drives HTTP
  alone so it cannot happen again.
- **A caregiver could not read the care needs they were booked for.** Now
  `GET /bookings/{id}/recipients/{recipientId}/care-needs`, disclosed only once the
  booking is accepted, only to a participant, only for a recipient named on it, and
  audited on every read. Seven tests in `BookingCareNeedsTests` cover each way it could
  leak.
- **A booking named only the caregiver.** `BookingResponse` now carries the requester's
  first name and last initial too — the same shape both sides get of each other.

### What the messaging UI exposed

- **`ContactDetailPolicy` flags but does not block**, by design and for a documented
  reason. The UI must not claim contact details are "not allowed" — it says why keeping
  arrangements on GoSit matters instead. Nothing surfaces the flag to a moderator yet;
  that queue is still the open Phase 5/7 item.
- **A caregiver cannot start a conversation.** `POST /conversations` is
  `CareRequesterOnly`, so a caregiver with something to raise before a booking has no
  route to raise it. Deliberate, but worth revisiting.
- **No unread badge in the navigation rail.** `NavItem.badge` exists and the count is
  available, but the rail is a shared constant rendered without a fetch. Wiring it needs
  the count resolved per layout render.

## Suggested Sprint Sequence

### Sprint 0
Discovery, architecture, service taxonomy, qualification matrix, Figma design system, infrastructure baseline.

### Sprint 1
Identity and authentication.

### Sprint 2
CareRequester, CareRecipient, CareNeeds, Emergency Contacts.

### Sprint 3
Caregiver profile, skills, services, certifications, availability.

### Sprint 4
Service selection, search, caregiver detail, favorites.

### Sprint 5
Care request and booking.

### Sprint 6
Messaging and notifications.

### Sprint 7
Payments.

### Sprint 8
Wallet and payouts.

### Sprint 9
Trust, safety, reviews, service qualification.

### Sprint 10
Mobile-native functionality and check-in/out.

**Started with the API half.** Check-in and check-out now accept an optional device
location and record whether the caregiver was at the booking address — as a *status and a
distance, never a coordinate*, because a dispute needs "they were at the home", not a fix
on a person. It never refuses: a phone that cannot locate itself, declines the permission,
or reports nonsense still checks in, and the booking says a location was not provided.
Refusing would leave someone at a family's door unable to start work, and would fall
hardest on whoever has the worst handset.

The web app sends the browser's location where it can.

**The MAUI app is now a real app.** `GoSit.Mobile.UI` holds the screens — sign in, Today,
Work, job detail with check-in/out, Earnings, Account — behind a bottom tab bar, styled
with the web app's own design tokens so the two clients are visibly one product. The API
client refreshes an expired access token once, single-flight, and signs out rather than
retrying a rejected refresh, because the API treats a replayed refresh token as theft.
Tokens live in platform secure storage.

Platform gaps found and fixed while wiring it up: Android declared no location permission
at all, iOS had no `NSLocationWhenInUseUsageDescription` (which terminates the app on the
first request rather than prompting), and the location service never asked for the
permission it needed. It now asks at the moment of check-in, where the reason is on the
screen behind the dialog.

**Run on the Windows head by the product owner**, who confirmed sign-in, the job list,
check-in and check-out, and the earnings statement all work against the live API. That
also verified the sprint's headline feature on real hardware: a booking was checked out
from the app, and the reduced 10% commission on the resulting payout confirmed the Pro
plan's REDUCED_COMMISSION end to end.

Four defects came out of those first two runs, none of which a build would have caught:
every font fell back to a serif (tokens.css references next/font's build-time variables,
so the copy brought the references and none of the values — the Latin subsets are now
bundled, ~75 KB); a focus ring drawn around each page heading by FocusOnNavigate read as a
stray black box; the status pill wrapped to two lines; and the payout status showed the
database's own word, "Queued", on the one screen whose only question is "have I been
paid".

**Still unverified: iOS and Android.** They compile; nothing has run on either.

An attempt was made to host the Razor components in a throwaway Blazor Server app to
screenshot them. It got as far as rendering the sign-in screen — which caught a real bug,
an `EditForm` `FormName` that silently stopped the inputs binding — before its static-asset
pipeline refused to serve the Blazor runtime. Worth another try when someone has a device,
at which point it is unnecessary.

Still open in this sprint: push notifications, which need a provider decision, and a
signed-in look at any of these screens on real hardware.

### Sprint 11
Beta hardening.

## Immediate Next Actions

1. Expand Figma for senior and specialized care, plus the trust/insurance/pet-care
   screens listed in `docs/FIGMA_HANDOFF.md`.
2. Finalize service taxonomy.
3. Define qualification requirements per service, including caregiver insurance where
   required (see `docs/TRUST_AND_SAFETY.md`).
4. Update ERD.
5. Update OpenAPI.
6. Update Azure DevOps backlog.
7. Evaluate screening/verification vendor candidates (`docs/TRUST_AND_SAFETY.md`) and
   confirm insurance requirements with counsel and a broker (`docs/BUSINESS_MODEL.md`)
   before building the verification/insurance features they gate.
8. Scaffold code only after those are stable.
