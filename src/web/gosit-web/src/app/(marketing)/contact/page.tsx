import type { Metadata } from "next";
import { CheckIcon } from "@/components/ui/marketing-icons";
import { ContactForm } from "@/features/marketing/components/contact-form";
import { Container, Eyebrow } from "@/features/marketing/components/section";

export const metadata: Metadata = {
  title: "Contact",
  description: "Families, caregivers and partners: send GoSit a message and we will reply by email.",
};

export default function ContactPage() {
  return (
    <section className="hero-wash relative overflow-hidden">
      <div aria-hidden className="dot-grid pointer-events-none absolute inset-0 opacity-60" />
      <Container className="relative grid gap-14 py-20 sm:py-28 lg:grid-cols-[0.9fr_1.1fr]">
        <div>
          <Eyebrow>Contact</Eyebrow>
          <h1 className="font-display mt-5 text-5xl leading-[1.04] text-text-primary sm:text-6xl">
            Tell us what you <em className="text-brand-primary">need</em>.
          </h1>
          <p className="mt-7 max-w-md text-xl leading-relaxed text-text-secondary">
            Families, caregivers and partners: send us a message and we will reply by email.
          </p>
          <ul className="mt-10 space-y-4 text-text-secondary">
            {[
              "A real person reads every message",
              "We reply to the email address you give us",
              "We do not add anyone to a mailing list",
            ].map((point) => (
              <li key={point} className="flex items-center gap-3">
                <span className="grid size-6 place-items-center rounded-full bg-brand-soft text-brand-primary">
                  <CheckIcon className="size-3.5" />
                </span>
                {point}
              </li>
            ))}
          </ul>
          <p className="mt-10 max-w-md text-sm leading-relaxed text-text-muted">
            In an emergency, call 911. This form is not monitored around the clock.
          </p>
        </div>

        <div className="rounded-[2rem] border border-border bg-surface p-6 shadow-lg sm:p-10">
          <ContactForm />
        </div>
      </Container>
    </section>
  );
}
