import type { Metadata } from "next";
import { CheckIcon, ClockIcon, IdCardIcon, LockIcon, SparkIcon, VideoCameraIcon } from "@/components/ui/marketing-icons";
import { FinalCta } from "@/features/marketing/components/final-cta";
import { Container, Eyebrow, PageHero, SectionHeading } from "@/features/marketing/components/section";

export const metadata: Metadata = {
  title: "Trust & safety",
  description:
    "Exactly what GoSit checks before a caregiver is listed, how your family's information is protected, and where our responsibility stops.",
};

/**
 * Concrete enough to be verifiable. A vague "we take safety seriously" reassures nobody
 * who is actually worried, and an overclaim here is worse than silence: these are the
 * statements a family relies on, so each one describes what the product enforces.
 */
const CHECKS = [
  {
    icon: <IdCardIcon className="size-6" />,
    title: "Identity verification",
    body: "Every caregiver proves who they are with government ID before they can be listed.",
  },
  {
    icon: <CheckIcon className="size-6" />,
    title: "Screening that fits the care",
    body: "For care of people, a vulnerable-sector check. For pet care only, a criminal record check. A caregiver cleared only for pets can never be matched to care for people.",
  },
  {
    icon: <SparkIcon className="size-6" />,
    title: "Certifications reviewed",
    body: "First Aid, CPR and specialist training are reviewed by our team before they appear on a profile, and expiry is tracked.",
  },
  {
    icon: <VideoCameraIcon className="size-6" />,
    title: "Interviews on the platform",
    body: "Video interviews happen inside GoSit and are recorded. Only trust and safety staff can view a recording, and every view is logged.",
  },
  {
    icon: <ClockIcon className="size-6" />,
    title: "A record of every booking",
    body: "Check-in and check-out times, messages and payments are kept, so there is always an account of what happened.",
  },
  {
    icon: <LockIcon className="size-6" />,
    title: "Private by default",
    body: "Care needs, notes and your exact address are only shown to a caregiver after they accept your booking, never in public search.",
  },
];

const BOUNDARIES = [
  {
    title: "Caregivers are independent.",
    body: "They are not GoSit employees, and GoSit does not direct how care is provided. Families and caregivers arrange the care between themselves.",
  },
  {
    title: "Screening reduces risk. It cannot remove it.",
    body: "No check can predict what a person will do. We will never describe a caregiver as guaranteed safe, and you should use your own judgement too.",
  },
  {
    title: "It is non-medical care.",
    body: "Caregivers do not administer medication, perform nursing procedures or provide therapy or diagnosis.",
  },
  {
    title: "In an emergency, call 911.",
    body: "Then tell us. You can report an incident from your booking and our team will review it.",
  },
];

export default function SafetyPage() {
  return (
    <>
      <PageHero
        eyebrow="Trust and safety"
        image={{
          src: "/images/specialised-support.jpg",
          alt: "A companion walking with someone in a wheelchair across a field at sunset.",
          badge: { icon: <IdCardIcon className="size-6" />, title: "Identity verified", body: "Before anyone is listed" },
        }}
        title={
          <>
            What we check, and <em className="text-brand-primary">where we stop</em>.
          </>
        }
        body="You are letting someone into your home to look after a person you love. Here is what stands behind that, stated plainly."
      />

      <section className="py-16 sm:py-24">
        <Container>
          <SectionHeading eyebrow="Before anyone is listed" title="Six things that are true of every caregiver and every booking." />
          <ul className="mt-14 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {CHECKS.map((check) => (
              <li key={check.title} className="reveal surface-card-interactive p-7">
                <span className="grid size-12 place-items-center rounded-2xl bg-brand-soft text-brand-primary">
                  {check.icon}
                </span>
                <h3 className="mt-5 font-heading text-lg font-bold text-text-primary">{check.title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-text-secondary">{check.body}</p>
              </li>
            ))}
          </ul>
        </Container>
      </section>

      <section className="px-3 pb-16 sm:px-5 sm:pb-24">
        <div className="deep-wash relative mx-auto max-w-7xl overflow-hidden rounded-[2rem] sm:rounded-[2.5rem]">
          <div aria-hidden className="dot-grid pointer-events-none absolute inset-0 opacity-15" />
          <div className="relative grid gap-12 px-6 py-14 sm:px-14 sm:py-20 lg:grid-cols-[0.8fr_1.2fr]">
            <div>
              <Eyebrow light>The honest part</Eyebrow>
              <h2 className="font-display mt-4 text-4xl leading-[1.08] text-text-on-deep sm:text-5xl">
                What GoSit is, and is not.
              </h2>
              <p className="mt-5 text-lg leading-relaxed text-text-on-deep/80">
                A marketplace earns trust by being clear about its limits.
              </p>
            </div>
            <dl className="grid gap-4 sm:grid-cols-2">
              {BOUNDARIES.map((item) => (
                <div key={item.title} className="rounded-3xl border border-white/15 bg-white/[0.07] p-6">
                  <dt className="font-heading text-base font-bold text-text-on-deep">{item.title}</dt>
                  <dd className="mt-2 text-sm leading-relaxed text-text-on-deep/75">{item.body}</dd>
                </div>
              ))}
            </dl>
          </div>
        </div>
      </section>

      <FinalCta title="Questions about safety?" body="Ask us anything. We would rather you asked before you booked." />
    </>
  );
}
