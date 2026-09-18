# BUSINESS_MODEL.md — Legal Entity, Marketplace Model, Insurance, Revenue

This document exists because the decisions below govern how every feature is written and
documented, and previously lived only in conversation with the founder and outside
counsel. It is not a legal opinion — see "Open legal questions" — and final wording for
anything user-facing needs lawyer review before it ships.

## Legal entity

The operating company is **11419056 Canada Inc.**, federally incorporated under the
Canada Business Corporations Act (2019-05-20), registered office at 1977 Northway Avenue,
Windsor, Ontario, and extra-provincially registered in Ontario the same day. Its Articles
of Incorporation:

- authorize an unlimited number of common shares;
- allow 1 to 10 directors;
- place **no restriction on the business the corporation may carry on**.

That last point matters: GoSit's expansion from babysitting into senior care, specialized
support, pet care, and whatever comes next is not something the Articles need to be
amended for. Nothing in the existing corporate record limits the company to one line of
business.

"GoSit" is a trade name the corporation operates under, not a separate legal entity. In
documentation and contracts, prefer:

- **"11419056 Canada Inc. operating as GoSit"** or **"11419056 Canada Inc. o/a GoSit"**
  for legal/business references (contracts, policies, filings).
- **"GoSit"** alone for public-facing branding (marketing copy, the app UI).

**Open item:** confirm with counsel whether "GoSit" is registered as a business name
under Ontario's Business Names Act. The Articles only name "11419056 Canada Inc." — if the
app, contracts, or receipts present the business as "GoSit" without that registration on
file, it should be filed before the platform has real users.

### Future subsidiary option (not created)

```text
11419056 Canada Inc.
        |
        +-- (today) operating as "GoSit"

11419056 Canada Inc.
        |
        +-- (future option) GoSit Inc.
              |
              +-- GoSit care marketplace
```

A subsidiary is a future strategic option, not a current plan. It may be worth revisiting
if any of the following becomes real: outside investment, a need to separate liability
between GoSit and any other business under the parent, tax planning, IP ownership
questions, multiple distinct businesses under the same parent, a strategic partnership, or
acquisition planning. Any such move needs corporate, tax, and legal review before it
happens — do not treat it as decided or in progress.

## Marketplace model

```text
        MARKETPLACE MODEL

Family / CareRequester
        |
        | selects and contracts with
        v
Independent Caregiver

GoSit provides the marketplace infrastructure: discovery, matching, profiles,
verification, screening, booking, scheduling, communication, payments, reviews,
check-in/check-out, and trust & safety — around a relationship the two sides form.
```

GoSit is **not**:

- a childcare or home-care agency;
- an employer of caregivers;
- a direct provider of the underlying care services;
- an insurer of caregivers.

Caregivers are independent marketplace providers. They decide which bookings to accept,
set their own schedule, and are responsible for how they perform care. GoSit does not
supervise the manner or means of care the way an employer would.

This is the same position CLAUDE.md's "Business Model" section states for engineering
purposes; this document carries the fuller reasoning and the open legal questions behind
it. See `docs/ADR-004-marketplace-provider-model.md` for the architecture-decision-record
form of this same decision.

## Insurance

The founder has discussed this with a lawyer. The direction that came out of that
conversation:

> Caregivers should have their own insurance. GoSit should not provide insurance
> coverage for caregivers as if they were employees or care providers.

This replaces any earlier idea of an Uber-style master policy covering caregivers during
a booking — no such recommendation exists elsewhere in the repository, and none should be
reintroduced.

```text
11419056 Canada Inc. o/a GoSit
        |
        +-- insures its OWN corporate/platform risks
        |
        +-- marketplace
              |
              +-- Independent Caregiver
                     |
                     +-- caregiver maintains own insurance;
                         GoSit verifies it where required
```

### GoSit's own coverage

Discuss each of the following with a commercial insurance broker; none of this is bound
yet.

| Priority | Coverage | Why |
|---|---|---|
| Essential | Commercial General Liability (CGL) | Claims against GoSit for bodily injury or property damage from GoSit's own corporate operations |
| Essential | Technology E&O / Professional Liability | Claims alleging failure or negligence in the software: matching, booking, verification workflows, notifications, marketplace operations |
| Essential | Cyber / Privacy Liability | Data breaches touching identity data, children's and seniors' information, care-needs data, disability-related data, background-screening information, messages, payment-related information |
| Essential | Marketplace / Platform Liability | Ask the broker directly whether the CGL/E&O covers negligent screening, negligent verification, negligent matching or referral, failure to suspend a known-risk caregiver, and misrepresentation of caregiver qualifications |
| **Critical** | Abuse / Molestation Exposure | The marketplace involves children, seniors, and other vulnerable people. The broker must say explicitly whether abuse, molestation, neglect, or misconduct are included, excluded, or need a specific endorsement or separate policy — do not assume standard CGL covers this |
| Important | Crime / Fidelity / Social Engineering | Payment diversion, fraud, social engineering, internal theft, account compromise |
| Before fundraising | Directors & Officers (D&O) | Especially before outside investors or external directors/advisors join |
| As the company hires | Employment Practices Liability | For GoSit's own employees, not caregivers |
| If applicable | Non-Owned / Contingent Automobile Liability | Only relevant if the platform enables caregivers to transport a care recipient or a pet — standard CGL does not automatically cover this |

Pet care raises its own questions for the broker: whether GoSit's platform policy needs
to specifically contemplate pet-care marketplace activity, animal injury claims, property
damage, bites and scratches, lost pets, and negligent-matching allegations arising from a
pet-care booking.

### Caregiver insurance

Caregivers, as independent providers, may be required to carry their own coverage before
they can be listed for certain services:

- Commercial General Liability
- Professional Liability, where applicable to the service
- Service-specific liability coverage
- Appropriate automobile insurance, if the caregiver transports a care recipient or a pet

**A $2M liability baseline is a planning assumption, not a stated requirement.** It must
be confirmed by counsel and an insurance broker before it is presented as a real
requirement anywhere — internally or to users.

Verification workflow, statuses, and expiry handling for caregiver insurance are in
`docs/TRUST_AND_SAFETY.md`.

## Revenue model

Primary MVP revenue: a marketplace take rate on completed bookings.

- Planning range: 12%–18%.
- Working modelling assumption used in code today: **15%**
  (`PlatformFeePolicy.DefaultCommissionRate`, configurable via `Payments:CommissionRate`).
  This is a placeholder to make the code work, not an agreed price — see PLAN.md Phase 6.

Potential future revenue, not yet built or decided:

- **GoSit Plus** — family subscription (already seeded as a $12/month plan; price and
  feature set are placeholders, see PLAN.md Phase 8).
- **GoSit Pro** — caregiver premium tools ($19/month seeded plan, same caveat).
- Employer / B2B benefits — employers subsidizing marketplace access for their staff.
- Instant payout fee — only if it makes sense once payouts are proven out.
- Verification-related fees — only if transparent and justified.

Do not over-monetize before marketplace liquidity (enough families and caregivers
actually transacting) is proven.

Payment architecture uses Stripe / Stripe Connect and is designed to avoid unnecessary
custody of funds by GoSit. The final structure should be reviewed with legal, accounting,
and payment advisors before launch.

## Open legal questions

- Is "GoSit" registered as a business name under Ontario's Business Names Act?
- What limitation-of-liability and dispute-resolution language is actually enforceable
  under Ontario's Consumer Protection Act, 2002, given the platform serves children,
  seniors, and other vulnerable people?
- Does the Caregiver Service Agreement's independent-contractor framing hold up given
  GoSit sets a minimum hourly rate and requires background checks — both facts a
  labour tribunal weighs toward "employee"?
- Should a future subsidiary be created, and when?

See `docs/legal/` for the drafts these questions are attached to
(`Go-Sit-Terms-of-Service-DRAFT.docx`, `Go-Sit-Caregiver-Service-Agreement-DRAFT.docx`,
`Go-Sit-Privacy-Policy-DRAFT.docx`, `Go-Sit-Subscription-Terms-DRAFT.docx`,
`Go-Sit-Accessibility-Policy-DRAFT.docx`), none of which have been reviewed by a lawyer
yet.

## Open insurance questions

- Which of the essential/critical coverages above does the company already carry, if any?
- Does a broker's CGL or E&O quote actually include the abuse/molestation endorsement, or
  does that need a separate policy?
- Is $2M a workable baseline for caregiver liability coverage, or does it price out
  caregivers the marketplace needs in its early supply?
- How is caregiver insurance actually verified — a document upload staff review, like
  certifications today, or an integration with a verification service?
