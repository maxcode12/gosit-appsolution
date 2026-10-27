# DEVELOPMENT.md — Local Setup

## Prerequisites

| Tool | Version used |
|---|---|
| .NET SDK | 10.0.x |
| Node.js | 22.x or later |
| Docker Desktop | required for local infra and integration tests |
| .NET MAUI workloads | `maui-windows`, `android`, `ios` |

Install MAUI workloads if missing:

```bash
dotnet workload install maui
```

## Local infrastructure

```bash
docker compose up -d
```

GoSit deliberately uses **non-default host ports** so the stack never collides with other
services already running on a developer machine:

| Service | Host port | Container port |
|---|---:|---:|
| PostgreSQL | 5434 | 5432 |
| Redis | 6379 | 6379 |
| Azurite blob | 11000 | 10000 |
| Azurite queue | 11001 | 10001 |
| Azurite table | 11002 | 10002 |

Local credentials live in `docker-compose.yml` and `appsettings.Development.json`. They are
development-only values. Real secrets come from Azure Key Vault and are never committed.

## Backend

```bash
dotnet build
dotnet run --project src/backend/GoSit.Api
```

Apply migrations after pulling:

```bash
dotnet ef database update --project src/backend/GoSit.Infrastructure --startup-project src/backend/GoSit.Api
```

Endpoints:

- `POST /api/v1/auth/register` — create a CareRequester or Caregiver account
- `POST /api/v1/auth/login` — exchange credentials for tokens
- `POST /api/v1/auth/refresh` — rotate the refresh token
- `POST /api/v1/auth/logout` — revoke one token, or all with `allSessions: true`
- `GET /api/v1/me` — the authenticated account (requires a bearer token)
- `GET /api/v1/meta` — service identity

Care requester profiles (role `CareRequester`):

- `GET` / `PUT /api/v1/care-requesters/me` — own profile
- `POST /api/v1/care-requesters/me/care-recipients` — add a care recipient
- `GET` / `DELETE /api/v1/care-requesters/me/care-recipients/{id}`
- `PUT /api/v1/care-requesters/me/care-recipients/{id}/care-needs`
- `POST /api/v1/care-requesters/me/emergency-contacts` · `DELETE .../{id}`

  Who a caregiver rings when the family cannot be reached. Read back as part of
  `GET /care-requesters/me`; a caregiver reads them through the booking, never here.
- `POST /api/v1/care-requesters/me/pets` · `PUT` / `DELETE .../{id}` — a family's pets

  Read back as part of `GET /care-requesters/me`. A caregiver sees a pet only through a
  booking: name, species, breed and size always, care notes once they have accepted.

Caregiver profiles (role `Caregiver`):

- `GET` / `PUT /api/v1/caregivers/me` — own profile. `nightlyRate` is optional and only
  needed to offer boarding; sending null clears it.
- `PUT /api/v1/caregivers/me/availability` — replace the weekly schedule

  An availability window whose end time is at or before its start runs **past midnight** —
  Friday 20:00 to 07:00 is one shift ending Saturday morning. That is what makes
  `OVERNIGHT_CHILD_CARE` bookable at all; the week is compared as a circle rather than as
  seven separate days. Equal start and end times are refused, because they could mean a
  full day or none at all.
- `PUT /api/v1/caregivers/me/services` — replace the services offered
- `POST /api/v1/caregivers/me/certifications` — submit a certification for review
- `POST /api/v1/caregivers/me/certifications/{id}/document` — upload the certificate
  (multipart `file`; JPEG, PNG, HEIC, WebP or PDF, up to 10 MB)

  The storage key is assigned by the server, never accepted from the client. Replacing a
  document reopens the review — otherwise a caregiver approved on one file could swap in
  another and keep the badge.

Discovery (role `CareRequester`):

- `GET /api/v1/caregivers/search` — filter and rank caregivers
- `GET /api/v1/caregivers/{id}` — public caregiver detail
- `GET` / `PUT` / `DELETE /api/v1/care-requesters/me/favorites[/{caregiverId}]`

Bookings (both roles — each handler resolves which side the caller is on):

- `POST /api/v1/bookings` — request care (CareRequester only). Names `careRecipientIds` for
  care for people or `petIds` for pet care — never both. Pet care can be booked from 30
  minutes; care for people from an hour.
- `GET /api/v1/bookings` — own bookings; `?status=` and `?upcomingOnly=`
- `GET /api/v1/bookings/{id}`

  **A caregiver does not get the household until they accept.** Before then a booking
  carries `area` (city, province, postal prefix) and not `address`, `instructions` or pet
  care notes; `householdDetailsDisclosed` says which. A declined or lapsed request never
  discloses them. The family always sees their own booking in full.

  **Boarding reverses the address rule.** A boarding booking carries the caregiver's home
  address (`careLocation: "CaregiverHome"`): the caregiver always sees it, and the family
  sees it once the caregiver accepts. `addressDisclosed` says whether `address` is present;
  `householdDetailsDisclosed` still governs the family's notes and pet care notes.
- `POST /api/v1/bookings/{id}/accept` · `/decline` · `/check-in` · `/check-out` — caregiver.
  Check-in and check-out take an optional `{ latitude, longitude }` body. It is evidence,
  never a condition: no body, a declined permission, or an out-of-range fix all record
  `NotProvided` and the transition still succeeds. The coordinates are compared against the
  booking address and discarded — what is stored is a status and a rounded distance, never
  a location.
- `POST /api/v1/bookings/{id}/cancel` — either party
- `POST /api/v1/bookings/{id}/dispute` — either party, within 14 days of completion
- `GET /api/v1/bookings/{id}/recipients/{recipientId}/care-needs` — the recorded care
  needs of one recipient
- `GET /api/v1/bookings/{id}/emergency-contacts` — who to ring if the family cannot be
  reached

  These two are the only routes by which a caregiver reads a family's private records, and
  they share four rules: the caller is a participant; the booking has been **accepted or
  later**, so a pending request discloses nothing and cannot be used to harvest care detail
  or phone numbers; every read is audited; and anything else is **404, not 403**, so
  nothing can be probed.

  Emergency contacts stay readable through `Completed` on purpose — a caregiver may need to
  reach someone about what just happened, and cutting them off at check-out would take the
  number away at the moment it is wanted.

Subscriptions and growth:

- `GET /api/v1/subscription-plans` — plans for this account's side of the marketplace
- `GET` / `POST` / `DELETE /api/v1/me/subscription`
- `GET` / `POST /api/v1/me/referrals`

Reviews, trust and safety:

- `POST /api/v1/bookings/{id}/review` — review a completed booking (either participant)
- `GET /api/v1/caregivers/{id}/reviews` — published reviews on a caregiver
- `POST /api/v1/reviews/{id}/report` — ask staff to look at a review written about you
  (the subject only; see **Taking one down** below)
- `POST /api/v1/reports` — report another user (any signed-in user)
- `GET` / `POST /api/v1/caregivers/me/verifications` — own verification cases

Staff only (`AdminOnly`):

- `GET /api/v1/admin/verifications` · `POST .../{id}/approve` · `.../{id}/reject`
- `GET /api/v1/admin/incidents` · `POST .../{id}/close`
- `POST /api/v1/admin/users/{id}/suspend` · `.../reinstate`
- `GET` / `POST /api/v1/admin/promo-codes` · `POST .../{id}/deactivate`
- `GET /api/v1/admin/disputes` · `POST .../{id}/resolve` — settle a disputed booking

  Resolving takes one number: what goes back to the family. Zero dismisses the dispute and
  releases the payout; the full amount cancels it; anything between reduces the payout and
  recomputes the commission on what is left. The refund is idempotent on the booking id, so
  pressing twice cannot refund twice.
- `GET /api/v1/admin/flagged-messages` — the moderation queue, **no message bodies**
- `GET /api/v1/admin/flagged-messages/{id}` — one message; audited against the staff member
- `POST /api/v1/admin/flagged-messages/{id}/moderate` — `NoConcern`, or escalate to an incident

  The queue carries no message bodies; reading one is a separate request that writes an
  audit row naming the staff member. See **The moderation queue** below.
- `GET /api/v1/admin/reported-reviews` · `POST .../{id}/moderate` — hide a review, or
  let it stand. Hiding removes it from the profile **and** from the rating, and never
  deletes or edits it. See **Taking one down**.
- `GET /api/v1/admin/certifications/{id}/document` — the uploaded certificate

  Streamed through the API, never linked. There is no URL anywhere in GoSit that resolves
  to a certification document: a link to a vulnerable-sector check works for whoever holds
  it, and links end up in browser histories and support tickets. Every read is audited
  against the staff member. **A certification with no document cannot be approved.**

Exposure counts are deleted once older than `Insights:ExposureRetention:Days` (default
180 — twice the ninety-day window the insights screen reports). `ExposureRetentionSweeper`
runs daily and deletes in batches.

Search exposure: every page of results counts an appearance for the caregivers on it, and
opening a profile counts a view. Both land in `caregiver_exposures` as **one row per
caregiver per day carrying counts and nothing else** — no searcher is recorded, so the
table cannot answer "which families looked at this caregiver". Recording is inline and
best-effort: a failure is logged and the search still returns.

Payments and payouts:

- `POST /api/v1/bookings/{id}/payment-intent` — start paying (CareRequester only)
- `GET /api/v1/bookings/{id}/payment` — receipt (either participant)
- `GET /api/v1/caregivers/me/earnings` — wallet and statement (Caregiver only)

  Each statement line carries `availableAtUtc` and `isHeldForDispute`. **A payout is not
  sent until the family can no longer dispute the booking** — fourteen days from
  completion — and an open dispute holds it indefinitely. Without that wait a payout went
  out within fifteen minutes of settlement, so a dispute always arrived after the money
  had gone.

  A payout already in transit or paid cannot be held; recovering that is a reversal, which
  is a different operation.
- `POST /api/v1/webhooks/stripe` — provider webhooks (anonymous, signature-verified)

Messaging and notifications (both roles):

- `GET /api/v1/conversations` — inbox with unread counts
- `GET /api/v1/conversations/{id}` — thread; reading marks it read
- `POST /api/v1/conversations` — start or add to a thread (CareRequester only)
- `POST /api/v1/conversations/{id}/messages` — reply (either party)
- `POST /api/v1/conversations/{id}/read`
- `GET /api/v1/notifications` — `?unreadOnly=` and `?take=`
- `POST /api/v1/notifications/{id}/read` · `POST /api/v1/notifications/read-all`
- `/hubs/messaging` — SignalR hub (authenticated)

Public reference data (anonymous):

- `GET /api/v1/care-services` — the care-service taxonomy

No profile route takes a profile id: the profile is always resolved from the access
token, so one account cannot address another's. Care-recipient routes do carry an id and
re-check ownership in the handler, returning **404 rather than 403** for a recipient
owned by someone else — a 403 would confirm the id exists.
- `GET /health/live` — liveness, no dependency checks
- `GET /health/ready` — readiness, verifies PostgreSQL and Redis
- `GET /openapi/v1.json` — OpenAPI document
- `/swagger` — Swagger UI (Development only)

Access tokens last 15 minutes; refresh tokens last 14 days and rotate on every use.
Only a SHA-256 hash of a refresh token is stored. Presenting an already-rotated token is
treated as theft and revokes every session for that account.

The credential endpoints are rate limited to 10 requests per minute per client IP
(`RateLimiting:AuthPermitLimit`).

Separately, an **account** locks after 10 consecutive failed sign-ins. The window
escalates — a minute, five, fifteen, an hour — and always expires. The two protections
cover different attacks: the per-IP limit stops one machine hammering the endpoint, the
per-account lockout stops a distributed guess at one known address that no single IP
limit would ever see.

A locked account is refused **even with the correct password**, and is given the same
status and body as a wrong password or an unregistered address — otherwise the lockout
would be an account-enumeration oracle. Completing a password reset clears a lockout,
which is the only route back a locked-out person can find unaided.

Search returns a caregiver's **first name and last initial**, and a location reduced to
city plus the postal code's forward sortation area ("Toronto, ON M5S"). It never returns
a street address, email, phone number, or certification document. Only caregivers who
are verified, accepting work, and hold every certification the requested service
requires appear at all — offering a service is a claim, holding the certifications is
what makes a caregiver eligible.

Distance ranking needs coordinates, and no geocoding provider is configured yet, so
`Address.Latitude`/`Longitude` are usually null. Search still works: it falls back to
city and postal-prefix filters, sorts caregivers with an unknown distance *after* those
with a known one, and sets `distanceUnavailable` on the response so the client can say
why results are not sorted by proximity.

### Bookings

`CareBooking` owns the state machine from CLAUDE.md and is the only place transitions
happen. There is deliberately **no endpoint that sets a status** — each transition is its
own action, so the state machine cannot be driven from a client.

Money is never accepted from a client. `CreateBookingRequest` has no price field: the
rate is snapshotted from the caregiver's profile at request time and the total is
computed by the aggregate. A later change to the caregiver's rate does not move the
agreed price. Check-out re-computes from hours actually worked, rounded up to 15 minutes.

Eligibility is checked twice — at request and again at accept — because a certification
can lapse in between.

That enforcement used to be entirely silent. A lapsed certificate removes a caregiver from
search for every service requiring it and blocks an accept, and nothing told them: their
work stopped arriving and the platform never connected the two. `CertificationExpirySweeper`
runs six-hourly and raises `CertificationExpiring` twice — 30 days out, while there is
still time to sit a refresher course, and again on the day it lapses, which is the notice
that answers "why has the work stopped". `CaregiverProfile.TakeExpiryNotices` decides what
is owed and records the stage on the certification, so a sweep four times a day says each
thing once. Unlike the booking notifications these name the certificate, the date, and the
services lost — it is the caregiver's own credential and their own deadline, sent to them
alone, and a summary would fail at the only job the notice has.

#### Screening follows the kind of care

`VerificationRequirements.ClearanceFrom` turns a caregiver's valid checks into a clearance:
photo ID and a vulnerable sector check clear them for **people**; photo ID and a criminal
record check clear them for **pets** only. A vulnerable sector check includes a record check,
so people clearance covers pets too. Staff approving or rejecting a check recomputes it.

`CaregiverProfile.IsEligibleFor` requires people clearance for every service for people.
Search and booking both go through it, which is what makes the split safe: a sitter cleared
for pets who adds Babysitting to their profile still never appears for, or can be booked
for, a child. The caregiver's own checklist asks for the checks that match what they offer.

#### Overnight pet care

A `CareService` has a `PricingUnit` (hourly or nightly) and a `CareLocation` (the
recipient's home or the caregiver's). Pet Boarding is nightly at the caregiver's home;
In-Home Pet Sitting is nightly at the family's home. `CareService.Create` refuses a service
for people at a caregiver's home.

A stay is 1 to `BookingPolicy.MaximumNights` nights, counted by `BookingPolicy.NightsBetween`
in Toronto local dates. It snapshots the caregiver's nightly rate, is estimated on the nights
booked and billed on the nights stayed (at least one). It skips the weekly-hours check but not
the overlap check. Boarding records check-in location as `NotComparable`; in-home sitting
compares it with the family's address as usual.

A stay can be checked out until a day after its scheduled pick-up
(`BookingPolicy.LatePickupGrace`); after that `BookingSweeper` closes it and bills the nights
scheduled. Hourly care still overruns fourteen hours after check-in — the sweep query branches
on `PricingUnit`, mirroring `CareBooking.HasOverrun`.

Cancellation records **who** cancelled (derived from the caller,
never the payload) and whether it fell inside the 24-hour late window, which Phase 6 will
turn into a fee.

A booking that the caller does not participate in returns **404, not 403**, so booking
ids cannot be probed.

#### The platform sweep

Two states leave a booking stranded, because no participant is able to act on it any more:

- a caregiver checks in and never checks out. Past the 14-hour `MaximumDuration`,
  `CheckOut` refuses — a booking that long needs support, not a silent bill — so the
  booking would sit in `InProgress` forever;
- nobody answers a request before its start time, so it stays `Pending` and keeps
  appearing as actionable to both sides.

`BookingSweeper` (`GoSit.Infrastructure/Scheduling`) is a hosted service that polls every
five minutes and closes both, using the `(Status, StartAtUtc)` index on `care_bookings`.
It is shaped like `OutboxPublisher`: a scope per unit of work, a loop that survives a bad
cycle, and logging that carries identifiers only.

- **Overrunning** → `CareBooking.AutoComplete`. Completes the booking and bills the
  **scheduled** `EndAtUtc`, not the elapsed time since check-in: the caregiver left at
  some unrecorded point, and the requester must not pay for a forgotten check-out. Sets
  `WasAutoCompleted` and leaves `CheckedOutAtUtc` **null** — stamping a check-out time
  nobody gave would put invented evidence in a financial record. Raises
  `BookingCompletedDomainEvent`, so the dispute window opens as usual.
- **Unanswered** → the ordinary `Cancel` path with `CancellationParty.Platform`, so
  Payments can tell it apart from a cancellation either side chose. It is flagged as a
  late cancellation because the start has passed; the party is what decides whether a fee
  applies.

Each closure commits in its own transaction, writing its `AuditEvent`
(`BookingAutoCompleted`, `BookingCancelledUnanswered`, both with no actor because the
platform acted) and outbox message alongside the state change. A booking a participant
changed in the meantime raises a `DomainException` or a concurrency conflict, is skipped,
and is re-evaluated next cycle.

### Subscriptions and entitlements

**Entitlements are always resolved server-side**, by `EntitlementService`, from the
stored subscription and the clock. A client telling us what plan it is on is ignored;
gate every feature through `HasAsync`. The `features` list returned by
`GET /me/subscription` is authoritative — a client should gate its UI on that rather
than inferring from the plan code.

Every audience has exactly one free plan, and an account with no subscription resolves
to it. "Not subscribed" and "on the free plan" are the same state, so there is no null
case for a feature gate to get wrong.

A plan can only be bought by its own audience — a caregiver sending a requester plan
code gets a 403.

Two rules about money already paid for:

- **cancelling stops the renewal, it does not confiscate the remainder.** Entitlements
  run to the end of the paid period;
- **a failed payment does not cut someone off immediately.** `PastDue` keeps entitlements
  for `UserSubscription.PastDueGrace` (7 days). Cutting a family off from the caregivers
  they rely on the instant a card expires would be the wrong trade.

`SubscriptionSweeper` closes out lapsed rows hourly. Entitlement checks re-evaluate
against the clock anyway, so a lapsed row never grants anything even before the sweep
runs — the sweep exists to make stored state match reality.

### Referrals and promo codes

A referral **qualifies when the referee completes a booking**, not at signup. Rewarding
signups would pay for throwaway accounts, and a referral programme that pays for nothing
is a fraud budget. An account can be referred once, ever, enforced by a unique index;
self-referral is refused, and reported as "not recognized" so a code cannot be confirmed
as one's own by trying it. A referrer sees that an invitation converted, not who the
account belongs to.

Every reason a promo code can be refused lives on the aggregate — expiry, redemption cap,
audience — because a promo code is a way of giving money away and the checks are the
product. A discount is capped at the price, so it can never produce a negative charge.
One use per account, enforced by a unique index rather than a check-then-act. A code
targeted at the other audience is refused **without saying so**, or the refusal would
leak a campaign's targeting to anyone trying codes.

### Business metrics

`BusinessMetrics` counts the marketplace health measures from CLAUDE.md, derived from
domain events rather than instrumented into each handler — every meaningful state change
already raises one, so this cannot drift out of step with the business logic.

Deliberately dimension-free beyond an event name and a role. Attaching a user id, an
email or a city would turn the metrics backend into a second, less protected copy of the
personal data, and metrics are widely readable inside an organisation. Searches are
counted without their filters for the same reason: a city or postal code would make the
metric a record of where individual families are looking for care.

### Reviews

Reviews are **written blind and published together**. A review stays hidden until the
other side has also reviewed, or the 14-day window closes and `ReviewPublicationSweeper`
releases it. In a care marketplace that matters more than convenience: a caregiver who
raises a safety concern, or a family who reports a problem, must not have to weigh a
retaliatory review before speaking up.

An author can always see their own review; nobody else can until it publishes. Ratings
count **published and not-hidden reviews only**, so a blind review can never move a rating
and reveal itself indirectly. The rating that feeds search ranking is recomputed on
publication, and again whenever a review is taken down.

#### Taking one down

A published review used to be permanent. A caregiver's rating is how they get work, and a
retaliatory one-star after a dispute, a comment naming a child, or a slur reached nobody
who could act — `POST /reports` reports a *user*, which leaves the words on the profile.

- **Only the subject can report a review**, and only a published one. Not the author:
  withdrawing your own review is not something staff should adjudicate. Not a bystander:
  a marketplace where anyone can queue up anyone else's reviews is one where competitors
  do. `POST /api/v1/reviews/{id}/report`.
- **Staff hide it or let it stand.** `GET /api/v1/admin/reported-reviews` ·
  `POST .../{id}/moderate`. A reason is required to hide and not to let stand — "this is
  within the rules" is the default and writes nothing down worth keeping. Staff can also
  hide a review nobody reported; an abusive comment found while looking at something else
  should not need the subject to file a form first.
- **Hiding removes it from the profile and from the rating.** The second half is what
  makes it mean anything: otherwise the words vanish while the one star goes on dragging
  the average down, and the takedown removed only the evidence.
- **Nothing is deleted or edited.** The review stays exactly as written — it is evidence
  about its author as much as its subject. `PublishedAtUtc` is deliberately left alone:
  clearing it would make a decided review look like one still waiting on the other side,
  and `ReviewPublicationSweeper` would put it straight back.
- A dismissed report is **not erased**, so a second complaint about the same review
  reaches someone who can see the first was already considered.

Only a `Completed` or `Disputed` booking can be reviewed — there is nothing to review on
care that did not happen. One review per participant per booking, enforced by a unique
index as well as the aggregate.

### Trust and safety

**A caregiver can start a verification case and supply evidence, but nothing they do can
approve it.** `Approve` and `Reject` both require the reviewing staff member's id and are
reachable only from admin-gated routes. `DocumentReference` is a private Blob Storage key
and never appears in a response — there is a test asserting it.

Becoming verified requires **every** check in `VerificationRequirements.RequiredForListing`
(identity plus a vulnerable-sector check) to be approved and unexpired. Approving one
check is not clearance to work. Rejecting a required check delists the caregiver
immediately.

No background-check vendor has been selected, so `IBackgroundCheckProvider` reports
unconfigured and cases wait for a manual staff decision — which is what actually happens
today.

Incident reports are never deleted; they are the evidence trail behind a safety decision.
Anything in the `SafetyConcern` category, or at `Critical` severity, jumps the triage
queue **whatever severity the reporter chose** — people under stress do not always
classify accurately. Closing a report requires an explanation: an outcome with no
reasoning is not an audit trail.

The report description is written by a user about a real person and may name a child.
It is never logged, never shown to the reported user, and returned only to staff.

Suspending an account revokes every refresh token, so it is signed out everywhere rather
than staying live until its tokens expire. An admin cannot suspend themselves.

**Admin accounts cannot be self-registered** — `User.Register` refuses any role but
care requester and caregiver, and `RegisterValidator` refuses it again at the edge.

Staff are provisioned from configuration instead, by `AdministratorSeeder` at startup.
`User.ProvisionAdministrator` is a separate factory rather than a role argument on
`Register`, so a future change to the registration flow cannot widen into admin creation
by accident.

To create the first staff account locally:

```bash
dotnet user-secrets set "Administration:Bootstrap:Email" "you@example.com" --project src/backend/GoSit.Api
```

Set `Administration:Bootstrap:Password` the same way, then start the API. Deployed
environments supply both from Key Vault. Leaving them unset — the default — provisions
nothing.

The safeguards, which are worth knowing before changing this:

- Nothing happens unless **both** an email and a password are configured.
- An account that already exists is **never touched**. The seeder cannot be used to
  reset a password, so gaining configuration access does not give an attacker an
  existing admin — only the ability to create a new one, which is visible in the
  account list and the audit trail.
- The password is hashed with the same hasher as any other account, and is never logged.
  The startup line names the new account's id, not its email.

Until this existed there was no way to create an admin at all, which left every staff
route unreachable and verification approval possible only by editing the database.
`AdministratorProvisioningTests` drives the real configuration path so that cannot
reopen — the older fixtures promote a row with SQL, which is exactly what hid it.

**Certification review is admin-gated too.** `GET /admin/certifications` lists what is
waiting, oldest first, and approve/reject apply through the caregiver aggregate. A
submitted certificate confers no eligibility until it is approved, so a certificate
sitting in that queue is a caregiver being kept from work they are qualified for.

The staff queues carry a person's **full name, email address and account status** —
more than anything else in the product exposes. That is deliberate: nobody can decide
whether to let a stranger into a family's home from a profile id. It is why
`IAdminLookupRepository` is separate from the marketplace repositories, and why nothing
it returns may be rendered outside `/admin`.

### Payments

**A payment only succeeds when the provider says so.** There is no method a request
handler can call to mark a payment succeeded from a client's word — `MarkSucceeded`
takes the provider's own reference and is reachable only from verified webhook handling.
If the provider reports collecting a different amount than GoSit expected, the aggregate
refuses rather than silently accepting it.

The client never sends a price. `POST /bookings/{id}/payment-intent` takes a booking id
and nothing else; the amount comes from the booking — the actual total once care has
happened, otherwise the estimate.

`Money` is the only place a monetary value is converted to the provider's integer minor
units. It rejects anything that is not already a whole number of cents rather than
silently rounding someone's money. Nothing else multiplies a money value by 100.

Idempotency keys are derived from the booking, so a retried request reuses the provider's
original intent instead of creating a second charge.

#### Webhook security

`POST /api/v1/webhooks/stripe` is anonymous by necessity — Stripe cannot present a GoSit
token. **The signature is the authentication.** Verification uses Stripe's own
`EventUtility.ConstructEvent`, which checks the HMAC over the raw body and rejects
timestamps outside `Stripe:WebhookToleranceSeconds` (default 300), so a captured payload
cannot be replayed later.

The raw body is read verbatim rather than model-bound, because the signature covers the
exact bytes sent. If no webhook secret is configured the endpoint **refuses everything** —
accepting unverified payment events would be far worse than rejecting real ones.

Provider event ids are recorded in `processed_webhook_events` with a unique index, so a
repeat delivery is ignored and two racing deliveries cannot both be applied.

The integration tests build genuine Stripe signatures and exercise the same parser that
runs in production: unsigned, wrongly-signed, tampered, and stale requests are all
asserted to change nothing.

#### Local development

With no `Stripe:SecretKey`, `SimulatedPaymentProvider` stands in so the flows work
without credentials. Webhook verification is **never** simulated — the real parser is used
in every environment. To test webhooks locally, run `stripe listen` and put the secret it
prints into `Stripe:WebhookSecret` (user secrets, not appsettings).

The Stripe path itself — creating intents, refunds, transfers, subscriptions — has not
been exercised against a real account. That needs a test-mode run with the Stripe CLI
before production. See "What is not verified" below.

### Subscription billing

Recurring billing goes through **Stripe Checkout and Stripe Billing**, behind
`ISubscriptionBillingProvider`. It is deliberately separate from `IPaymentProvider`: a
booking payment is a one-off charge GoSit initiates and reconciles, while a subscription
is a relationship the provider owns and drives.

**The rule that matters: `POST /me/subscription` grants nothing when billing is
configured.** It returns a `checkoutUrl` and creates no subscription row. The plan
becomes real only when Stripe confirms payment over a signed webhook. Someone who starts
checkout, abandons it, and then types the success URL by hand still has nothing — there
is a test for exactly that.

The response has two shapes and exactly one is ever set:

```jsonc
{ "checkoutUrl": "https://checkout.stripe.com/...", "subscription": null }  // pay first
{ "checkoutUrl": null, "subscription": { /* ... */ } }                      // no billing configured
```

A caller must handle both. Collapsing them into one optional field is how a redirect gets
mistaken for a completed subscription.

#### Setting it up

Only the first step needs a person; the rest is scripted.

**1. Put a test-mode secret key in user secrets.** This is the one thing that cannot be
automated — it is your credential:

```bash
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." --project src/backend/GoSit.Api
```

**2. Create the Products and Prices:**

```bash
pwsh scripts/setup-stripe-test.ps1
```

It creates a monthly Price per paid plan and records the ids in user secrets. Idempotent
— Prices are matched on `lookup_key`, so re-running finds existing ones. It refuses any
key that is not `sk_test_`, because a setup script should not be able to create pricing
in a live account by accident.

**3. Forward webhooks**, in a second terminal:

```bash
stripe listen --forward-to http://localhost:5199/api/v1/webhooks/stripe
```

Put the `whsec_...` it prints into `Stripe:WebhookSecret`. The events that matter are
`checkout.session.completed`, `invoice.paid`, `invoice.payment_failed` and
`customer.subscription.deleted`; on a deployed endpoint, subscribe to exactly those.

**4. Verify:**

```bash
pwsh scripts/verify-stripe-e2e.ps1
```

It registers a throwaway account, asserts subscribing returns a real
`checkout.stripe.com` URL **and grants nothing**, hands you the URL to complete with test
card `4242 4242 4242 4242`, then polls until the webhook activates the plan and checks
the period, the portal link, and that cancelling keeps the paid period.

Set `WebApp:BaseUrl` too if the web app is not on `http://localhost:3000` — it is where
Stripe returns the browser after checkout.

The seeder links plans to prices on every boot, so changing configuration is enough to
repoint one. A plan with no price configured reports itself as not purchasable rather
than failing at checkout.

#### How events are matched to accounts

GoSit attaches `gosit_user_id` and `gosit_plan_code` as metadata **to both the checkout
session and the subscription it creates**. The second one matters: without it, a renewal
twelve months from now could not be traced back to an account. An event is matched by
that metadata and by the stored provider subscription id — never by anything the browser
reports.

#### The rules the handlers enforce

- **Stripe's billing period wins.** `SyncBillingPeriod` adopts the provider's dates
  rather than our `AddMonths(1)` guess. Stripe knows about proration, trials and dunning
  retries; we do not. Entitlement is decided from that date, so letting the two drift
  would mean charging for one month and entitling another.
- **`billing_reason` separates the first invoice from a renewal.** Treating them alike
  would advance the period twice on signup.
- **A failed invoice does not cut anyone off.** It marks the subscription past due, and
  entitlements continue through `UserSubscription.PastDueGrace` while Stripe retries.
- **Cancelling stops the renewal at Stripe first**, then locally. If the provider call
  fails, nothing local has changed — the alternative is showing someone a cancelled
  subscription while their card keeps being billed.
- **Card details, invoices and receipts live in Stripe's billing portal**, reachable from
  `POST /me/billing-portal`. Building those here would mean handling card data and
  reimplementing tax receipts for no benefit.

#### What is not verified

`SubscriptionBillingTests` covers the whole flow — checkout, activation, renewal, failed
payment, cancellation, replay, and an unsigned webhook granting nothing — through the
real HTTP stack and the real signature verification, with a recording provider standing
in at the same seam Stripe occupies.

`StripeApiContractTests` closes most of the rest. It points the **real**
`StripeSubscriptionBillingProvider` — the actual Stripe SDK, serializing the actual
options objects — at a local stub speaking Stripe's wire format, and asserts on the
requests that arrive: `mode=subscription`, `line_items[0][price]`, the idempotency
headers, and metadata on **both** the session and `subscription_data`. That is the class
of mistake (an option named wrongly, metadata that never leaves the process) which fails
no build and only shows up in production.

Two things remain genuinely unproven, and both need a real test account:

- **Whether Stripe accepts those requests.** The stub returns success regardless; only
  Stripe can reject a malformed one.
- **Whether real event payloads match the parser.** The payloads in
  `StripeWebhookSigner` are hand-written from Stripe's documented shapes. If a payload
  and the parser were wrong the same way, the test passes and production does not.

`scripts/verify-stripe-e2e.ps1` covers both. **Run it before going live.**

### Geocoding

Addresses are resolved to coordinates through **Azure Maps Search**, behind `IGeocoder`.
Without a subscription key — local development and the tests — `UnconfiguredGeocoder`
stands in and returns null.

```bash
dotnet user-secrets set "AzureMaps:SubscriptionKey" "<key>" --project src/backend/GoSit.Api
```

Distance ranking, the radius filter and the "2 km away" on a search card all depend on
this. Nothing else does.

Three decisions worth knowing:

- **A geocoding failure is never fatal.** `AddressResolver` returns the address unchanged
  when it cannot be resolved. A mapping outage must cost distance ranking, not someone's
  ability to record where they live.
- **The subscription key goes in a `Subscription-Key` header, not the query string.**
  Every Azure Maps example uses `?subscription-key=`, but a credential in a URL ends up in
  request logs, proxy logs, and any exception that prints the URI.
- **Low-confidence matches are discarded.** Azure Maps answers almost anything with its
  best guess. A confidently wrong coordinate is worse than none — it puts someone
  kilometres from where they are, in every search, with no symptom. `MinimumConfidence`
  defaults to 0.5.

`countrySet=CA` is sent on every request; without it a Canadian street name will happily
match an American one. Address line 2 is deliberately excluded from the query — "Unit 4"
does not help find a building and often makes the match worse.

`GeocodingBackfillSweeper` runs hourly and resolves addresses saved without coordinates.
It exists precisely because the inline failure has **no symptom**: an unresolved caregiver
is simply invisible to distance ranking and nobody is told. It also covers every address
stored before the key was configured. Caregivers are backfilled before requesters — an
unresolved caregiver loses work, an unresolved requester only loses their own sorting.

#### What is not verified

`AzureMapsGeocoderTests` points the real client at a local stub speaking Azure Maps'
wire format and asserts on the requests it sends. **No call has been made to Azure Maps
itself** — that needs a subscription key.

### Email

Notification emails go out through **Brevo's SMTP relay**, behind `IEmailSender`. With no
credentials configured — local development and the tests — `LoggingEmailSender` stands in
and reports `IsLive` false.

```bash
dotnet user-secrets set "Email:SmtpUsername" "b6494f001@smtp-brevo.com" --project src/backend/GoSit.Api
dotnet user-secrets set "Email:SmtpPassword" "<the SMTP key>" --project src/backend/GoSit.Api
dotnet user-secrets set "Email:FromEmail" "<a Brevo-verified sender>" --project src/backend/GoSit.Api
```

`NotificationEmailDispatcher` polls every 30 seconds and emails notifications that have
not gone out. Out of band rather than inline: a slow relay must not make accepting a
booking slow, and sending inside the request transaction would mean either emailing about
a booking that then rolled back, or holding a transaction open across a call to a third
party.

**The delivery rule that matters:** *"we did not send this"* and *"we sent this"* are
never the same state. With no provider a notification is marked **suppressed**, not sent.
A failure leaves it **owed** and retries with backoff. After eight attempts it **gives
up** — unlike a payout, an email is not owed forever, and the in-app notification is
still there. Those three outcomes stay distinguishable because the record gets read back
when someone asks whether they were told their booking was cancelled.

Emails carry no more than the notification did. `Notification` bodies are already written
to be safe on a lock screen, and the template adds nothing — no addresses, no care needs,
no message content. Titles are HTML-escaped, since they carry names people chose
themselves.

#### Two traps

**The SMTP key is IP-allowlisted.** Brevo's "Unauthorized IP addresses are blocked for
your SMTP keys" is switched on for this account, so every send fails from an address that
is not on the list — with an authentication error that looks like a wrong password.
`SmtpEmailSender` calls this out by name in the exception for that reason. A developer's
home IP and *every* outbound address of the Azure plan have to be added, and App Service
outbound addresses change when the plan scales or moves.

Given that, **Brevo's transactional HTTP API is likely the better fit for deployment**
than SMTP: it authenticates with an API key rather than an IP allowlist, and cloud hosts
commonly throttle outbound port 587. `IEmailSender` exists so that swap is one class.

**Set `Email:RedirectAllTo` in staging.** It sends every email to one address whatever it
was addressed to, which makes it impossible to email a real family from a test database —
the single worst accident this system can have. The intended recipient is preserved in an
`X-GoSit-Original-To` header.

### Email verification and password reset

Both work by emailing a single-use token. **Only the SHA-256 hash is stored** — a reset
token is a bearer credential that can take over an account, so a database leak must not
hand an attacker a working set. Same treatment as refresh tokens.

| | Lifetime | Notes |
|---|---|---|
| Email verification | 7 days | Expiring is a support ticket, not a security event |
| Password reset | 1 hour | A live key to the account; the dangerous window stays small |

Three rules, each because the obvious implementation gets it wrong:

- **Requesting never reveals whether an account exists.** `POST /auth/password-reset`
  returns 204 for any address. An endpoint that 404s on an unknown one is a free tool for
  working out who has a GoSit account — and these accounts belong to families arranging
  care for their children and parents.
- **Redeeming never says *why* it failed.** Wrong, expired and already-used share one
  message, because distinguishing them tells an attacker whether a guessed token existed.
- **Issuing supersedes.** Asking for a second link kills the first, so a request made
  because someone fears their mailbox was seen does not leave a live link behind.

Resetting **revokes every refresh token** — whoever prompted the reset needs the
intruder's session gone, not just the password changed — and **verifies the address as a
side effect**, since redeeming the link already proved control of the mailbox. A
"your password was changed" email always follows: it is the account holder's warning if
the reset was not them.

Recovery emails are sent **inline**, not through the notification dispatcher — someone
watching a mailbox for a reset link should not be waiting on a 30-second poll.

#### Configuring the sender

Sending is from **`noreply@maxwellau.com`**. The domain is registered in Brevo but is
**not yet authenticated** — the DNS records below have to exist first.

`Email:ReplyToEmail` is `maxgudly@gmail.com` deliberately: `maxwellau.com` has **no MX
record**, so it cannot receive email and a reply addressed there would bounce.

`Email:RedirectAllTo` is also `maxgudly@gmail.com`, so **every** email goes there whatever
it was addressed to. Right for testing; it must be cleared for production.

Everything except the key is in user secrets. One command left:

```bash
dotnet user-secrets set "Email:SmtpPassword" "<the Brevo SMTP key>" --project src/backend/GoSit.Api
```

#### DNS records for maxwellau.com

The domain is on GoDaddy (`ns19`/`ns20.domaincontrol.com`). Add these three at the
registrar:

| Type | Name | Value |
|---|---|---|
| TXT | `@` | `brevo-code:33a20c7a5909960f3b671eef54c92f5d` |
| CNAME | `brevo1._domainkey` | `b1.maxwellau-com.dkim.brevo.com` |
| CNAME | `brevo2._domainkey` | `b2.maxwellau-com.dkim.brevo.com` |

Then press **Authenticate domain** in Brevo. Propagation can take up to 48 hours.

**Do not add the fourth record Brevo offers.** Its wizard suggests a DMARC record of
`v=DMARC1; p=none; rua=mailto:rua@dmarc.brevo.com`, but `maxwellau.com` already publishes:

```
v=DMARC1; p=quarantine; adkim=r; aspf=r; rua=mailto:dmarc_rua@onsecureserver.net;
```

A domain may have only one DMARC record — two makes DMARC fail entirely — and replacing
this one would **downgrade the policy from `quarantine` to `none`**, weakening every
address on the domain, not just GoSit's. The existing record uses relaxed alignment
(`adkim=r`), so Brevo's DKIM signature will align against `maxwellau.com` and pass on its
own. Leave it alone.

There is no SPF record at the apex. Brevo's current setup does not need one because it
uses its own return-path, so nothing is required here — but if an SPF record is ever added
for another service, remember it is one TXT record for the whole domain.

#### What is not verified

`NotificationEmailTests` and `AccountRecoveryTests` run against the real database,
aggregates and templates with only the outbound SMTP call replaced. **No email has been
sent through Brevo** — that needs the SMTP key, and this machine's IP on the allowlist.

### Payouts

Caregivers are paid through **Stripe Connect Express accounts with hosted onboarding**,
behind `IPayoutAccountProvider`. Custom accounts would mean GoSit collecting bank
details, government ID and dates of birth from every caregiver, and carrying the
compliance obligations that come with them. **GoSit never sees a bank account number** —
all that crosses the boundary is an account reference and whether money can be sent to it.

`PayoutSweeper` runs every fifteen minutes and is the only thing that moves money out.
The rules it follows, and why each one matters:

- **Money goes only to an account the provider says is ready.** `CanReceivePayouts`
  requires both an account reference and `PayoutsEnabled` as Stripe reports it — never
  inferred from onboarding having been completed, because an account can submit
  everything and still be held for review.
- **The idempotency key is the payout id.** This is the single most important line in
  the sweep: a crash between calling Stripe and saving must not pay someone twice.
- **The attempt is recorded *before* the provider is called**, so a transfer that fails
  by exception backs off exactly like one that fails by response. Without it, a provider
  outage would be retried on every cycle forever.
- **Failures back off** — 15 minutes, 1 hour, 6 hours, then daily — but never stop. A
  transfer usually fails for something that will not fix itself in fifteen minutes, and
  the caregiver has still earned the money.
- **A failed payout stays owed.** It returns to a retryable state rather than vanishing.
- **Only the provider marks a payout paid**, on `transfer.paid` — the same rule as a
  payment. Handing money to Stripe is not evidence it arrived, and the caregiver's
  pending balance counts in-flight transfers as still owed.

`account.updated` is how GoSit learns a caregiver may be paid, matched on the account
reference we stored. The earnings screen also reads status on demand, because someone
returning from onboarding should not have to wait on a webhook to be told where they
stand.

Subscribe to `account.updated`, `transfer.paid` and `transfer.failed` alongside the
payment and subscription events.

#### What is not verified

`PayoutSweepTests` and `PayoutRetryTests` cover the sweep and the backoff through the
real repository, aggregate and scheduling code, with only the outbound Stripe call
replaced. **What has never run is Connect itself** — creating an Express account,
generating an onboarding link, and a real transfer landing in a real test account.
That needs the same test-mode key as subscription billing.

Two things are deliberately not built yet, and both are listed in `PLAN.md`:
**Connect onboarding requires a `transfers` capability on the platform account** (a
Stripe dashboard setting), and **transfers require an available platform balance** —
in test mode that means using the `4000000000000077` card, which funds the balance
immediately, rather than `4242…`.

#### How a payout is created

A payout is created only after the payment has actually settled: GoSit never promises a
caregiver money it has not collected. `PlatformFeePolicy` splits the gross, and the
commission rate is captured on each payout so a later pricing change cannot alter what an
already-earned payout is worth. The split always adds back to the gross, so rounding
never creates or destroys a cent.

**The 15% commission rate is a placeholder, not an agreed price** — see the open decision
in PLAN.md.

### Messaging

One `Conversation` per (care requester, caregiver) pair, so history stays in one place
across repeat bookings. A booking can start a thread — recorded in
`InitiatedByBookingId` — but does not own it.

**Only a care requester can start a conversation.** A caregiver replies to threads that
already exist, which keeps the marketplace from becoming a channel for unsolicited
approaches to families. Contact details are never disclosed through messaging: responses
carry a first name and last initial only.

Read state is a timestamp per participant rather than a flag per message — one value to
update when a thread is opened, and unread counts fall out of it. Sending is treated as
reading.

Messages that look like an exchange of phone numbers or email addresses are marked
`FlaggedForReview` and **still delivered**. Taking care off-platform loses the audit
trail, the verified identity, and the payment protection, but a caregiver may have a
legitimate reason to share a number — so this is moderation input, not censorship.

A conversation the caller is not part of returns **404, not 403**.

#### The moderation queue

`GET /admin/flagged-messages` is the queue, and it is the one staff surface in GoSit that
can show a private message. Its whole shape follows from that:

- **The list carries no message bodies.** It shows who wrote to whom, which direction, and
  when. A preview field would make browsing strangers' private conversations the default
  way to use the screen, so there isn't one — and the test asserts it against the raw
  payload rather than a typed model, because a body cannot be missing from a record that
  never declared it.
- **Reading one is a separate, audited request.** `GET /admin/flagged-messages/{id}` writes
  a `FlaggedMessageRead` audit row naming the staff member, and commits it *before*
  returning the body. The audit is the price of the disclosure. The screen tells the staff
  member this before they click — a safeguard nobody is told about changes no behaviour.
- **Settling is "nothing wrong" or an escalation.** Escalation raises an ordinary
  `IncidentReport` about the sender, so it lands in the queue that already has assignment,
  resolution and its own audit trail rather than in a second place where decisions about
  someone's account get made. The staff note is the incident description; the message body
  is not copied into it, because an incident is read by more people and lives longer.
- **The flag is never unset, and the message is never edited or withdrawn.** Whether it
  looked like contact sharing is a fact about the message; whether that mattered is a
  judgement about it. `ModerationOutcome` records the second without overwriting the first.
  Deciding twice is refused rather than ignored — two staff members reaching opposite
  conclusions is a real event and should not be silently resolved in favour of whoever
  clicked first.

### Video interviews

Families and caregivers meet on video without leaving GoSit. The point is the same as
messaging's: nothing arranged off the platform, so the verified identity, the record, and
the payment protection all still apply when a booking follows.

**Only a family can ask**, from a caregiver's profile or their conversation, and the API
creates the pair's conversation if there is none yet — the interview's chat is written
there. One open interview per pair. A request needs an hour's notice, is 15/30/45/60
minutes, and is at most 60 days out. The caregiver accepts or declines; either side can
cancel until it starts. "Expired" (no reply in time) and "Missed" (accepted, nobody joined)
are worked out on read, never stored.

**Every interview is recorded, and joining is refused otherwise.** `POST /interviews/{id}/join`
takes `consentToRecording`; without `true` it is 400. With it, the handler (in this order)
saves consent and the join plus an audit row, confirms the room is being recorded or starts
a recording, and only then issues a LiveKit token — for that room only, camera and
microphone only, no screen share. If the recording cannot be started the join is 409 and no
token exists. Two people joining at the same moment start one recording: the first to win
`TryClaimRecordingLeaseAsync` (one conditional `UPDATE`) starts it, the other waits and
finds it running.

Recordings are room composites LiveKit uploads straight to a private Azure container
(`InterviewRecordings`), never through the API. LiveKit's webhooks (`POST /webhooks/livekit`,
anonymous, verified by the signed body hash in the Authorization header) mark a recording
saved or failed and a room closed. A recording that fails while the room is still open
closes the room — the alternative is an unrecorded conversation carrying on.

**Staff only.** `GET /admin/interviews` lists who met whom, when, and what is kept; no
video. `GET /admin/interviews/recordings/{id}` streams one, after writing a
`RecordingViewed` audit row and saving it — same shape as certification documents, for
the same reason: a storage link is a bearer token for video of someone's home. Participants
get no route to a recording at all. `InterviewRecordingRetentionSweeper` deletes files 90
days after they finish (`InterviewPolicy.RecordingRetention`), file first and row second,
unless staff have put the interview on hold (`POST /admin/interviews/{id}/hold` with a
reason; `/release` lifts it).

Configuration, all secrets outside development:

```json
"LiveKit": { "ServerUrl": "wss://<project>.livekit.cloud", "ApiKey": "", "ApiSecret": "" },
"InterviewRecordings": { "ConnectionString": "<Azure Storage, with AccountName and AccountKey>", "ContainerName": "interview-recordings" }
```

Both are needed or neither is used: without them `UnconfiguredVideoMeetingProvider` refuses
interview requests with "not available yet", because a video service with nowhere to put
the recording is a way to hold an unrecorded call. The storage account must be a real one —
LiveKit Cloud cannot reach a local Azurite. Register `https://<api>/api/v1/webhooks/livekit`
as a webhook in the LiveKit project.

The integration tests replace the provider with `FakeVideoMeetingProvider` and the store
with `InMemoryRecordingStore` (see `GoSitApiFactory`); `LiveKitWebhookTests` exercises the
real provider's signature verification on its own.

### Notifications

Notification titles and bodies are short, non-sensitive summaries — "New message from
Ada L.", never the message itself. They are delivered by email and push and land on lock
screens, so they must never carry message content, care needs, or addresses. There is a
test asserting exactly this.

Booking transitions raise them through `BookingNotifier`, called from the transition
handlers inside the same transaction as the state change. Two rules hold across all of
them, and both are tested rather than assumed:

- **The actor is not notified about their own action.** Both sides can cancel, so who to
  tell is passed in rather than inferred. Telling someone what they just did is the noise
  that trains people to ignore the notification that matters.
- **A notification says what happened and where to look, never why.** A decline carries
  the caregiver's words about why they cannot take a job, a dispute carries a complaint
  about what happened in a family's home, and a settlement carries a staff member's
  account of it. Those stay on the booking, which is authorized; the notification is a
  summary.

A dispute notifies **both** sides. It stops the caregiver's payout, and money that quietly
stops arriving with no message and no date reads as a bug — they would contact support,
which is the one outcome nobody wants from a dispute. Its settlement notifies both too,
and the mandatory staff note is exposed on `BookingResponse` so the family and the
caregiver read the same words on their own booking pages.

`/hubs/messaging` is a SignalR hub. It is authorized, clients are grouped by their own
user id, and it exposes no methods a client can call to fetch data — it only announces
that something changed, carrying identifiers and counts. The authorized HTTP endpoints
remain the only way to read anything.

### Audit trail and outbox

Every booking transition writes an `AuditEvent` and an `OutboxMessage` in the *same*
transaction as the state change (see `GoSitDbContext.SaveChangesAsync`). Business state
and the intent to publish therefore commit together or not at all. This holds for
transitions the platform makes on its own, too — see the sweep above.

`OutboxPublisher` is a hosted service that polls every 10 seconds, publishes, and marks
messages processed only on success — so delivery is at-least-once and **consumers must be
idempotent**. The transport is currently `LoggingIntegrationEventPublisher`; swapping in
Azure Service Bus changes that one class.

Event payloads and audit rows carry **identifiers only** — never an address, instructions,
recipient details, or contact information.

`CareNeedsProfile` holds allergies, behavioural notes, triggers, and emergency
instructions. It is the most sensitive data in the system: never log it, never put it in
a shared cache, and never return it to anyone but the owning care requester. Caregiver
certification documents are private Blob Storage keys and are never serialized into a
response — only the derived status and expiry are public.

Every response carries an `X-Correlation-Id` header, echoing the request header when supplied.
Unhandled exceptions return RFC 7807 ProblemDetails without leaking exception text.

## Tests

```bash
dotnet test tests/GoSit.UnitTests
dotnet test tests/GoSit.ArchitectureTests
dotnet test tests/GoSit.IntegrationTests   # requires Docker (Testcontainers)
```

Architecture tests enforce the layering rules: Domain depends on nothing, Application depends
only on Domain, and neither may reference EF Core.

## Web

```bash
cd src/web/gosit-web
npm install
npm run dev        # http://localhost:3000
npm run lint
npm run typecheck
npm run build
```

Set `API_BASE_URL` (server-side) and `NEXT_PUBLIC_API_BASE_URL` — see `.env.example`.

Design tokens live in `src/styles/tokens.css` and are exposed to Tailwind through
`src/app/globals.css`. Components must reference semantic tokens (`bg-brand-primary`), never
raw hex values.

Two things to know about the token layer:

- A semantic token must not share a name with the Tailwind theme namespace it feeds
  (`--color-*`, `--shadow-*`, `--font-*`), or it resolves to itself and silently produces
  nothing. Name the source in the raw tier instead — see `--gosit-shadow-auth-card`.
- `next/font` variables are applied to `<html>` in `app/layout.tsx`, because `tokens.css`
  maps them to typography roles at `:root`. Moving those classes to `<body>` puts them
  out of scope and every font falls back to the system stack.

### Layout

`DashboardShell` is the signed-in frame: a navigation rail beside a content column,
following the wireframes' dashboard layout. The rail becomes a horizontal scroller on
small screens rather than a drawer — it holds five destinations, so a drawer would add a
tap for no gain.

**Result grids use container queries, not viewport breakpoints.** The results column sits
inside a sidebar layout, so it is much narrower than the window; `sm:`/`xl:` produced
200px-wide cards on a 1280px screen. Note that `@container` must be on an *ancestor* of
the grid — putting it on the grid itself silently never matches.

Icons in `components/ui/icons.tsx` are drawn in-repo because the wireframes have no icon
set. They are always paired with a visible text label and marked `aria-hidden`, so
nothing depends on recognising them; replacing that one file swaps the set.

Session tokens are held in httpOnly cookies set by server actions in
`src/features/authentication/actions.ts`. `src/lib/auth/session.ts` is marked
`server-only`, so importing it from a Client Component fails the build rather than
leaking tokens into the bundle.

## End-to-end tests

```bash
cd tests/web-e2e
npm install
npx playwright install chromium
npx playwright test
```

Playwright starts its own Next.js dev server on **port 3100** so it never attaches to an
unrelated dev server on port 3000. Set `E2E_BASE_URL` to test a deployed environment instead.

Specs tagged `@smoke` need only the web app. Specs tagged `@api` also need the API
running and reachable at `API_BASE_URL`:

```bash
npm run test:smoke
npm run test:api
```

**Raise the credential rate limit for `@api` runs.** A suite legitimately signs in far
more often than a person would, so the default of 10 requests per minute per IP trips
partway through and the failures look like product bugs. Start the API for an e2e run
with the limit raised by environment — never in `appsettings.json`, where it would weaken
a real protection:

```bash
RateLimiting__AuthPermitLimit=100000 dotnet run --project src/backend/GoSit.Api
```

The suite itself signs up **once per worker** and reuses the session cookies, so it is
not the number of tests that matters — it is that any run does several registrations.

**On flaky parallel runs.** Repeated full runs were giving 74/74, then 72, then 70 —
different tests each time, all passing in isolation. Most of that was one genuinely
fragile test: it clicked the same button label twice across a re-render, so the second
click raced React replacing the element. Scoping that click to the form it lives in fixed
it, and consecutive full runs are green again.

That lesson still holds and is still the first thing to check: a test that fails only
under parallel load is often a locator resolving to different elements at different
moments, and load is what widens the window.

**But it was not the whole story, and the suite now runs serially by default.** A later
round of failures survived every explanation of that kind. They were chased down and ruled
out one at a time: not the credential rate limit (registrations all returned 200, and the
only limiters are the auth and webhook policies); not `next dev` compiling on demand (a
production build failed the same way, and worse); not database bloat (about 1,400 users and
150 caregivers, and the schema was fully migrated); not a slow API (its worst request in a
failing run was 8s against a 30s timeout); and not a degraded server (it answered the same
routes in single-digit milliseconds the moment the run finished).

What remained was the machine. The failures were always `page.goto` exceeding its timeout,
never the same feature twice, and the rate tracked the worker count rather than anything in
the code — **1 worker: all 77 passing, twice over; 2 workers: 63; 4 workers: between 56 and
70, varying run to run**. Playwright defaults to half the available cores, which on a box
already running the API, a dev server and a browser is enough to saturate it.

So `playwright.config.ts` pins `workers: 1` locally and leaves CI on Playwright's default.
The full run takes about two minutes serially, which is a cheap price for a result worth
reading — a suite that fails differently every time is worse than no suite, because people
stop believing it. Override with `--workers=N` on an idle machine.

One thing was *not* explained. A long run leaves `SessionExpiredError` entries in the web
server log, thrown out of Server Components. The obvious suspect was link prefetching —
the proxy deliberately skips its refresh redirect for prefetches, so a prefetched render
would have no access token — but a prefetch request with the access cookie removed still
redirects rather than rendering, so that is not it. Server Actions rethrow the error by
design, which accounts for some. Unexplained, and worth returning to.

## Mobile

```bash
dotnet build src/mobile/GoSit.Mobile/GoSit.Mobile.csproj -f net10.0-windows10.0.19041.0
dotnet build src/mobile/GoSit.Mobile/GoSit.Mobile.csproj -f net10.0-android
```

iOS builds require a macOS host with Xcode.

Device capabilities (secure storage, geolocation, connectivity) are behind interfaces in
`GoSit.Mobile.UI/Abstractions` so shared Razor UI stays testable.

## Infrastructure

```bash
az bicep build --file infra/bicep/main.bicep --stdout
```

Pipelines live in `infra/pipelines/`: `backend-ci.yml`, `web-ci.yml`, `mobile-ci.yml`,
`infra-ci.yml`.

## Troubleshooting

**Port already allocated on `docker compose up`** — another service holds the host port. Check
with `docker ps` and adjust the mapping in `docker-compose.yml` plus the matching connection
string in `appsettings.Development.json`.

**Integration tests pass without running anything** — the Postgres Testcontainers test no-ops
when Docker is unavailable. Start Docker Desktop to exercise it for real.

## Postal-area search ("caregivers near you")

Distance search and the website's "how many caregivers are near me" box run on free data held
inside the application. Nothing here calls a paid mapping service.

- **Data:** the GeoNames postal-code dataset for Canada, about 1,650 forward sortation areas
  (the first three characters of a postal code, like `M5V`), each with a name and a centre
  point. It lives in `src/backend/GoSit.Infrastructure/Geocoding/Data/ca-postal-areas.tsv` and is
  embedded in the build. It is published under **Creative Commons Attribution 4.0**, so the
  website footer credits GeoNames; keep that credit.
- **Refreshing it:** download `CA.zip` from https://download.geonames.org/export/zip/, then write
  columns 2 (code), 3 (name), 5 (province), 10 (latitude) and 11 (longitude) of `CA.txt`, tab
  separated, over the file above.
- **Precision:** neighbourhood level, by design. Locations are worked out when a search runs and
  are **never stored on an address**. A stored coordinate is read as "this is the house", and
  arrival check-in compares a caregiver's phone against it, so a neighbourhood's centre saved
  there would be wrong by kilometres. A street-level coordinate, when Azure Maps is configured,
  still wins over the postal area.
- **Public endpoints:** `GET /api/v1/areas/overview?query=M5V&radiusKm=25` and
  `GET /api/v1/areas/suggest?q=toron`. Anonymous, rate limited per IP (`RateLimiting:Lookup*`),
  readable from the company website only (`Cors:PublicFormOrigins`), and they return a count and a
  place, never a caregiver.
