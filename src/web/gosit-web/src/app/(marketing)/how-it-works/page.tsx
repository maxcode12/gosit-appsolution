import type { Metadata } from "next";
import { CalendarCheckIcon, ChatIcon, ClockIcon, IdCardIcon, UsersIcon, VideoCameraIcon, WalletCardIcon } from "@/components/ui/marketing-icons";
import { FinalCta } from "@/features/marketing/components/final-cta";
import { Container, Eyebrow, PageHero } from "@/features/marketing/components/section";

export const metadata: Metadata = {
  title: "How it works",
  description:
    "How GoSit works for families and for caregivers: from creating a profile to being paid for the time worked.",
};

const FAMILY_STEPS = [
  { icon: <UsersIcon className="size-6" />, title: "Create your account", body: "Tell us who needs care and what a good day looks like. Care needs are private and shared only with a caregiver you book." },
  { icon: <IdCardIcon className="size-6" />, title: "Browse checked caregivers", body: "Every caregiver in search has passed the checks for the care they offer. Compare rates, experience and certifications." },
  { icon: <ChatIcon className="size-6" />, title: "Message and meet", body: "Ask questions in the app, and request a video interview. It happens inside GoSit and is recorded for safety." },
  { icon: <CalendarCheckIcon className="size-6" />, title: "Book", body: "Send a request. Nothing is charged until the caregiver accepts, and you see the full cost first." },
  { icon: <ClockIcon className="size-6" />, title: "Care happens, and is recorded", body: "Your caregiver checks in when they arrive and out when they leave, so there is a clear record of the time." },
  { icon: <WalletCardIcon className="size-6" />, title: "Pay, and say how it went", body: "You are charged for the time actually worked, and you can leave a review for other families." },
];

const CAREGIVER_STEPS = [
  { title: "Create your profile", body: "Your experience, services, rates and the area you work in." },
  { title: "Sign the agreement", body: "The Caregiver Service Agreement sets out how the marketplace works, including that you are independent." },
  { title: "Complete your checks", body: "Identity verification, plus a vulnerable-sector check if you will care for people." },
  { title: "Add certifications", body: "Optional, but families look for First Aid, CPR and specialist training." },
  { title: "Accept the work you want", body: "Requests come to you. Accept or decline each one, and check in and out from your phone." },
  { title: "Get paid", body: "Connect your payout account once. You are paid for the time you worked, with every fee itemised." },
];

export default function HowItWorksPage() {
  return (
    <>
      <PageHero
        eyebrow="How it works"
        image={{
          src: "/images/hero.jpg",
          alt: "A caregiver and a young child reading a book together, both laughing.",
          position: "object-[70%_center]",
          badge: { icon: <ClockIcon className="size-6" />, title: "Checked in", body: "Billed for time actually worked" },
        }}
        title={
          <>
            Simple for families. <em className="text-brand-primary">Fair</em> for caregivers.
          </>
        }
        body="From first message to final payment, here is what happens, and who is responsible for what."
      />

      <section className="py-16 sm:py-24">
        <Container>
          <Eyebrow>For families</Eyebrow>
          <h2 className="font-display mt-4 max-w-2xl text-4xl text-text-primary sm:text-5xl">
            Six steps from search to settled.
          </h2>
          <ol className="mt-14 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {FAMILY_STEPS.map((step, index) => (
              <li key={step.title} className="reveal surface-card relative p-7">
                <span className="font-display absolute right-6 top-4 text-6xl text-brand-soft-border">{index + 1}</span>
                <span className="grid size-12 place-items-center rounded-2xl bg-brand-soft text-brand-primary">
                  {step.icon}
                </span>
                <h3 className="mt-5 font-heading text-lg font-bold text-text-primary">{step.title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-text-secondary">{step.body}</p>
              </li>
            ))}
          </ol>
        </Container>
      </section>

      <section className="border-y border-border bg-surface py-16 sm:py-24">
        <Container className="grid gap-14 lg:grid-cols-[0.8fr_1.2fr]">
          <div>
            <Eyebrow>For caregivers</Eyebrow>
            <h2 className="font-display mt-4 text-4xl text-text-primary sm:text-5xl">Your side of the booking.</h2>
            <p className="mt-5 text-lg leading-relaxed text-text-secondary">
              GoSit is a marketplace, not an employer. You decide what you charge, when you work and which
              requests to accept.
            </p>
          </div>
          <ol className="relative flex flex-col gap-6 border-l-2 border-dashed border-border-strong pl-8">
            {CAREGIVER_STEPS.map((step, index) => (
              <li key={step.title} className="reveal relative">
                <span className="absolute -left-[2.9rem] grid size-9 place-items-center rounded-full bg-brand-secondary font-heading text-sm font-bold text-white ring-8 ring-surface">
                  {index + 1}
                </span>
                <h3 className="font-heading text-lg font-bold text-text-primary">{step.title}</h3>
                <p className="mt-1 text-sm leading-relaxed text-text-secondary">{step.body}</p>
              </li>
            ))}
          </ol>
        </Container>
      </section>

      <section className="py-16 sm:py-24">
        <Container className="grid items-center gap-12 lg:grid-cols-2">
          <div className="reveal">
            <Eyebrow>Meeting first</Eyebrow>
            <h2 className="font-display mt-4 text-4xl text-text-primary sm:text-5xl">
              Video interviews, kept on the platform.
            </h2>
            <p className="mt-5 text-lg leading-relaxed text-text-secondary">
              Families can ask a caregiver for a video interview, and the caregiver accepts or declines. Every
              interview is recorded, and you agree to that before you join. A call that cannot be recorded
              does not go ahead.
            </p>
          </div>
          <ul className="reveal grid gap-4">
            {[
              "Recordings are deleted after 90 days unless our safety team needs to keep one.",
              "Recordings are visible only to trust and safety staff, and every view is logged.",
              "Please do not arrange calls, links or meetings on other services. Doing so takes you outside GoSit's protections.",
            ].map((point) => (
              <li key={point} className="flex gap-4 rounded-3xl border border-border bg-surface p-6 shadow-sm">
                <span className="grid size-11 shrink-0 place-items-center rounded-xl bg-brand-soft text-brand-primary">
                  <VideoCameraIcon className="size-5" />
                </span>
                <p className="text-sm leading-relaxed text-text-secondary">{point}</p>
              </li>
            ))}
          </ul>
        </Container>
      </section>

      <FinalCta />
    </>
  );
}
