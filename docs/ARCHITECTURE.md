# ARCHITECTURE.md — GoSit Solution Architecture

## Context

```text
Care Requesters + Caregivers
          |
   Next.js / MAUI
          |
   Azure Front Door
          |
 Azure API Management
          |
 ASP.NET Core API
 Feature-Oriented Modular Monolith
          |
  +-------+-------+---------+---------+
  |       |       |         |         |
Postgres Redis Service Bus Blob    SignalR
                  |
             Background Workers

Key Vault + Application Insights + OpenTelemetry
```

## Marketplace Boundary

GoSit is a marketplace connecting a `CareRequester` with an independent `Caregiver` — it
is not an employer, not an agency, and not a direct provider of care. This is a legal
position as much as an architectural one; see `docs/BUSINESS_MODEL.md` and
`docs/ADR-004-marketplace-provider-model.md`. It has one direct architectural
consequence: caregiver insurance is modelled as a *verification* the marketplace checks
(`IInsuranceVerificationProvider`, planned — see `docs/TRUST_AND_SAFETY.md`), never as a
policy GoSit itself issues or administers on a caregiver's behalf.

## Core Marketplace Model

```text
CareRequester
   |
   +--> CareRecipient
   |      |
   |      +--> CareNeedsProfile
   |
   +--> Pet                      # a separate entity, never a CareRecipient

Caregiver
   |
   +--> Skills
   +--> Certifications
   +--> CareServices
   +--> Availability
   +--> Verification              # identity, background, vulnerable-sector
   +--> Insurance (planned)        # see docs/TRUST_AND_SAFETY.md
   +--> References (planned)

CareRequest
   |
   +--> CareBooking
          |
          +--> CareRecipient(s) OR Pet(s)   # never both on one booking
          +--> Caregiver
          +--> CareService
          +--> Payment/Payout

Interview                         # pre-booking video call, recorded, staff-only playback
   |
   +--> CareRequester
   +--> Caregiver
   +--> Conversation
```

## Feature Boundaries

Authentication, CareRequesters, CareRecipients, CareNeeds, Pets, Caregivers (Skills,
Certifications, Availability, Verification, Insurance and References planned),
CareServices, Search, Matching, CareRequests, Bookings, Payments, Payouts, Messaging,
Interviews, Notifications, Reviews, Subscriptions, Trust & Safety (Incidents, Screening,
Verification, Suspensions, Appeals planned), Administration.

## Verification Provider Abstraction

Background screening, identity verification, and (once built) insurance verification are
designed as narrow, swappable interfaces rather than one wide service, so GoSit is never
locked to a single vendor. See `docs/TRUST_AND_SAFETY.md` for the interface list and the
vendor candidates under evaluation — none is selected. `IBackgroundCheckProvider` exists
today with no vendor implementation wired up.

## Service Eligibility

Each CareService can define required:
- verification
- background check
- certifications
- skills
- experience
- transportation capability
- other service-specific requirements

The backend evaluates caregiver eligibility before booking acceptance.

## Privacy

Care-needs details, health-related information, addresses, background-check documents, and private messages are restricted data.

## Medical Boundary

MVP supports non-medical care only.

## Reliability

Use transactional Outbox + Azure Service Bus for integration events. Consumers must be idempotent.

## Deployment

Begin as modular monolith + background workers. Extract services only when scale, compliance isolation, or team ownership justifies it.
