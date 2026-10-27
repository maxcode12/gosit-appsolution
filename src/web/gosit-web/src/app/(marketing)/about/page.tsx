import type { Metadata } from "next";
import Image from "next/image";
import { FinalCta } from "@/features/marketing/components/final-cta";
import { Container, Eyebrow, PageHero, SectionHeading } from "@/features/marketing/components/section";

export const metadata: Metadata = {
  title: "About",
  description:
    "GoSit is a trusted care marketplace for children, seniors, adults and pets, built in Ontario by 11419056 Canada Inc.",
};

const VALUES = [
  {
    title: "Trust is the product.",
    body: "We are not selling bookings, we are selling confidence that the right person is at the door. So we show our working: what is checked, what is private, and where our responsibility stops.",
  },
  {
    title: "Care is for the whole family.",
    body: "Families are not one age. A product built only around babysitting fails the person looking after their parents too, and the dog.",
  },
  {
    title: "Fair to those who do the work.",
    body: "Caregivers set their own rates and hours, and are paid for the time they actually worked. A marketplace that squeezes its caregivers will not keep good ones.",
  },
  {
    title: "Honest about what we are.",
    body: "GoSit connects families with independent caregivers. We do not employ them, supervise them, or provide medical care, and we will not pretend otherwise.",
  },
];

export default function AboutPage() {
  return (
    <>
      <PageHero
        eyebrow="About GoSit"
        title={
          <>
            Built for the people who <em className="text-brand-primary">do the caring</em>.
          </>
        }
        body="GoSit started with a simple frustration: finding someone you can trust to look after your family should not depend on who you happen to know."
      />

      <section className="py-24 sm:py-32">
        <Container className="grid items-center gap-16 lg:grid-cols-2">
          <div className="reveal">
            <Eyebrow>Our story</Eyebrow>
            <h2 className="font-display mt-4 text-4xl leading-[1.08] text-text-primary sm:text-5xl">
              A marketplace for every stage of family life.
            </h2>
            <div className="mt-6 space-y-5 text-lg leading-relaxed text-text-secondary">
              <p>
                Most care platforms were built around one stage: a babysitter for the kids. But the same
                families are soon looking for help with an ageing parent, a relative with additional needs, or
                someone to walk the dog while everyone is out.
              </p>
              <p>
                GoSit brings those together in one place, with checks that fit the kind of care, tools for
                meeting caregivers properly before you book, and a clear record of every booking.
              </p>
            </div>
          </div>
          <div className="reveal relative aspect-[5/4] overflow-hidden rounded-[2rem] shadow-lg ring-1 ring-border">
            <Image
              src="/images/family-support.jpg"
              alt="Three adults of different generations cooking a meal together in a kitchen."
              fill
              sizes="(min-width: 1024px) 50vw, 100vw"
              className="object-cover"
            />
          </div>
        </Container>
      </section>

      <section className="border-y border-border bg-surface py-24 sm:py-32">
        <Container>
          <SectionHeading eyebrow="What we believe" title="Four things we hold to." />
          <ul className="mt-14 grid gap-5 md:grid-cols-2">
            {VALUES.map((value, index) => (
              <li key={value.title} className="reveal rounded-3xl border border-border bg-canvas p-8">
                <span className="font-display text-5xl leading-none text-brand-soft-border">0{index + 1}</span>
                <h3 className="mt-4 font-heading text-xl font-bold text-text-primary">{value.title}</h3>
                <p className="mt-3 text-sm leading-relaxed text-text-secondary">{value.body}</p>
              </li>
            ))}
          </ul>
        </Container>
      </section>

      <section className="py-20">
        <Container>
          <p className="mx-auto max-w-3xl text-center text-sm leading-relaxed text-text-muted">
            GoSit is operated by 11419056 Canada Inc., a corporation incorporated under the Canada Business
            Corporations Act and registered in Ontario. &ldquo;GoSit&rdquo; is a trade name of that company.
          </p>
        </Container>
      </section>

      <FinalCta />
    </>
  );
}
