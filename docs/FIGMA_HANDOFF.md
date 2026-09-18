# FIGMA_HANDOFF.md — GoSit Design-to-Engineering Map

## Figma

https://www.figma.com/design/sJQ271UkXOx3Uiklp2Bn41/GoSitly-Wireframe

## Product Expansion

The original wireframe was focused on parents, children, and babysitters.

GoSit now supports:
- Child Care
- Senior Care
- Specialized Non-Medical Support
- Respite & Family Support
- Pet Care — built (dog walking, drop-in visits, in-home pet sitting, boarding); no
  wireframes exist for it yet, see "Pet Care" below

Existing labels like Parent/Sitter/Babysitter are legacy design labels, not final domain constraints.

## Existing Nodes

| Surface | Node |
|---|---:|
| Landing Page | `1:3` |
| Login | `49:8` |
| Sign Up | `77:12` |
| Parent Dashboard | `91:2` |
| Search Dropdown | `107:257` |
| Inbox Empty | `127:2` |
| Inbox Messages | `136:30` |
| Message Content | `139:56` |
| Saved | `129:4` |
| Caregiver Inbox | `140:103` |
| Caregiver Stats | `148:3` |
| Caregiver Earnings | `148:31` |
| Caregiver Wallet | `148:59` |
| Caregiver Account | `148:87` |

## New Screens Needed

### Shared
- Care service selection
- Notifications
- Safety/reporting
- Mobile navigation

### Care Requester
- Onboarding
- Care recipient list
- Child recipient
- Adult/senior recipient
- CareNeedsProfile
- Emergency contacts
- Service-specific search
- Caregiver detail
- Care request
- Booking summary
- Payment
- Receipt
- Review

### Senior Care
- Senior care landing
- Senior recipient setup
- Mobility needs
- Dementia/companionship preferences
- Recurring care request

### Specialized Support
- Special-needs support selection
- Communication needs
- Sensory needs
- Behavioral support
- Required caregiver skills
- Respite booking

### Caregiver
- Service selection
- Skills
- Certifications
- Qualification status
- Verification
- Availability
- Booking request detail
- Check-in/out
- Report a safety issue on a booking

### Caregiver Trust Onboarding (planned — see docs/TRUST_AND_SAFETY.md)
- Identity Verification
- Background Screening
- Vulnerable Sector Check
- References
- Certifications
- Insurance
- Skills
- Service Eligibility
- Verification Progress (overall onboarding status)

### Caregiver Account — Trust & Verification section (planned)
A section on the caregiver's own account screen showing each check's status:
- Identity
- Background Check
- Vulnerable Sector
- CPR
- First Aid
- Insurance
- Transportation
- Specialized Credentials

### Family View — Trust Profile (planned)
Trust Profile badges on a caregiver's public profile (factual status language only — see
docs/TRUST_AND_SAFETY.md, "GoSit Trust Profile" — never "100% safe" or "guaranteed").

### Pet Care (built in the backend; no wireframes exist yet)
- Pet Care service selection
- PetProfile creation/edit
- Pet-caregiver search (species, dog size, distance, rate, insurance filters)
- Pet-care booking (per-night pricing for boarding/in-home sitting)
- Pet instructions (feeding, medication, behaviour, vet contact)
- Pet-care incident report (lost pet, animal injury, bite/scratch, unsafe environment)
- Pet-caregiver qualifications display

### Admin
- Verification queue
- Certification review
- Insurance review (planned — see docs/TRUST_AND_SAFETY.md)
- Expiry management — certifications and, once built, insurance (planned)
- Incident report / incident queue
- User report
- Suspension
- Appeals (planned)
- Audit timeline

## New Landing Positioning

Hero:
> **Find trusted care for the people who matter most.**

Supporting message:
> Connect with verified caregivers for children, seniors, and individuals who need specialized support.

Service cards:
- Child Care
- Senior Care
- Specialized Support
- Respite & Family Support

## Design Tokens — Preliminary

- Teal `#03888F`
- Orange `#E39435`
- Gold `#F9BB06`
- Navigation Orange `#FF9C00`
- White `#FFFFFF`
- Light Surface `#F5F5F5`
- Placeholder Gray `#D9D9D9`

Do not implement absolute Figma coordinates literally. Build responsive components.
