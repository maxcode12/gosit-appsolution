import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/button";
import {
  ArrowRightIcon,
  CalendarCheckIcon,
  CheckIcon,
  ClockIcon,
  IdCardIcon,
  LockIcon,
  VideoCameraIcon,
} from "@/components/ui/marketing-icons";
import { AppShowcase } from "@/features/marketing/components/app-showcase";
import { Faq } from "@/features/marketing/components/faq";
import { FAMILY_FAQ } from "@/features/marketing/components/faq-data";
import { FinalCta } from "@/features/marketing/components/final-cta";
import { Container, Eyebrow, SectionHeading } from "@/features/marketing/components/section";
import { getStartedHref } from "@/features/marketing/public-mode";

export const metadata: Metadata = {
  // Absolute, or the root layout's "%s | GoSit" template appends the brand to a title
  // that already opens with it.
  title: { absolute: "GoSit — Trusted care for every stage of family life" },
  description:
    "Find verified, independent caregivers across Ontario for children, seniors, adults who need support, and pets. Identity-checked, vulnerable-sector screened, and booked in minutes.",
};

/**
 * The public front page.
 *
 * The argument, in order: GoSit is for the whole arc of family life; here is who it is for;
 * here is the care you can book; here is how little work it is; and here is precisely what
 * stands behind the person at your door. For a marketplace where the thing being bought is
 * trust, the safety section is the pitch, so it sits in the middle of the page rather than
 * at the bottom of it.
 *
 * Nothing here is invented social proof. GoSit has not launched, and a claim about
 * thousands of families on the page a family reads before trusting us with their child
 * would be the worst possible place to begin. Every statement describes how the product
 * works, and the product enforces it.
 */
export default function HomePage() {
  return (
    <>
      <Hero />
      <TrustStrip />
      <Audiences />
      <Services />
      <HowItWorks />
      <Safety />
      <AppSection />
      <FaqSection />
      <FinalCta />
    </>
  );
}

function Hero() {
  return (
    <section className="hero-wash relative overflow-hidden">
      <div aria-hidden className="dot-grid pointer-events-none absolute inset-0 opacity-60" />

      <Container className="relative grid items-center gap-16 pb-24 pt-14 lg:grid-cols-[1.05fr_0.95fr] lg:pb-32 lg:pt-24">
        <div>
          <span className="inline-flex items-center gap-2.5 rounded-full border border-border bg-surface/80 py-1.5 pl-2 pr-4 font-control text-sm font-medium text-text-secondary shadow-xs backdrop-blur">
            <span className="grid size-6 place-items-center rounded-full bg-brand-primary text-text-on-brand">
              <CheckIcon className="size-3.5" />
            </span>
            Verified caregivers across Ontario
          </span>

          <h1 className="font-display mt-8 text-[2.75rem] leading-[1.02] text-text-primary sm:text-6xl lg:text-[4.75rem]">
            Care you can <em className="text-brand-primary">trust</em>, for every stage of family life.
          </h1>

          <p className="mt-7 max-w-xl text-lg leading-relaxed text-text-secondary sm:text-xl">
            Your child after school, your mother on a Tuesday morning, a family member who needs
            specialised support, even the dog. Find someone checked, nearby and right for the job.
          </p>

          <div className="mt-10 flex flex-col gap-3 sm:flex-row">
            <Link href={getStartedHref("family")} className={buttonClassName("primary", "!rounded-full", "xl")}>
              Find a caregiver
              <ArrowRightIcon />
            </Link>
            <Link href={getStartedHref("caregiver")} className={buttonClassName("ghost", "!rounded-full", "xl")}>
              Become a caregiver
            </Link>
          </div>

          <ul className="mt-10 flex flex-col gap-3 text-sm font-medium text-text-secondary sm:flex-row sm:flex-wrap sm:gap-x-7">
            {["Identity and vulnerable-sector checked", "Free to join", "Meet on video before you book"].map(
              (point) => (
                <li key={point} className="flex items-center gap-2">
                  <CheckIcon className="size-4 text-brand-primary" />
                  {point}
                </li>
              ),
            )}
          </ul>
        </div>

        <HeroCollage />
      </Container>
    </section>
  );
}

/**
 * Two photographs and two cards that show the product's promises as they would look in
 * use. The cards are illustrations of the app, not testimonials: they carry no names and
 * no people, because the photographs are licensed stock and must never be presented as
 * real GoSit caregivers (see public/images/CREDITS.md).
 */
function HeroCollage() {
  return (
    <div className="relative mx-auto aspect-[4/5] w-full max-w-md lg:max-w-none">
      <div className="absolute right-0 top-0 h-[88%] w-[84%] overflow-hidden rounded-[2.5rem] shadow-lg ring-1 ring-border">
        <Image
          src="/images/hero.jpg"
          alt="A caregiver and a young child reading a book together, both laughing."
          fill
          priority
          sizes="(min-width: 1024px) 40vw, 90vw"
          className="object-cover object-[70%_center]"
        />
      </div>

      <div className="absolute bottom-0 left-0 aspect-square w-[44%] overflow-hidden rounded-[2rem] shadow-lg ring-[10px] ring-canvas">
        <Image
          src="/images/senior-care.jpg"
          alt="A caregiver holding an older woman's hands, helping her up from a chair."
          fill
          sizes="(min-width: 1024px) 18vw, 40vw"
          className="object-cover"
        />
      </div>

      <div className="drift absolute -left-2 top-[14%] flex items-center gap-3 rounded-2xl border border-border bg-surface p-3.5 pr-5 shadow-lg sm:-left-6">
        <span className="grid size-11 place-items-center rounded-xl bg-brand-soft text-brand-primary">
          <IdCardIcon className="size-6" />
        </span>
        <div>
          <p className="font-heading text-sm font-bold text-text-primary">Identity verified</p>
          <p className="text-xs text-text-secondary">Vulnerable-sector checked</p>
        </div>
      </div>

      <div className="drift-slow absolute -right-2 bottom-[8%] flex items-center gap-3 rounded-2xl border border-border bg-surface p-3.5 pr-5 shadow-lg sm:-right-4">
        <span className="grid size-11 place-items-center rounded-xl bg-accent-soft text-brand-secondary">
          <ClockIcon className="size-6" />
        </span>
        <div>
          <p className="font-heading text-sm font-bold text-text-primary">Checked in · 3:02 pm</p>
          <p className="text-xs text-text-secondary">Billed for time actually worked</p>
        </div>
      </div>
    </div>
  );
}

const STRIP = [
  "Identity verified",
  "Vulnerable-sector screened",
  "Video interviews first",
  "Check-in and check-out",
  "Private care notes",
  "Paid for time worked",
  "Non-medical care",
  "Ontario-based",
];

/** A slow, endless ribbon of the product's promises. Pauses under the pointer. */
function TrustStrip() {
  const row = (hidden: boolean) => (
    <ul aria-hidden={hidden || undefined} className="flex shrink-0 items-center gap-4 pr-4">
      {STRIP.map((item) => (
        <li
          key={item}
          className="flex items-center gap-2 whitespace-nowrap rounded-full border border-border bg-surface px-5 py-2.5 font-control text-sm font-medium text-text-secondary"
        >
          <CheckIcon className="size-4 text-brand-primary" />
          {item}
        </li>
      ))}
    </ul>
  );

  return (
    <div className="marquee overflow-hidden border-y border-border bg-canvas py-5 [mask-image:linear-gradient(to_right,transparent,black_8%,black_92%,transparent)]">
      <div className="marquee-track flex w-max">
        {row(false)}
        {row(true)}
      </div>
    </div>
  );
}

function Audiences() {
  return (
    <section className="py-24 sm:py-32">
      <Container>
        <SectionHeading
          align="center"
          eyebrow="Two sides, one marketplace"
          title="Whichever side you are on, it starts here."
        />

        <div className="mt-16 grid gap-6 lg:grid-cols-2">
          <article className="reveal group relative overflow-hidden rounded-[2rem] border border-border bg-surface shadow-md">
            <div className="relative aspect-[16/9] overflow-hidden">
              <Image
                src="/images/family-support.jpg"
                alt="Three adults of different generations cooking a meal together in a kitchen."
                fill
                sizes="(min-width: 1024px) 50vw, 100vw"
                className="object-cover transition-transform duration-700 group-hover:scale-105"
              />
            </div>
            <div className="p-8 sm:p-10">
              <Eyebrow>For families</Eyebrow>
              <h3 className="font-display mt-4 text-3xl leading-tight text-text-primary sm:text-4xl">
                Find someone you would be glad to leave at home.
              </h3>
              <p className="mt-4 text-text-secondary">
                Browse checked caregivers nearby, read their experience, message them, and meet on video
                before you decide.
              </p>
              <ul className="mt-6 space-y-2.5 text-sm font-medium text-text-primary">
                {["Free to join and browse", "Charged only once a caregiver confirms", "Your address stays private until you book"].map(
                  (point) => (
                    <li key={point} className="flex items-center gap-2.5">
                      <CheckIcon className="size-4 text-brand-primary" />
                      {point}
                    </li>
                  ),
                )}
              </ul>
              <div className="mt-8 flex flex-wrap gap-3">
                <Link href={getStartedHref("family")} className={buttonClassName("primary", "!rounded-full", "lg")}>
                  Find a caregiver
                  <ArrowRightIcon />
                </Link>
                <Link href="/for-families" className={buttonClassName("ghost", "!rounded-full", "lg")}>
                  Learn more
                </Link>
              </div>
            </div>
          </article>

          <article className="reveal deep-wash group relative overflow-hidden rounded-[2rem] shadow-md">
            <div className="relative aspect-[16/9] overflow-hidden">
              <Image
                src="/images/pet-care.jpg"
                alt="A person walking a golden retriever on a leash along a paved path."
                fill
                sizes="(min-width: 1024px) 50vw, 100vw"
                className="object-cover transition-transform duration-700 group-hover:scale-105"
              />
              <div aria-hidden className="absolute inset-0 bg-gradient-to-t from-[var(--color-brand-deep)] to-transparent" />
            </div>
            <div className="p-8 sm:p-10">
              <Eyebrow light>For caregivers</Eyebrow>
              <h3 className="font-display mt-4 text-3xl leading-tight text-text-on-deep sm:text-4xl">
                Set your rate. Choose your hours. Do the work you are good at.
              </h3>
              <p className="mt-4 text-text-on-deep/80">
                Families find you already knowing your experience and what you are certified for. You accept
                or decline every request, and you are paid for the time you work.
              </p>
              <ul className="mt-6 space-y-2.5 text-sm font-medium text-text-on-deep">
                {["You are an independent caregiver, not an employee", "See care needs before the day", "One clear statement of every fee"].map(
                  (point) => (
                    <li key={point} className="flex items-center gap-2.5">
                      <CheckIcon className="size-4 text-accent-on-deep" />
                      {point}
                    </li>
                  ),
                )}
              </ul>
              <div className="mt-8 flex flex-wrap gap-3">
                <Link href={getStartedHref("caregiver")} className={buttonClassName("secondary", "!rounded-full", "lg")}>
                  Become a caregiver
                  <ArrowRightIcon />
                </Link>
                <Link href="/for-caregivers" className={buttonClassName("light", "!rounded-full", "lg")}>
                  Learn more
                </Link>
              </div>
            </div>
          </article>
        </div>
      </Container>
    </section>
  );
}

const SERVICES = [
  {
    name: "Child care",
    blurb: "Babysitting, nannies, after-school and overnight care.",
    image: "/images/child-care.jpg",
    alt: "A caregiver and a young girl building with coloured blocks at a table.",
    className: "lg:col-span-3 lg:row-span-2 min-h-[26rem]",
  },
  {
    name: "Senior care",
    blurb: "Company, meals, errands and getting to appointments.",
    image: "/images/senior-care.jpg",
    alt: "A caregiver holding an older woman's hands, helping her up from a chair.",
    className: "lg:col-span-3 min-h-[15rem]",
  },
  {
    name: "Specialized support",
    blurb: "Caregivers experienced with additional needs.",
    image: "/images/specialised-support.jpg",
    alt: "A companion walking with someone in a wheelchair across a field at sunset.",
    className: "lg:col-span-3 min-h-[15rem]",
  },
  {
    name: "Family support",
    blurb: "The practical help that keeps a household going.",
    image: "/images/family-support.jpg",
    alt: "Three adults of different generations cooking a meal together in a kitchen.",
    className: "lg:col-span-3 min-h-[15rem]",
  },
];

function Services() {
  return (
    <section className="border-y border-border bg-surface py-24 sm:py-32">
      <Container>
        <div className="flex flex-col justify-between gap-8 lg:flex-row lg:items-end">
          <SectionHeading
            eyebrow="Care we cover"
            title="Care for the whole family, not just the youngest."
            body="GoSit started with child care. Families kept asking for help with their parents too, so that is what we built."
          />
          <Link href="/services" className={buttonClassName("ghost", "!rounded-full self-start lg:self-auto", "lg")}>
            See every service
            <ArrowRightIcon />
          </Link>
        </div>

        <ul className="mt-14 grid gap-5 lg:auto-rows-[15rem] lg:grid-cols-6">
          {SERVICES.map((service) => (
            <li
              key={service.name}
              className={`reveal group relative overflow-hidden rounded-[1.75rem] ${service.className}`}
            >
              <Image
                src={service.image}
                alt={service.alt}
                fill
                sizes="(min-width: 1024px) 33vw, 100vw"
                className="object-cover transition-transform duration-700 group-hover:scale-105"
              />
              {/* Fixed dark values: the ground is a photograph, which does not follow the theme. */}
              <div
                aria-hidden
                className="absolute inset-0 bg-gradient-to-t from-[rgba(12,20,22,0.85)] via-[rgba(12,20,22,0.2)] to-transparent"
              />
              <div className="absolute inset-x-0 bottom-0 p-6 sm:p-7">
                <h3 className="font-display text-2xl text-white sm:text-3xl">{service.name}</h3>
                <p className="mt-1.5 max-w-xs text-sm leading-relaxed text-white/85">{service.blurb}</p>
              </div>
            </li>
          ))}

          <li className="reveal group relative overflow-hidden rounded-[1.75rem] lg:col-span-3 min-h-[15rem]">
            <Image
              src="/images/pet-care.jpg"
              alt="A person walking a golden retriever on a leash along a paved path."
              fill
              sizes="(min-width: 1024px) 33vw, 100vw"
              className="object-cover transition-transform duration-700 group-hover:scale-105"
            />
            <div
              aria-hidden
              className="absolute inset-0 bg-gradient-to-t from-[rgba(12,20,22,0.85)] via-[rgba(12,20,22,0.2)] to-transparent"
            />
            <div className="absolute inset-x-0 bottom-0 p-6 sm:p-7">
              <h3 className="font-display text-2xl text-white sm:text-3xl">Pet care</h3>
              <p className="mt-1.5 max-w-xs text-sm leading-relaxed text-white/85">
                Walks, drop-in visits, in-home sitting and boarding.
              </p>
            </div>
          </li>
        </ul>
      </Container>
    </section>
  );
}

const STEPS = [
  {
    title: "Tell us who needs care",
    body: "Their age, what a good day looks like, and anything a caregiver should know. Private, and shared only with someone you book.",
    icon: <CalendarCheckIcon className="size-6" />,
  },
  {
    title: "Choose and meet someone",
    body: "Compare checked caregivers nearby by rate, experience and certifications. Message them, and meet on video before you commit.",
    icon: <VideoCameraIcon className="size-6" />,
  },
  {
    title: "Book, and pay for the time worked",
    body: "They confirm, check in when they arrive, and you are charged for the hours they were actually there.",
    icon: <ClockIcon className="size-6" />,
  },
];

function HowItWorks() {
  return (
    <section className="py-24 sm:py-32">
      <Container>
        <SectionHeading
          align="center"
          eyebrow="How it works"
          title="Three steps, and nothing taken up front."
          body="No subscription to browse, no fee to message, and no charge until your caregiver says yes."
        />

        <ol className="relative mt-20 grid gap-12 md:grid-cols-3 md:gap-8">
          <div
            aria-hidden
            className="absolute left-[16%] right-[16%] top-7 hidden border-t-2 border-dashed border-border-strong md:block"
          />
          {STEPS.map((step, index) => (
            <li key={step.title} className="reveal relative text-center">
              <span className="relative mx-auto grid size-14 place-items-center rounded-full bg-brand-primary text-text-on-brand shadow-brand ring-8 ring-canvas">
                {step.icon}
              </span>
              <p className="font-display mt-6 text-6xl text-brand-soft-border">0{index + 1}</p>
              <h3 className="mt-1 font-heading text-xl font-bold text-text-primary">{step.title}</h3>
              <p className="mx-auto mt-3 max-w-xs text-sm leading-relaxed text-text-secondary">{step.body}</p>
            </li>
          ))}
        </ol>

        <div className="mt-14 text-center">
          <Link href="/how-it-works" className={buttonClassName("ghost", "!rounded-full", "lg")}>
            The full walkthrough
            <ArrowRightIcon />
          </Link>
        </div>
      </Container>
    </section>
  );
}

const CHECKS = [
  {
    icon: <IdCardIcon className="size-6" />,
    title: "Identity, matched to the person",
    body: "Every caregiver proves who they are before they can be listed.",
  },
  {
    icon: <CheckIcon className="size-6" />,
    title: "Screened for the care they offer",
    body: "A vulnerable-sector check for care of people. Not optional, not skippable.",
  },
  {
    icon: <LockIcon className="size-6" />,
    title: "Your address and notes stay private",
    body: "Shared only with the caregiver you booked, and only once they accept.",
  },
  {
    icon: <VideoCameraIcon className="size-6" />,
    title: "Meet first, inside GoSit",
    body: "Video interviews happen on the platform and are recorded for safety.",
  },
];

function Safety() {
  return (
    <section className="px-3 sm:px-5">
      <div className="deep-wash relative mx-auto max-w-7xl overflow-hidden rounded-[2rem] sm:rounded-[2.5rem]">
        <div aria-hidden className="dot-grid pointer-events-none absolute inset-0 opacity-15" />
        <div className="relative grid gap-14 px-6 py-20 sm:px-14 sm:py-28 lg:grid-cols-[0.9fr_1.1fr] lg:items-center">
          <div className="reveal">
            <Eyebrow light>Trust and safety</Eyebrow>
            <h2 className="font-display mt-5 text-4xl leading-[1.06] text-text-on-deep sm:text-6xl">
              What stands behind the person at your door.
            </h2>
            <p className="mt-6 max-w-lg text-lg leading-relaxed text-text-on-deep/80">
              You are letting someone into your home to look after a person you love. Here is exactly what we
              check, and where our job stops.
            </p>
            <Link href="/safety" className={buttonClassName("light", "!rounded-full mt-9", "xl")}>
              How we keep it safe
              <ArrowRightIcon />
            </Link>
          </div>

          <ul className="grid gap-4 sm:grid-cols-2">
            {CHECKS.map((check) => (
              <li
                key={check.title}
                className="reveal rounded-3xl border border-white/15 bg-white/[0.07] p-6 backdrop-blur-sm"
              >
                <span className="grid size-12 place-items-center rounded-2xl bg-white/15 text-accent-on-deep">
                  {check.icon}
                </span>
                <h3 className="mt-5 font-heading text-lg font-bold text-text-on-deep">{check.title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-text-on-deep/75">{check.body}</p>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}

function AppSection() {
  return (
    <section className="py-24 sm:py-32">
      <Container className="grid items-center gap-16 lg:grid-cols-2">
        <div className="reveal">
          <Eyebrow>The GoSit app</Eyebrow>
          <h2 className="font-display mt-4 text-4xl leading-[1.08] text-text-primary sm:text-5xl">
            Your day and your bookings, on your phone.
          </h2>
          <p className="mt-5 max-w-lg text-lg leading-relaxed text-text-secondary">
            Check in when you arrive, see what is coming up, and follow every update in one inbox. Families
            and caregivers see the same booking, so nobody is guessing.
          </p>
          <ul className="mt-8 space-y-3 text-text-primary">
            {["One tap to check in and out", "Every message, in one conversation", "Clear earnings and payouts for caregivers"].map(
              (point) => (
                <li key={point} className="flex items-center gap-3 font-medium">
                  <span className="grid size-6 place-items-center rounded-full bg-brand-soft text-brand-primary">
                    <CheckIcon className="size-3.5" />
                  </span>
                  {point}
                </li>
              ),
            )}
          </ul>
        </div>

        <div className="reveal">
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
      </Container>
    </section>
  );
}

function FaqSection() {
  return (
    <section className="border-t border-border bg-surface py-24 sm:py-32">
      <Container className="grid gap-14 lg:grid-cols-[0.8fr_1.2fr]">
        <div>
          <SectionHeading
            eyebrow="Questions"
            title="Good questions, plainly answered."
            body="If you do not see yours, ask us. A real person replies."
          />
          <Link href="/faq" className={buttonClassName("ghost", "!rounded-full mt-8", "lg")}>
            Read every answer
            <ArrowRightIcon />
          </Link>
        </div>
        <Faq items={FAMILY_FAQ.slice(0, 5)} />
      </Container>
    </section>
  );
}
