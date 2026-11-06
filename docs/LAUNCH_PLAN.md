# GoSit launch plan and early-access campaign

Status: draft for the owner's review. Nothing here has been run, sent or published. Written
November 2026 against the production state described below; check "Where we are" before
acting on any section, because it goes out of date fast.

## The short version

1. **Recruit caregivers before families.** A family's first search is only worth doing if
   someone good is near them. Aim for a small number of verified, insured caregivers in one
   area, then open to families there.
2. **Pick one launch area.** Ontario is the market, but a marketplace works street by street.
   Choose one city or region and do not market outside it yet. The company's registered
   office is in Windsor, which may give a natural local network; that is the owner's call.
3. **Collect an early-access list now; do not open to the public until the gates below are
   green.** The site, the early-access form, email and Google sign-in work today.
4. **Say only what is true today.** No "background-checked" or "verified" language in
   marketing until checks are really being done, and never "safe" or "guaranteed" (see
   "Wording rules").

## Where we are (read before launching)

| Area | State | Blocks launch? |
| --- | --- | --- |
| Public site, `/privacy`, `/terms` | Live at www.gosits.com | No |
| Early-access form | Live; sends to the admin enquiries list | No |
| Email delivery | Working (Brevo) | No |
| Sign in with Google | Working; consent screen is in "Testing" (100 test users) until published | Yes for public launch |
| Legal documents | Live but marked "pending legal review" | **Yes** |
| Registration | **Open in production right now** (`Registration__Open=true`) | Decide: close it until gates are green, or keep it for invited testers only (`Registration__InvitedEmails`) |
| First administrator account | Not confirmed to exist | **Yes**: nobody can approve a caregiver without one |
| Screening (Certn) | Built, never run; no key or webhook secret configured, so every check waits for staff | **Yes** |
| Vulnerable sector check | Done by staff review of an uploaded document; not sent to Certn | Yes, for care of people |
| Caregiver insurance | Required to be listed or booked (new); staff verify the uploaded policy | Needs staff to be ready to review |
| Payments (Stripe) | Secret key set; production webhook not confirmed | **Yes**: no live bookings without it |
| Monitoring and alerts | Nothing sending to Log Analytics | Soon after launch |

**Gates for opening to the public** (all must be true): legal documents cleared by counsel
and the review notice removed; admin account exists and staff know the review queue; Stripe
webhook live and one real test payment refunded; screening process real (Certn working, or
the staff process written down and tested end to end); Google consent screen published;
privacy and terms linked from every sign-up path.

## Who we are talking to

**Caregivers (first).** Independent people who want to set their own rate and hours:
experienced nannies and after-school carers, personal support workers looking for flexible
private work, students in early-childhood, nursing or social-work programs, retirees with
senior-care experience, experienced dog walkers and pet sitters. What they care about: being
paid properly for time worked, choosing their hours, not being an employee of an agency, and
trusting that families are real.

**Families (second).** Parents arranging child care, and adult children arranging care for a
parent: the "sandwich generation". What they care about: knowing who is coming into the
home, being able to talk to someone first (the platform's video interview), clear pricing,
and not being locked into an agency.

## Positioning

> **Trusted care for every stage of family life.**

For families: *Find independent caregivers near you, meet them on video first, and book
through one place.* For caregivers: *Set your own rate and hours. Families come to you.*

What makes this different, in true statements the product already backs up:

- Care for children, seniors, specialised non-medical support and pets, in one place.
- Caregivers are independent and set their own rates and hours.
- A video interview inside GoSit before anyone is booked.
- Check-in and check-out are recorded for every booking.
- Every caregiver signs the Caregiver Service Agreement and, to be listed, has a verified
  identity, the right screening for the care they offer, and their own liability insurance
  verified by GoSit. (Use the screening and insurance lines only once those are really
  running; see gates.)

## Wording rules (these come from the company's legal position)

- Caregivers are **independent**. Never "our caregivers", "GoSit staff", "employed by".
  Say "caregivers on GoSit".
- GoSit **connects** families and caregivers. It does not provide care.
- Never "safe", "100% safe", "guaranteed", "fully vetted" or similar. Say what is checked:
  "identity verification and a vulnerable sector check for care of people", and that
  "screening reduces risk; it does not remove it".
- Do not say GoSit insures caregivers. Say "caregivers carry their own insurance, which GoSit
  verifies".
- No dollar liability figure anywhere until counsel and a broker confirm it.
- It is **non-medical** care. No medication, nursing, therapy or diagnosis.
- Do not use real caregivers' or families' names, photos or stories without written consent.
  The site's photographs are licensed stock and must not be presented as real GoSit
  caregivers.
- No "launched in" or "available in" claims for places that are not open.

## Consent and email: a problem to fix first

The early-access form's consent line reads: *"I agree that GoSit may use these details to
reply to me. We do not add anyone to a mailing list."* The contact page makes the same
promise. Under Canada's anti-spam law (CASL) and simple honesty, that means:

- **Existing sign-ups can be replied to, not marketed to.** Do not send campaign emails to
  the people already on the list.
- **Change the wording going forward** so new sign-ups agree to a specific, limited thing,
  for example: *"I agree that GoSit may email me when accounts open in my area, and about
  GoSit's launch. I can unsubscribe at any time."* Keep it a separate, unticked checkbox.
- Every email must name the sender (11419056 Canada Inc., operating as GoSit, with a mailing
  address) and include a working unsubscribe link, honoured within 10 business days.
- Keep a record of when and how each person consented.
- A one-time "accounts are now open" message to existing sign-ups is a grey area. Ask counsel
  before sending it, or ask them to re-confirm interest on the site instead.

## Phases

### Phase 0: Get ready (before any public push)

- Create the admin account; write the staff checklist for reviewing ID, screening documents,
  certifications and insurance. A caregiver who uploads a policy should hear back in two
  business days.
- Decide `Registration__Open`. Recommended: closed, with `Registration__InvitedEmails` for
  the first 5 to 10 caregivers you personally recruit.
- Finish the gates above. Fix the consent wording.
- Choose the launch area and write down the target: for example 15 verified, insured
  caregivers across a few service types before any family is invited.
- Set up simple tracking: the admin enquiries list already receives early-access answers;
  add a shared spreadsheet or the admin view as the source of truth. Add privacy-respecting
  analytics to the public site only with a notice.

### Phase 1: Recruit the first caregivers (weeks 1 to 6)

Goal: 15 to 25 caregivers in the launch area who have started, and 10 to 15 who are fully
listed (profile, documents, checks, insurance, agreement).

- **Personal outreach** to people the owner already knows in childcare, senior care and pet
  care. Invite by email to sign up; add them to the invited list.
- **Local channels**: college and university early-childhood, PSW, nursing and social-work
  programs (placement offices and student groups); community and faith groups; local
  caregiver and parenting groups where posting is allowed; community notice boards.
- **Caregiver referral**: the app already has referral codes. Offer a simple thank-you that is
  paid only after the referred caregiver is listed, not on sign-up. Confirm the amount and
  that it complies with the platform's rules before announcing it.
- **Message**: set your rate and hours; families come to you; you stay independent. Be
  upfront about the real steps: ID verification, a screening check, and carrying your own
  liability insurance. Honest friction filters for serious people.
- **Help them finish.** The Documents page shows what is missing. Offer a 15-minute
  onboarding call to anyone stuck. Most drop-off will be at insurance and screening.

### Phase 2: Early-access list for families (run alongside Phase 1)

Goal: 100 to 200 families in the launch area who have said they want care and when.

- Point everything to `www.gosits.com/get-started` (the early-access form). It already asks
  who the care is for, the kind of help, timing and schedule, without collecting names,
  health details or addresses.
- Channels: local parent and family-caregiver groups, school and daycare newsletters (with
  permission), employers' HR or wellness lists, community health centres and seniors'
  organisations, a short press note to local news.
- Offer: first access, plus (if approved) a small first-booking benefit. Do not promise a
  price or discount that has not been set in the product.

### Phase 3: Soft launch (when gates are green and supply target is met)

- Open sign-up to families in the launch area only, in waves: invite 20 to 30 families from
  the list, watch what breaks, then widen.
- Keep a human on every first booking: check that the caregiver was matched, the video
  interview worked, check-in and check-out recorded, payment cleared.
- Ask each first family and caregiver for feedback within a week.

### Phase 4: Open the area, then repeat (month 3 onward)

- Open to the public in the first area. Publish real, consented stories.
- Only then pick a second area and repeat Phase 1 there.

## Early-access campaign

### Campaign idea

**"Be first through the door."** (Already the headline on the early-access page.) Two parallel
asks:

- *Caregivers*: "Set your own rate and hours. Be among the first caregivers on GoSit."
- *Families*: "Find independent caregivers near you, and meet them on video before you book.
  Get first access."

### Landing page copy changes (small)

- Early-access page: keep the headline. Change the consent line as described above.
- Add one line under the form: *"We will only email you about GoSit's launch in your area. You
  can unsubscribe at any time."* (only once the consent wording changes.)
- Do not add a countdown or scarcity claims.

### Copy: caregiver recruitment

**Short post / message (about 60 words)**

> Looking for flexible care work you control? GoSit is opening in [area]. Set your own rate and
> hours, accept the requests you want, and get paid for the time you work. Caregivers on GoSit
> are independent and carry their own insurance. We verify identity and screen for the care you
> offer. Join the early list: gosits.com/get-started

**Email to a person you know (personal, not a mass mailing)**

> Subject: Would you try GoSit as a caregiver?
>
> Hi [name], I'm starting GoSit, a marketplace that connects families in [area] with
> independent caregivers for children, seniors and pets. You set your rate and hours and
> families come to you. Before you're listed we verify your ID, run the screening for the
> care you offer, and check your own liability insurance. I'd value your honest feedback
> and, if it suits you, you as one of the first caregivers. Can I send you an invitation?

**Flyer (campus / community board)**

> **Care work on your terms.**
> Set your own rate and hours. Families in [area] find you.
> Independent caregivers: childcare · senior care · pet care
> Identity verification and screening for the care you offer. You carry your own insurance.
> gosits.com/get-started

### Copy: families

**Social post**

> Meeting someone new before they come to your home should not be a leap of faith. GoSit is
> opening in [area]: find independent caregivers for children, seniors and pets, meet them on
> video first, and book in one place. Get early access: gosits.com/get-started

**Local-group post (informative, not a hard sell)**

> Hi everyone. I'm building GoSit, a way for families in [area] to find independent
> caregivers (child care, senior companionship, pet care). Caregivers set their own rates;
> you can video-chat with them before booking; check-in and check-out are recorded. We're
> collecting an early list so we can open carefully. If that would help your family, the link
> is gosits.com/get-started. Happy to answer questions here.

**Short ad lines (if you later run paid ads)**

- Meet your caregiver on video before you book.
- Care for kids, parents and pets, from independent caregivers near you.
- Early access for families in [area].

### Emails to send (only after consent wording is fixed; sender must be identified)

**1. Confirmation (sent right after the form, to the person who just signed up)**

> Subject: You're on the GoSit early list
>
> Thanks, [first name]. We have your details for [area]. GoSit is opening carefully, one step
> at a time, and we will email you when accounts open near you. If that's not what you
> expected, reply and tell us, or unsubscribe at the link below.
>
> 11419056 Canada Inc., operating as GoSit · [mailing address] · Unsubscribe

**2. "We're opening in your area" (once the gates are green)**

> Subject: GoSit is opening in [area]
>
> You asked to hear first. GoSit is now open to [families / caregivers] in [area]. [One
> sentence on what to do next: sign up, then for caregivers finish your documents.]
>
> A few honest notes: caregivers on GoSit are independent; screening reduces risk but does not
> remove it; and this is non-medical care. If you need regulated health care, please speak to a
> licensed provider.
>
> [button: Create your account] · Unsubscribe

**3. Caregiver follow-up (to caregivers who started but haven't finished)**

> Subject: You're [one] step from being listed
>
> Your GoSit profile still needs: [missing documents, from the Documents page]. Families can't
> find you until your required documents are approved. We aim to review documents within two
> business days. Need a hand? Reply to this email.

### Referral (later)

Caregiver-to-caregiver referrals exist in the product. Family referral is not built yet, so do
not advertise one.

## What to measure

- **Supply**: caregivers who signed up, who uploaded each document, who are fully listed,
  and time from sign-up to listed. This is the number that gates launch.
- **Demand**: early-access sign-ups by area and by kind of care.
- **Match readiness**: for each service, listed caregivers within range of waiting families.
- **First bookings**: requests made, accepted, completed; time to first reply.
- **Trust signals**: interviews held, incidents reported, reviews left.
- **Cost**: money and time spent per listed caregiver. Keep early spend near zero; outreach
  and word of mouth first.

## Budget and rough timeline

- Phase 0: 1 to 3 weeks, mostly the owner's and counsel's time.
- Phase 1 and 2: weeks 1 to 6, near-zero cash (printing, a few boosted posts if wanted).
- Phase 3: weeks 6 to 10, when supply and gates are ready.
- Phase 4: month 3 onward.

Dates depend on counsel, Stripe live mode and screening, not on marketing.

## Risks

- **Opening families before there are caregivers.** Wait for the supply target.
- **A reputation hit from one bad outcome.** Moderate by hand at first, keep the incident
  process ready, and keep the wording honest.
- **Marketing claims that outrun the product.** Keep the wording rules; have counsel review
  anything that mentions safety, checks or insurance.
- **Privacy slip.** Never ask for names, health details or addresses on the public form.
- **Email law.** Consent wording and unsubscribe must be right before the first campaign.

## Decisions needed from the owner

1. Launch area and the first three service types to focus on.
2. Whether to close `Registration__Open` now.
3. Whether to offer any first-booking benefit or caregiver referral thank-you, and the amount.
4. Who handles caregiver document reviews and how fast.
5. Approval of the new consent wording and the three emails, ideally reviewed by counsel.
6. Whether to ask counsel about a one-time "now open" note to the existing early-access list.
