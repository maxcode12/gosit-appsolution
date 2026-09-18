---
name: gosit-feature-delivery
description: Plan, implement, review, or extend a GoSit trusted-care marketplace feature across Figma, Next.js, .NET MAUI Blazor Hybrid, ASP.NET Core Web API, PostgreSQL, Azure, and Azure DevOps.
---

# GoSit Feature Delivery Skill

## Product Context

GoSit is a marketplace operated by 11419056 Canada Inc. o/a GoSit, connecting a
`CareRequester` with an independent `Caregiver` — never an employer relationship, never
GoSit directly providing care, never a claim that GoSit guarantees caregiver safety.
See `docs/BUSINESS_MODEL.md` and `docs/ADR-004-marketplace-provider-model.md`.

GoSit serves:
- Child Care
- Senior Care
- Specialized Non-Medical Support
- Respite & Family Support
- Pet Care (a separate vertical — pets are a `Pet` entity, never a `CareRecipient`)

## Domain Vocabulary

Use:
- CareRequester
- CareRecipient
- Caregiver
- CareService
- CareNeedsProfile
- CareRequest
- CareBooking

Special needs belong in CareNeedsProfile, not a recipient type.

## For Every Feature

Identify:
1. Actor
2. Care recipient
3. Selected service
4. Relevant care needs
5. Caregiver qualification requirements (screening, certifications, and — where
   built — insurance; see `docs/TRUST_AND_SAFETY.md`)
6. Domain rules
7. Authorization rules
8. API contract
9. Persistence changes
10. Events
11. Next.js behavior
12. MAUI behavior
13. Figma source/gaps
14. Security/privacy risks
15. Tests
16. Observability
17. Azure DevOps work items

## Mandatory Safety Checks

Test:
- unauthorized access
- non-owner access
- unqualified caregiver
- invalid booking state
- excessive sensitive-data exposure
- background/verification status
- audit requirements

## Medical Boundary

MVP is non-medical. Do not introduce regulated clinical services without explicit legal/compliance approval.

## Figma

Source:
https://www.figma.com/design/sJQ271UkXOx3Uiklp2Bn41/GoSitly-Wireframe

Existing wireframes are child-care oriented and must be expanded for senior and special-needs support.

## Completion

A feature is complete only when design, domain rules, service eligibility, API, clients, tests, privacy/security, telemetry, and DevOps tracking agree.
