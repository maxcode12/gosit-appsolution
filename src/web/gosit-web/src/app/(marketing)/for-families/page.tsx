import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/button";
import {
  ArrowRightIcon,
  CheckIcon,
  ChatIcon,
  HomeIcon,
  LockIcon,
  VideoCameraIcon,
  WalletCardIcon,
} from "@/components/ui/marketing-icons";
import { Faq } from "@/features/marketing/components/faq";
import { FAMILY_FAQ } from "@/features/marketing/components/faq-data";
import { FinalCta } from "@/features/marketing/components/final-cta";
import { Container, PageHero, SectionHeading } from "@/features/marketing/components/section";
import { getStartedHref } from "@/features/marketing/public-mode";

export const metadata: Metadata = {
  title: "For families",
  description:
    "Find checked, independent caregivers for your children, parents or pets. Free to join, meet on video first, and pay only for the time worked.",
};

const BENEFITS = [
  {
    icon: <ChatIcon className="size-6" />,
    title: "Talk before you decide",
    body: "Message any caregiver for free, ask your questions, and see how they answer.",
  },
  {
    icon: <VideoCameraIcon className="size-6" />,
    title: "Meet on video first",
    body: "Ask for an interview inside GoSit. It is recorded for safety, and you are told before you join.",
  },
  {
    icon: <HomeIcon className="size-6" />,
    title: "Care at your home",
    body: "Care for people happens where your family is comfortable. Only pet boarding happens at a caregiver's home.",
  },
  {
    icon: <LockIcon className="size-6" />,
    title: "Your details stay yours",
    body: "Care needs and your exact address are shared only after a caregiver accepts, never in public search.",
  },
  {
    icon: <WalletCardIcon className="size-6" />,
    title: "Pay for time actually worked",
    body: "Your caregiver checks in and out, and the final bill follows the clock, not the estimate.",
  },
  {
    icon: <CheckIcon className="size-6" />,
    title: "Matched to the care you need",
    body: "Dementia support, autism support, First Aid: shown against what a caregiver is actually certified for.",
  },
];

const MOMENTS = [
  { title: "After school, most days", body: "Someone reliable to collect, feed and supervise while you are still at work.", image: "/images/child-care.jpg", alt: "A caregiver and a young girl building with coloured blocks at a table." },
  { title: "Mum's Tuesday morning", body: "Company, a proper breakfast and a lift to the appointment, without it all landing on you.", image: "/images/senior-care.jpg", alt: "A caregiver holding an older woman's hands, helping her up from a chair." },
  { title: "A break that is earned", body: "Respite from someone experienced with additional needs, so you can rest knowing it is handled.", image: "/images/specialised-support.jpg", alt: "A companion walking with someone in a wheelchair across a field at sunset." },
];

export default function ForFamiliesPage() {
  return (
    <>
      <PageHero
        eyebrow="For families"
        title={
          <>
            Help you can <em className="text-brand-primary">rely on</em>, found in minutes.
          </>
        }
        body="Search checked caregivers near you, message them, meet on video, and book. You are not charged until a caregiver says yes."
      >
        <Link href={getStartedHref("family")} className={buttonClassName("primary", "!rounded-full", "xl")}>
          Find a caregiver
          <ArrowRightIcon />
        </Link>
        <Link href="/how-it-works" className={buttonClassName("ghost", "!rounded-full", "xl")}>
          How it works
        </Link>
      </PageHero>

      <section className="py-24 sm:py-32">
        <Container>
          <SectionHeading
            eyebrow="Why families choose GoSit"
            title="Everything you would want to know, before anyone arrives."
          />
          <ul className="mt-14 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {BENEFITS.map((benefit) => (
              <li key={benefit.title} className="reveal surface-card-interactive p-7">
                <span className="grid size-12 place-items-center rounded-2xl bg-brand-soft text-brand-primary">
                  {benefit.icon}
                </span>
                <h3 className="mt-5 font-heading text-lg font-bold text-text-primary">{benefit.title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-text-secondary">{benefit.body}</p>
              </li>
            ))}
          </ul>
        </Container>
      </section>

      <section className="border-y border-border bg-surface py-24 sm:py-32">
        <Container>
          <SectionHeading
            eyebrow="Real life"
            title="The moments GoSit is for."
            body="Not a single stage, and not a single kind of help."
          />
          <ul className="mt-14 grid gap-6 md:grid-cols-3">
            {MOMENTS.map((moment) => (
              <li key={moment.title} className="reveal group overflow-hidden rounded-[1.75rem] border border-border bg-canvas shadow-sm">
                <div className="relative aspect-[4/3] overflow-hidden">
                  <Image
                    src={moment.image}
                    alt={moment.alt}
                    fill
                    sizes="(min-width: 768px) 33vw, 100vw"
                    className="object-cover transition-transform duration-700 group-hover:scale-105"
                  />
                </div>
                <div className="p-7">
                  <h3 className="font-display text-2xl text-text-primary">{moment.title}</h3>
                  <p className="mt-3 text-sm leading-relaxed text-text-secondary">{moment.body}</p>
                </div>
              </li>
            ))}
          </ul>
        </Container>
      </section>

      <section className="py-24 sm:py-32">
        <Container className="grid gap-14 lg:grid-cols-[0.8fr_1.2fr]">
          <SectionHeading
            eyebrow="Questions families ask"
            title="Before you sign up."
            body="Straight answers, including the limits of what GoSit does."
          />
          <Faq items={FAMILY_FAQ} />
        </Container>
      </section>

      <FinalCta title="Find your caregiver." body="Free to join. No charge until a caregiver confirms your booking." />
    </>
  );
}
