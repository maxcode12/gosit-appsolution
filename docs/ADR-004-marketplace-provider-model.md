# ADR-004 — Marketplace provider model, not employer

## Status
Accepted.

## Context

GoSit connects families arranging care with independent caregivers, and now also
connects pet owners with independent pet-care providers. As the platform has grown from
babysitting into senior care, specialized support, and pet care, it has become necessary
to state explicitly — for engineering, product copy, and legal drafting alike — what kind
of business GoSit is, because the answer changes what documentation, contracts, and code
comments may say.

The founder has discussed caregiver insurance with a lawyer. The direction that came back
was specific: caregivers should carry their own insurance, and GoSit should not insure
them as if they were employees or care providers. That single decision only makes sense
once the underlying marketplace-provider model is stated as a decision in its own right,
which is what this ADR records.

The operating entity is 11419056 Canada Inc., operating as GoSit — see
`docs/BUSINESS_MODEL.md` for the corporate detail. Its Articles of Incorporation place no
restriction on the business it may carry on, so nothing here required a corporate
amendment; it required a documented product and legal position.

## Decision

GoSit operates as a marketplace connecting CareRequesters with independent Caregivers
(and, for pet care, pet owners with independent pet-care providers).

- GoSit does not employ caregivers and does not supervise the manner or means of care.
- GoSit does not directly provide care.
- GoSit does not guarantee caregiver performance or safety.
- GoSit does not insure caregivers. Caregivers maintain their own appropriate insurance,
  which GoSit verifies where required for a given service.
- GoSit maintains insurance for its own corporate and platform exposures (see
  `docs/BUSINESS_MODEL.md` for the coverage checklist).

Full detail on the insurance split, the caregiver-insurance-verification workflow, and the
open legal and insurance questions this raises live in `docs/BUSINESS_MODEL.md` and
`docs/TRUST_AND_SAFETY.md` — this ADR records the decision, not the mechanics.

## Consequences

Positive:

- Supports the marketplace model the product has always been built around, rather than
  drifting toward an agency model as more service categories are added.
- Clear provider responsibility: each caregiver is accountable for their own conduct and
  coverage, which is also what independent-contractor status requires to hold up.
- Avoids the mistake of treating GoSit's own platform insurance as if it covered
  caregiver liability, which it does not and should not.
- Service-specific provider eligibility (insurance, certifications, screening) can be
  layered on per service without changing the underlying relationship.

Trade-offs:

- Higher caregiver onboarding friction: caregivers who do not already carry insurance
  will need to obtain it before they can be listed for services that require it, which
  may narrow the caregiver pool while the marketplace is small.
- An insurance-verification system is required (document upload, staff or vendor review,
  expiry tracking, renewal reminders) — see `docs/TRUST_AND_SAFETY.md`.
- Expiry and renewal management is ongoing operational work, not a one-time build.
- Legal and broker review is needed per service and per jurisdiction as new care
  verticals are added (pet care already raises its own questions for the broker — see
  `docs/BUSINESS_MODEL.md`).
- Potential supply impact if the insurance bar is set too high for the market GoSit is
  trying to serve at launch.

## Related

- `docs/BUSINESS_MODEL.md` — legal entity, insurance strategy, revenue model, open legal
  and insurance questions.
- `docs/TRUST_AND_SAFETY.md` — screening vendors under evaluation, verification-provider
  abstraction, caregiver onboarding and statuses, insurance verification workflow.
- `docs/ADR-001-option-c.md` — established GoSit as a trusted care marketplace rather
  than a babysitting-only application; this ADR is the legal/insurance corollary of that
  decision as the marketplace has grown.
- `docs/ADR-003-care-first-domain-vocabulary.md` — the domain vocabulary
  (`CareRequester`/`Caregiver`/`CareRecipient`) this ADR's language builds on.
