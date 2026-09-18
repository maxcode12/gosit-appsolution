import Link from "next/link";
import type { ReactNode } from "react";
import { Wordmark } from "@/components/ui/wordmark";

interface AuthCardProps {
  title: string;
  subtitle?: string;
  children: ReactNode;
  /** Where the close control returns to. The wireframes present these screens as modals. */
  closeHref?: string;
}

/**
 * Shared shell for the Login and Sign Up screens.
 *
 * The wireframes (49:8, 77:12) frame these as modal cards with decorative crossing
 * bars over the corner. The card is kept; the bars are not. They read as a 2019
 * flourish, and on a screen whose whole job is "does this look like somewhere I would
 * type my password", ornament costs more credibility than it buys.
 *
 * What replaces them is the wordmark, which is the thing a person actually looks for to
 * check they are in the right place.
 */
export function AuthCard({
  title,
  subtitle,
  children,
  closeHref = "/",
}: AuthCardProps) {
  return (
    <div className="w-full max-w-md">
      <div className="mb-8 flex justify-center">
        <Link href={closeHref} aria-label="Close and return home">
          <Wordmark />
        </Link>
      </div>

      <section className="rounded-auth-card border border-border bg-surface px-6 py-10 shadow-lg sm:px-10">
        <header className="mb-8 text-center">
          <h1 className="font-heading text-2xl font-bold tracking-tight text-text-primary">
            {title}
          </h1>
          {subtitle ? (
            <p className="mt-2 text-text-secondary">{subtitle}</p>
          ) : null}
        </header>

        {children}
      </section>
    </div>
  );
}
