import type { Metadata } from "next";
import { Suspense } from "react";
import { notFound } from "next/navigation";
import { CheckIcon } from "@/components/ui/marketing-icons";
import { EarlyAccessForm } from "@/features/marketing/components/early-access-form";
import { Container, Eyebrow } from "@/features/marketing/components/section";
import { MARKETING_ONLY } from "@/features/marketing/public-mode";

export const metadata: Metadata = {
  title: "Get started",
  description: "Join the GoSit early list. Families and caregivers: tell us who you are and we will be in touch when accounts open.",
};

/**
 * Where every "Get started" button goes while accounts are not open to the public.
 *
 * The full app has a real sign-up form, so this page only exists on the brochure build.
 * Once registration is open, the buttons point at /signup and this page disappears.
 */
export default function GetStartedPage() {
  if (!MARKETING_ONLY) {
    notFound();
  }

  return (
    <section className="hero-wash relative overflow-hidden">
      <div aria-hidden className="dot-grid pointer-events-none absolute inset-0 opacity-60" />
      <Container className="relative grid gap-14 py-20 sm:py-28 lg:grid-cols-[0.9fr_1.1fr]">
        <div>
          <Eyebrow>Early access</Eyebrow>
          <h1 className="font-display mt-5 text-5xl leading-[1.04] text-text-primary sm:text-6xl">
            Be first through <em className="text-brand-primary">the door</em>.
          </h1>
          <p className="mt-7 max-w-md text-xl leading-relaxed text-text-secondary">
            GoSit is opening carefully, one step at a time. Tell us who you are and we will contact you the
            moment accounts open.
          </p>
          <ul className="mt-10 space-y-4 text-text-secondary">
            {[
              "Families: be first to find a caregiver",
              "Caregivers: get a head start on your checks",
              "No spam, and no mailing list",
            ].map((point) => (
              <li key={point} className="flex items-center gap-3">
                <span className="grid size-6 place-items-center rounded-full bg-brand-soft text-brand-primary">
                  <CheckIcon className="size-3.5" />
                </span>
                {point}
              </li>
            ))}
          </ul>
        </div>

        <div className="rounded-[2rem] border border-border bg-surface p-6 shadow-lg sm:p-10">
          <Suspense>
            <EarlyAccessForm />
          </Suspense>
        </div>
      </Container>
    </section>
  );
}
