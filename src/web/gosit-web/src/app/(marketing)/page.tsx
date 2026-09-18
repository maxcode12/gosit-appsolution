import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/button";
import { ShieldIcon, StarIcon } from "@/components/ui/icons";
import { SiteFooter } from "@/features/marketing/components/site-footer";
import { HeroSearch } from "@/features/marketing/components/hero-search";
import { AppShowcase } from "@/features/marketing/components/app-showcase";
import { SiteHeader } from "@/features/marketing/components/site-header";

export const metadata: Metadata = {
  // Absolute, or the root layout's "%s | GoSit" template appends the brand to a title
  // that already opens with it — "GoSit — Trusted care… | GoSit" in the tab and in
  // every search result for the one page most people meet first.
  title: { absolute: "GoSit — Trusted care for every stage of family life" },
  description:
    "Verified caregivers across Ontario for children, seniors, and adults who need support. Every caregiver is identity-checked and vulnerable-sector screened.",
};

/**
 * The public landing page.
 *
 * Replaces the Phase 0 placeholder. Figma node `1:3` is still not inspectable on this
 * seat, so this is designed in-repo against the product's own token system.
 *
 * The argument it makes, in order: GoSit is for the whole arc of family life, not just
 * babysitting; here is the care you can actually book; here is how little work it is;
 * and here is precisely what we check before someone reaches your door. That last part
 * is the product — for a marketplace where the thing being bought is trust, the safety
 * section is not a footnote, it is the pitch.
 */
export default function LandingPage() {
  return (
    <>
      <SiteHeader />

      <main>
        <Hero />
        <CareTypes />
        <HowItWorks />
        <TrustAndSafety />
        <CaregiverApp />
        <ForCaregivers />
      </main>

      <SiteFooter />
    </>
  );
}

/**
 * A photograph, a sentence, and one box to type into.
 *
 * Full-bleed: the image is the ground the words sit on, rather than a picture beside
 * them. That is the harder of the two arrangements to keep readable, and the two things
 * holding it up are the scrim and the crop — a face behind a headline is the failure
 * mode, and both are set to prevent it rather than to hope.
 *
 * Everything inside is pinned to fixed light-on-dark values instead of the theme's text
 * colours. The palette flips with the reader's system setting; a photograph does not.
 *
 * The photograph is illustrative and licensed from Unsplash; the people in it are not
 * GoSit caregivers. See public/images/CREDITS.md — that distinction is not decorative,
 * and nothing here should ever caption one of them as a real caregiver.
 */
function Hero() {
  return (
    <>
      <section className="relative isolate overflow-hidden">
        <Image
          src="/images/hero.jpg"
          alt="A caregiver and a young child reading a book together, both laughing."
          fill
          // The only image above the fold on the page people land on first.
          priority
          sizes="100vw"
          /*
           * Pushed off-centre on purpose. The pair sit in the middle of the frame, and a
           * centred crop puts a face directly behind the headline — the failure this
           * layout has to design around rather than hope about. Shifting the focal point
           * right moves them clear of the text and leaves the tent and wall as the ground
           * the words sit on.
           */
          className="object-cover object-[72%_center] sm:object-[68%_center]"
        />

        {/*
          The scrim, and the reason the text is readable at all.

          Fixed dark values rather than theme tokens: the ground here is a photograph, not
          the page, so it must not invert with the palette — a scrim that goes pale in
          light mode would leave white text on a lit room. Vertical on a phone, where the
          copy spans the full width; horizontal on a wide screen, where it does not.
        */}
        <div
          aria-hidden
          className="absolute inset-0 bg-gradient-to-t from-[rgba(12,20,22,0.94)] via-[rgba(12,20,22,0.72)] to-[rgba(12,20,22,0.45)] lg:bg-gradient-to-r lg:from-[rgba(12,20,22,0.94)] lg:via-[rgba(12,20,22,0.75)] lg:to-transparent"
        />

        <div className="relative mx-auto flex min-h-[34rem] max-w-6xl flex-col justify-center px-4 py-16 sm:px-6 sm:py-20 lg:min-h-[40rem]">
          <div className="max-w-xl">
            <span className="inline-flex items-center gap-2 rounded-pill border border-white/25 bg-white/10 px-3.5 py-1.5 font-control text-sm font-medium text-text-on-deep backdrop-blur-sm">
              <ShieldIcon />
              Every caregiver is background-checked
            </span>

            <h1 className="mt-6 font-heading text-4xl font-extrabold leading-[1.08] tracking-tight text-text-on-deep sm:text-5xl lg:text-6xl">
              Trusted care for every stage of{" "}
              <span className="text-accent-on-deep">family life</span>
            </h1>

            <p className="mt-6 max-w-lg text-lg leading-relaxed text-text-on-deep/85">
              Whether it is your child after school, your mother on a Tuesday morning, or
              a family member who needs specialised support — find someone verified,
              nearby, and right for the job.
            </p>

            <HeroSearch />

            <p className="mt-4 text-sm text-text-on-deep/70">
              Free to join. No charge until a caregiver confirms your booking.{" "}
              <Link
                href="/signup"
                className="underline underline-offset-2 hover:text-text-on-deep"
              >
                Work as a caregiver
              </Link>
            </p>
          </div>
        </div>
      </section>

      {/*
        Lifted out of the photograph. Three claims are the argument, and an argument set
        over someone's living room is harder to read than one set on a plain ground.
      */}
      <div className="mx-auto max-w-6xl px-4 pb-4 pt-14 sm:px-6 sm:pt-16">
        <TrustBar />
      </div>
    </>
  );
}

/**
 * The three claims that survive scrutiny.
 *
 * Deliberately not "10,000 happy families" — GoSit has not launched, and inventing
 * social proof on the one page a family reads before trusting us with their child is
 * exactly the wrong place to start. These are statements about how the product works,
 * every one of which the code actually enforces.
 */
const TRUST_POINTS = [
  {
    title: "Identity and vulnerable-sector checked",
    body: "Nobody appears in search until both checks clear. No exceptions, no “pending” badge.",
  },
  {
    title: "You are charged for time actually worked",
    body: "Your caregiver checks in and out. The final bill follows the clock, not the estimate.",
  },
  {
    title: "Qualified for the care you booked",
    body: "Dementia support, autism support, first aid — matched against what they are certified for.",
  },
];

function TrustBar() {
  return (
    <ul className="mx-auto grid max-w-5xl gap-4 sm:grid-cols-3">
      {TRUST_POINTS.map((point) => (
        <li key={point.title} className="surface-card p-6">
          <h2 className="font-heading text-base font-semibold text-text-primary">
            {point.title}
          </h2>
          <p className="mt-2 text-sm leading-relaxed text-text-secondary">{point.body}</p>
        </li>
      ))}
    </ul>
  );
}

/**
 * The five care categories, matching the service taxonomy the API actually serves —
 * `docs/PLAN.md` Phase 3 added Pet Care, and this list has to keep up with it rather
 * than being written once at launch. Written for the person searching, not the
 * database: "someone to be there after school" rather than "AFTER_SCHOOL_CARE".
 */
const CARE_TYPES = [
  {
    name: "Child care",
    image: "/images/child-care.jpg",
    alt: "A caregiver and a young girl building with coloured blocks at a table.",
    blurb: "Babysitting, nannies, after-school and overnight care.",
    examples: ["Babysitting", "Nanny care", "After-school", "Overnight"],
  },
  {
    name: "Senior care",
    image: "/images/senior-care.jpg",
    alt: "A caregiver holding an older woman's hands, helping her up from a chair.",
    blurb: "Company, meals, errands and getting to appointments.",
    examples: ["Companionship", "Meal support", "Mobility", "Dementia care"],
  },
  {
    name: "Specialized support",
    image: "/images/specialised-support.jpg",
    alt: "A companion walking with someone in a wheelchair across a field at sunset.",
    blurb: "Caregivers experienced with additional needs.",
    examples: ["Autism support", "Communication", "Behavioural", "Respite"],
  },
  {
    name: "Family support",
    image: "/images/family-support.jpg",
    alt: "Three adults of different generations cooking a meal together in a kitchen.",
    blurb: "The practical help that keeps a household going.",
    examples: ["Respite care", "Transport", "Light household", "Routine support"],
  },
  {
    name: "Pet care",
    image: "/images/pet-care.jpg",
    alt: "A person walking a golden retriever on a leash along a paved path.",
    blurb: "Walks, drop-in visits, in-home sitting and boarding.",
    examples: ["Dog walking", "Drop-in visits", "In-home sitting", "Boarding"],
  },
];

function CareTypes() {
  return (
    <section id="care" className="scroll-mt-20 border-t border-border bg-surface">
      <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6 sm:py-24">
        <SectionHeading
          eyebrow="What you can book"
          title="Care for the whole family, not just the youngest"
          body="GoSit started with childcare. Families kept asking for help with their parents too, so that is what we built."
        />

        <ul className="mt-14 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
          {CARE_TYPES.map((type) => (
            <li key={type.name} className="surface-card-interactive overflow-hidden">
              {/*
                A fixed aspect ratio rather than a fixed height, so four photographs of
                different proportions still line up as four cards rather than four
                nearly-identical rectangles.
              */}
              <div className="relative aspect-[4/3] overflow-hidden bg-surface-muted">
                <Image
                  src={type.image}
                  alt={type.alt}
                  fill
                  sizes="(min-width: 1024px) 25vw, (min-width: 640px) 50vw, 100vw"
                  className="object-cover"
                />
              </div>
              <div className="p-6">
                <h3 className="font-heading text-lg font-semibold text-text-primary">
                  {type.name}
                </h3>
                <p className="mt-1.5 text-sm leading-relaxed text-text-secondary">
                  {type.blurb}
                </p>
                <ul className="mt-4 flex flex-wrap gap-1.5">
                  {type.examples.map((example) => (
                    <li
                      key={example}
                      className="rounded-pill bg-surface-muted px-2.5 py-1 text-xs font-medium text-text-secondary"
                    >
                      {example}
                    </li>
                  ))}
                </ul>
              </div>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}

const STEPS = [
  {
    title: "Tell us who needs care",
    body: "Their age, what a good day looks like, and anything a caregiver should know. Private, and shared only with someone you book.",
  },
  {
    title: "Choose someone nearby",
    body: "Browse verified caregivers in your area with their rate, experience and certifications. Message them before you commit.",
  },
  {
    title: "Book, and pay for the time worked",
    body: "They confirm, they check in when they arrive, and you are charged for the hours they were actually there.",
  },
];

function HowItWorks() {
  return (
    <section id="how" className="scroll-mt-20">
      <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6 sm:py-24">
        <SectionHeading
          eyebrow="How it works"
          title="Three steps, and nothing taken up front"
          body="No subscription to browse, no fee to message, and no charge until your caregiver says yes."
        />

        <ol className="mt-14 grid gap-6 md:grid-cols-3">
          {STEPS.map((step, index) => (
            <li key={step.title} className="relative">
              <span
                aria-hidden
                className="grid size-11 place-items-center rounded-full bg-brand-primary font-heading text-lg font-bold text-text-on-brand shadow-brand"
              >
                {index + 1}
              </span>
              <h3 className="mt-5 font-heading text-lg font-semibold text-text-primary">
                {step.title}
              </h3>
              <p className="mt-2 text-sm leading-relaxed text-text-secondary">{step.body}</p>
            </li>
          ))}
        </ol>
      </div>
    </section>
  );
}

/**
 * Exactly what is checked, in the order it happens. Concrete enough to be verifiable —
 * a vague "we take safety seriously" reassures nobody who is actually worried.
 */
const CHECKS = [
  {
    title: "Government ID, matched to the person",
    body: "Every caregiver proves who they are before they can be listed.",
  },
  {
    title: "Vulnerable sector check",
    body: "Required for anyone working with children or vulnerable adults. Not optional, not skippable.",
  },
  {
    title: "Certifications verified, not just claimed",
    body: "First Aid, CPR and specialist training are reviewed by our team before they count.",
  },
  {
    title: "Your address stays private until you book",
    body: "Searches show an approximate area. Your street address is shared only once you have chosen someone.",
  },
  {
    title: "Care needs shared with one caregiver, once",
    body: "Allergies, triggers and routines go to the person you booked — after they accept, and to nobody else.",
  },
  {
    title: "Every booking on the record",
    body: "Times, messages and payments are kept, so there is always an account of what happened.",
  },
];

function TrustAndSafety() {
  return (
    <section id="safety" className="scroll-mt-20 border-y border-border bg-surface">
      <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6 sm:py-24">
        <SectionHeading
          eyebrow="Trust & safety"
          title="What we check before anyone reaches your door"
          body="You are letting a stranger into your home to look after someone you love. Here is exactly what stands behind that."
        />

        <ul className="mt-14 grid gap-x-10 gap-y-8 md:grid-cols-2">
          {CHECKS.map((check) => (
            <li key={check.title} className="flex gap-4">
              <span
                aria-hidden
                className="mt-0.5 grid size-9 shrink-0 place-items-center rounded-full bg-brand-soft text-brand-primary"
              >
                <ShieldIcon />
              </span>
              <div>
                <h3 className="font-heading text-base font-semibold text-text-primary">
                  {check.title}
                </h3>
                <p className="mt-1 text-sm leading-relaxed text-text-secondary">
                  {check.body}
                </p>
              </div>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}

/**
 * Real screenshots of the caregiver app, not a rendering of one.
 *
 * Placed right before the caregiver pitch: the copy there makes promises — set your own
 * rate, get paid for the hours you worked — and this is where someone can see the screen
 * that keeps them. See public/app-screenshots for how these were captured.
 */
function CaregiverApp() {
  return (
    <section className="border-t border-border bg-surface">
      <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6 sm:py-24">
        <SectionHeading
          eyebrow="The caregiver app"
          title="Your day and your earnings, on your phone"
          body="Check in when you arrive, see what's coming up, and watch your money clear — without opening a laptop."
        />

        <div className="mt-14">
          <AppShowcase
            screens={[
              {
                src: "/app-screenshots/today-cropped.png",
                alt: "The GoSit app's Today screen, showing a dog walking booking and an 'I have arrived — check in' button.",
                caption: "What's next, and the one button that matters when you get there.",
              },
              {
                src: "/app-screenshots/earnings-cropped.png",
                alt: "The GoSit app's Earnings screen, showing total earned, a weekly chart, and recent payouts.",
                caption: "Every dollar, when it clears, and why — before it lands in your account.",
              },
              {
                src: "/app-screenshots/inbox-cropped.png",
                alt: "The GoSit app's Inbox screen, showing booking confirmations and interview notifications.",
                caption: "Every update in one inbox, so nothing gets missed between apps.",
              },
              {
                src: "/app-screenshots/account-cropped.png",
                alt: "The GoSit app's Account screen, showing document, insurance, payout, and earnings menu rows.",
                caption: "Documents, insurance, and payouts — all in one place, always current.",
              },
            ]}
          />
        </div>
      </div>
    </section>
  );
}

function ForCaregivers() {
  return (
    <section id="caregivers" className="scroll-mt-20">
      <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6 sm:py-24">
        <div className="overflow-hidden rounded-card bg-brand-deep shadow-lg">
          <div className="grid gap-10 p-8 sm:p-14 lg:grid-cols-2 lg:items-center">
            <div>
              <span className="inline-flex items-center gap-2 rounded-pill bg-white/15 px-3.5 py-1.5 font-control text-sm font-medium text-text-on-deep">
                <StarIcon filled />
                For caregivers
              </span>

              <h2 className="mt-6 font-heading text-3xl font-extrabold leading-tight tracking-tight text-text-on-deep sm:text-4xl">
                Set your rate. Choose your hours. Get paid for the time you work.
              </h2>

              <p className="mt-5 text-lg leading-relaxed text-text-on-deep/80">
                You decide what you charge and when you are free. Families come to you
                already knowing your experience and what you are certified for.
              </p>

              <div className="mt-8">
                <Link href="/signup" className={buttonClassName("secondary", "", "lg")}>
                  Join as a caregiver
                </Link>
              </div>
            </div>

            <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-1">
              {[
                "You set your own hourly rate",
                "Accept or decline every request",
                "See the care needs before the day",
                "One clear statement of every fee",
              ].map((point) => (
                <li
                  key={point}
                  className="rounded-control bg-white/10 px-4 py-3.5 text-sm font-medium text-text-on-deep"
                >
                  {point}
                </li>
              ))}
            </ul>
          </div>
        </div>
      </div>
    </section>
  );
}

function SectionHeading({
  eyebrow,
  title,
  body,
}: {
  eyebrow: string;
  title: string;
  body: string;
}) {
  return (
    <div className="mx-auto max-w-2xl text-center">
      <p className="font-control text-sm font-semibold uppercase tracking-widest text-brand-primary">
        {eyebrow}
      </p>
      <h2 className="mt-3 font-heading text-3xl font-extrabold leading-tight tracking-tight text-text-primary sm:text-4xl">
        {title}
      </h2>
      <p className="mt-4 text-lg leading-relaxed text-text-secondary">{body}</p>
    </div>
  );
}
