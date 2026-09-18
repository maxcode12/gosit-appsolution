# ADR-001 — Option C and Multi-Service Care Domain

## Status
Accepted.

## Decision

Use:
- Next.js / React / TypeScript for web
- .NET MAUI Blazor Hybrid for iOS/Android
- ASP.NET Core Web API for backend

GoSit is a trusted care marketplace, not a babysitting-only application.

The core domain uses CareRequester, CareRecipient, Caregiver, CareService, CareNeedsProfile, CareRequest, and CareBooking.

## Rationale

This supports child care, senior care, and specialized non-medical support without rewriting the platform later.

## Consequences

Benefits:
- broader market opportunity
- reusable marketplace core
- stronger lifetime value opportunity
- service-specific caregiver matching
- avoids child-care-only data model constraints

Trade-offs:
- additional privacy obligations
- more complex verification and qualification
- different UX by service type
- broader legal/insurance review
- two frontend ecosystems remain necessary under Option C
