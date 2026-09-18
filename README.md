# GoSit

A Canadian trusted-care marketplace, operated by **11419056 Canada Inc. o/a GoSit**.
Canada-first, with Ontario as the initial operating market.

GoSit connects a `CareRequester` (a family or individual arranging care) with an
independent `Caregiver` — it is a marketplace, not an employer, an agency, or a direct
provider of care. See [docs/BUSINESS_MODEL.md](docs/BUSINESS_MODEL.md) for the legal and
insurance framing, and
[docs/ADR-004-marketplace-provider-model.md](docs/ADR-004-marketplace-provider-model.md)
for the decision record.

Target care areas:

- Child Care
- Senior Care
- Specialized Non-Medical Support
- Respite & Family Support
- Pet Care

MVP is non-medical; see CLAUDE.md's "Medical Boundary."

Engineering documents:

- [CLAUDE.md](CLAUDE.md) — repository-level engineering instructions
- [PLAN.md](PLAN.md) — product and engineering plan, with per-phase implementation status
- [docs/BUSINESS_MODEL.md](docs/BUSINESS_MODEL.md) — legal entity, marketplace model,
  insurance strategy, revenue model, open legal/insurance questions
- [docs/TRUST_AND_SAFETY.md](docs/TRUST_AND_SAFETY.md) — screening/verification vendors
  under evaluation, caregiver onboarding and trust model
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — solution architecture
- [docs/FIGMA_HANDOFF.md](docs/FIGMA_HANDOFF.md) — Figma node-to-product mapping
- [docs/ADR-001-option-c.md](docs/ADR-001-option-c.md) through
  [ADR-004](docs/ADR-004-marketplace-provider-model.md) — architecture decision records
- [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) — local setup and day-to-day workflow
- [.claude/skills/gosit/SKILL.md](.claude/skills/gosit/SKILL.md) — feature-delivery skill

## Repository layout

```text
src/backend/    ASP.NET Core API, Domain, Application, Infrastructure
src/web/        Next.js web client
src/mobile/     .NET MAUI Blazor Hybrid app and shared Razor UI library
tests/          unit, integration, architecture, and Playwright e2e tests
infra/          Bicep infrastructure and Azure DevOps pipelines
docs/           architecture, business, trust & safety, and legal documents
```

## Quick start

Start local infrastructure (PostgreSQL, Redis, Azurite):

```bash
docker compose up -d
```

Run the API:

```bash
dotnet run --project src/backend/GoSit.Api
```

Run the web client:

```bash
npm run dev --prefix src/web/gosit-web
```

See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for ports, tests, and troubleshooting.

## Current status

Substantial engineering has been done — authentication, care-requester and caregiver
profiles, search, bookings, messaging, video interviews, payments and payouts, reviews,
subscriptions, and a trust-and-safety admin portal all exist in the backend and (mostly)
the web client, with the mobile app covering sign-in, job list, check-in/out, and
earnings. See [PLAN.md](PLAN.md) for exact, per-phase status — it marks each item done,
in progress, or explicitly open, and is the source of truth over this summary.

The project has **not** shipped: there is no vendor-selected background-check or
insurance-verification integration, no live production deployment, no billing or payouts
verified against a real Stripe account, and the legal documents in `docs/legal/` are
drafts that have not been reviewed by a lawyer. Do not describe any of this as launched
or production-ready without checking PLAN.md first.
