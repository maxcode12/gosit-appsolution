# ADR-003 — Care-first domain vocabulary

## Status
Accepted.

## Context

Two project sources described the domain differently.

`CLAUDE.md` and `.claude/skills/gosit/SKILL.md` define GoSit as a trusted-care
marketplace for children, seniors, adults, and people needing specialized non-medical
support. They name the core concepts `CareRequester`, `CareRecipient`,
`CareNeedsProfile`, `CareService`, `CareRequest`, and `CareBooking`, and state directly:
*"Avoid designing the domain only around Parent, Child, and Babysitter."*

The engineering brief that drove Phases 0 and 1 described a childcare marketplace and
listed `ParentProfile` and `Child` among the core entities. Phase 1 shipped a
`UserRole.Parent` value on that basis.

The conflict had to be settled before Phase 2, because profiles are the foundation that
Search, Booking, Payments, and Trust and Safety all build on.

## Decision

Use the care-first vocabulary from `CLAUDE.md`.

- `CareRequesterProfile` replaces `ParentProfile`.
- `CareRecipient` replaces `Child`, with `CareRecipientType` of `Child`, `Adult`, or
  `Senior`.
- `CareNeedsProfile` carries support needs for a recipient of any age.
- `UserRole.Parent` was renamed to `UserRole.CareRequester`, keeping ordinal `1` so no
  data migration was needed.

Special needs are described in `CareNeedsProfile`, never as a recipient type, because a
recipient of any age may have them.

## Rationale

A parent is one kind of care requester, alongside an adult arranging care for a parent
and a support worker arranging respite. Modelling the general case costs little now:
`CareRecipient` carries a type and a date of birth where `Child` would have carried a
date of birth alone.

Generalizing later would have been far more expensive. `CareRequest`, `CareBooking`,
`Payment`, and `Review` all reference the recipient, so widening the concept after those
aggregates exist means migrating live booking and payment data, not just renaming types.

## Consequences

Positive:

- The senior-care and specialized-support taxonomy in `CLAUDE.md` can be added as
  reference data without a schema change.
- Domain names match the checked-in project instructions and the delivery skill, so
  future work has one vocabulary to follow.

Trade-offs:

- The Figma wireframes still say "Parent". `docs/FIGMA_HANDOFF.md` already records those
  as historical labels rather than domain constraints; screens will need their copy
  reviewed as they are designed for senior and specialized care.
- The registration API contract changed: `role` now takes `"CareRequester"` rather than
  `"Parent"`. Acceptable because there is no production data and no external consumer.
- `CareRecipient` guards the pairing of age and type — a minor cannot be recorded as an
  adult or senior. Adult and Senior are allowed to overlap, since someone in their
  sixties may reasonably be recorded either way.
