import Link from "next/link";
import type { ReactNode } from "react";
import { Wordmark } from "@/components/ui/wordmark";

interface AuthCardProps {
  title: string;
  subtitle?: string;
  children: ReactNode;
  /** Where the wordmark returns to. */
  closeHref?: string;
}

/**
 * Shared shell for the Log in and Sign up screens.
 *
 * The wireframes (49:8, 77:12) frame these as modal cards with decorative crossing
 * bars over the corner. The card is kept; the bars are not. On a screen whose whole job
 * is "does this look like somewhere I would type my password", ornament costs more
 * credibility than it buys. What stands in for them is the wordmark, which is the thing
 * a person actually looks for to check they are in the right place.
 */
export function AuthCard({ title, subtitle, children, closeHref = "/" }: AuthCardProps) {
  return (
    <div className="w-full max-w-md">
      <div className="mb-6 flex justify-center">
        <Link href={closeHref} aria-label="Close and return home">
          <Wordmark />
        </Link>
      </div>

      <section className="rounded-[2rem] border border-border bg-surface px-6 py-8 shadow-lg sm:px-9 sm:py-10">
        <header className="mb-6 text-center">
          <h1 className="font-display text-[1.75rem] leading-tight sm:text-[2rem] text-text-primary">{title}</h1>
          {subtitle ? <p className="mt-2 text-sm text-text-secondary">{subtitle}</p> : null}
        </header>

        {children}
      </section>
    </div>
  );
}
