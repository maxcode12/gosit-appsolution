"use client";

import { useRouter } from "next/navigation";
import { Button, buttonClassName } from "@/components/ui/button";
import { CONTACT_ANCHOR, MARKETING_ONLY } from "@/features/marketing/public-mode";

/**
 * "Where do you need care?" — the one thing a family can do on the front page.
 *
 * It goes to sign-up rather than straight to results, because caregiver search is
 * authenticated: profiles carry a neighbourhood, a rate and certifications, and none of
 * that is public. Rather than drop what they typed at the door, the postal code travels
 * with them and seeds the search they land on.
 *
 * Nothing is validated away here. A postal code that does not parse still gets someone
 * to sign-up — being told off by a landing page for mistyping is a poor first meeting,
 * and the server checks it properly before it reaches a redirect.
 */
export function HeroSearch() {
  const router = useRouter();

  // Search leads to sign-up, which is not open on the public brochure build.
  if (MARKETING_ONLY) {
    return (
      <div className="mt-8 flex flex-col gap-3 rounded-card bg-surface/95 p-4 shadow-lg backdrop-blur-sm sm:flex-row sm:items-center sm:justify-between sm:p-5">
        <p className="font-control text-sm font-medium text-text-primary">
          Families, caregivers and partners: tell us what you need and we will be in
          touch.
        </p>
        <a
          href={CONTACT_ANCHOR}
          className={buttonClassName("primary", "h-12 sm:w-auto", "lg")}
        >
          Contact us
        </a>
      </div>
    );
  }

  function search(formData: FormData) {
    const postalCode = String(formData.get("postalCode") ?? "").trim().toUpperCase();

    router.push(
      postalCode ? `/signup?postalCode=${encodeURIComponent(postalCode)}` : "/signup",
    );
  }

  return (
    <form
      action={search}
      className="mt-8 flex flex-col gap-3 rounded-card bg-surface/95 p-4 shadow-lg backdrop-blur-sm sm:flex-row sm:items-end sm:p-5"
    >
      <div className="flex-1">
        <label
          htmlFor="hero-postal-code"
          className="font-control text-sm font-medium text-text-primary"
        >
          Where do you need care?
        </label>
        <input
          id="hero-postal-code"
          name="postalCode"
          autoComplete="postal-code"
          placeholder="M5S 1M2"
          maxLength={7}
          className="mt-1.5 h-12 w-full rounded-control border border-border bg-surface px-4 font-control text-base uppercase text-text-primary placeholder:normal-case placeholder:text-text-muted focus:border-brand-primary focus:outline-none focus:ring-2 focus:ring-brand-primary/30"
        />
      </div>

      <Button type="submit" size="lg" className="h-12 sm:w-auto">
        Find care
      </Button>
    </form>
  );
}
