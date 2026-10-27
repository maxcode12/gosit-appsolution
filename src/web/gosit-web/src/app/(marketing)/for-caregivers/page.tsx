import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/button";
import {
  ArrowRightIcon,
  CalendarCheckIcon,
  ClockIcon,
  IdCardIcon,
  UsersIcon,
  WalletCardIcon,
} from "@/components/ui/marketing-icons";
import { Faq } from "@/features/marketing/components/faq";
import { CAREGIVER_FAQ } from "@/features/marketing/components/faq-data";
import { FinalCta } from "@/features/marketing/components/final-cta";
import { NearbySearch } from "@/features/marketing/components/nearby-search";
import { Container, PageHero, SectionHeading } from "@/features/marketing/components/section";
import { getStartedHref } from "@/features/marketing/public-mode";

export const metadata: Metadata = {
  title: "For caregivers",
  description:
    "Work as an independent caregiver on GoSit. Set your own rate, choose your hours, accept the requests you want, and be paid for the time you work.",
};

const PERKS = [
  {
    icon: <WalletCardIcon className="size-6" />,
    title: "You set your rate",
    body: "Decide what you charge for each service you offer. Families see it before they message you.",
  },
  {
    icon: <CalendarCheckIcon className="size-6" />,
    title: "You choose your hours",
    body: "Publish when you are free. Accept or decline every request. Nobody schedules you.",
  },
  {
    icon: <UsersIcon className="size-6" />,
    title: "Families come to you",
    body: "Your experience and certifications are on your profile, so the people who message you already know what you offer.",
  },
  {
    icon: <ClockIcon className="size-6" />,
    title: "Paid for the time you work",
    body: "Check in and out from your phone. Your payout follows the clock, with every fee shown in one clear statement.",
  },
];

const STEPS = [
  { title: "Create your profile", body: "Tell families who you are, what you offer and where you work. It takes a few minutes." },
  { title: "Sign the agreement and get checked", body: "Accept the Caregiver Service Agreement and complete the checks for the care you offer: identity, plus a vulnerable-sector check for care of people." },
  { title: "Add what makes you stand out", body: "Upload certifications such as First Aid and CPR. Our team reviews them before they show on your profile." },
  { title: "Start accepting bookings", body: "Once you are listed, families can find you, message you and ask for a video interview." },
];

export default function ForCaregiversPage() {
  return (
    <>
      <PageHero
        eyebrow="For caregivers"
        image={{
          src: "/images/toddler-play.jpg",
          alt: "A caregiver sitting on the floor showing colourful blocks to young children.",
          badge: { icon: <WalletCardIcon className="size-6" />, title: "Paid for time worked", body: "Every fee in one statement" },
        }}
        title={
          <>
            Work you are good at, <em className="text-brand-primary">on your terms</em>.
          </>
        }
        body="Join GoSit as an independent caregiver. Set your rate, choose your hours, and meet families who already know what you offer."
      >
        <Link href={getStartedHref("caregiver")} className={buttonClassName("secondary", "!rounded-full", "xl")}>
          Become a caregiver
          <ArrowRightIcon />
        </Link>
        <Link href="#how-to-join" className={buttonClassName("ghost", "!rounded-full", "xl")}>
          How joining works
        </Link>
      </PageHero>

      <section className="pt-14 sm:pt-20">
        <Container className="grid items-center gap-10 lg:grid-cols-[0.8fr_1.2fr]">
          <SectionHeading
            eyebrow="Your area"
            title="See who is already working near you."
            body="Enter a postal code or city to see how many caregivers are on GoSit around you."
          />
          <NearbySearch audience="caregiver" />
        </Container>
      </section>

      <section className="py-16 sm:py-24">
        <Container className="grid items-center gap-16 lg:grid-cols-2">
          <div className="reveal relative aspect-[5/4] overflow-hidden rounded-[2rem] shadow-lg ring-1 ring-border">
            <Image
              src="/images/pet-care.jpg"
              alt="A person walking a golden retriever on a leash along a paved path."
              fill
              sizes="(min-width: 1024px) 50vw, 100vw"
              className="object-cover"
            />
          </div>
          <div>
            <SectionHeading
              eyebrow="Why GoSit"
              title="Independent means independent."
              body="You are not an employee. You run your own caregiving work, and GoSit provides the marketplace, scheduling, messaging and payments around it."
            />
            <ul className="mt-10 grid gap-4 sm:grid-cols-2">
              {PERKS.map((perk) => (
                <li key={perk.title} className="reveal rounded-3xl border border-border bg-surface p-6 shadow-sm">
                  <span className="grid size-11 place-items-center rounded-xl bg-accent-soft text-brand-secondary">
                    {perk.icon}
                  </span>
                  <h3 className="mt-4 font-heading text-base font-bold text-text-primary">{perk.title}</h3>
                  <p className="mt-1.5 text-sm leading-relaxed text-text-secondary">{perk.body}</p>
                </li>
              ))}
            </ul>
          </div>
        </Container>
      </section>

      <section id="how-to-join" className="scroll-mt-24 border-y border-border bg-surface py-16 sm:py-24">
        <Container>
          <SectionHeading
            align="center"
            eyebrow="Getting started"
            title="From sign-up to your first booking."
            body="Checks exist to protect the families you will work with, and to show them you can be trusted."
          />
          <ol className="mx-auto mt-16 grid max-w-4xl gap-5 md:grid-cols-2">
            {STEPS.map((step, index) => (
              <li key={step.title} className="reveal flex gap-5 rounded-3xl border border-border bg-canvas p-7">
                <span className="font-display text-5xl leading-none text-brand-soft-border">0{index + 1}</span>
                <div>
                  <h3 className="font-heading text-lg font-bold text-text-primary">{step.title}</h3>
                  <p className="mt-2 text-sm leading-relaxed text-text-secondary">{step.body}</p>
                </div>
              </li>
            ))}
          </ol>

          <div className="mx-auto mt-10 flex max-w-4xl items-start gap-4 rounded-3xl border border-brand-soft-border bg-brand-soft p-6">
            <span className="mt-0.5 grid size-10 shrink-0 place-items-center rounded-xl bg-brand-primary text-text-on-brand">
              <IdCardIcon className="size-5" />
            </span>
            <p className="text-sm leading-relaxed text-text-secondary">
              <strong className="font-semibold text-text-primary">What you will need:</strong> government photo ID, and
              consent to a background check for the care you offer. Caregivers are responsible for their own
              insurance. We will tell you what is expected for the services you choose.
            </p>
          </div>
        </Container>
      </section>

      <section className="py-16 sm:py-24">
        <Container className="grid gap-14 lg:grid-cols-[0.8fr_1.2fr]">
          <SectionHeading
            eyebrow="Questions caregivers ask"
            title="The honest answers."
            body="Including what independent means, and what you are responsible for."
          />
          <Faq items={CAREGIVER_FAQ} />
        </Container>
      </section>

      <FinalCta
        title="Start caring on your terms."
        body="Create your profile today. We will walk you through each check."
      />
    </>
  );
}
