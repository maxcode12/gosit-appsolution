import Link from "next/link";
import { buttonClassName } from "@/components/ui/button";
import { ThemeToggle } from "@/components/ui/theme-toggle";
import { Wordmark } from "@/components/ui/wordmark";
import { CONTACT_ANCHOR, MARKETING_ONLY } from "@/features/marketing/public-mode";

const LINKS = [
  { href: "#care", label: "Types of care" },
  { href: "#how", label: "How it works" },
  { href: "#safety", label: "Trust & safety" },
  { href: "#caregivers", label: "For caregivers" },
];

/**
 * The public header.
 *
 * Sticky and translucent: the page behind it stays visible while scrolling, which keeps
 * a long marketing page feeling like one surface rather than a stack of slides. The
 * backdrop blur is a progressive enhancement — without it the background is simply
 * opaque, which is fine.
 */
export function SiteHeader() {
  return (
    <header className="sticky top-0 z-50 border-b border-border/60 bg-canvas/80 backdrop-blur-xl">
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-6 px-4 sm:px-6">
        <Link href="/" aria-label="GoSit home">
          <Wordmark />
        </Link>

        <nav aria-label="Sections" className="hidden items-center gap-1 md:flex">
          {LINKS.map((link) => (
            <a
              key={link.href}
              href={link.href}
              className="rounded-control px-3 py-2 font-control text-sm font-medium text-text-secondary transition-colors hover:bg-surface hover:text-text-primary"
            >
              {link.label}
            </a>
          ))}
        </nav>

        <div className="flex items-center gap-2">
          <ThemeToggle />
          {MARKETING_ONLY ? (
            <a
              href={CONTACT_ANCHOR}
              className={buttonClassName("primary", "", "sm")}
            >
              Contact us
            </a>
          ) : (
            <>
              <Link
                href="/login"
                className="hidden rounded-control px-3 py-2 font-control text-sm font-semibold text-text-primary transition-colors hover:text-brand-primary sm:block"
              >
                Log in
              </Link>
              <Link href="/signup" className={buttonClassName("primary", "", "sm")}>
                Get started
              </Link>
            </>
          )}
        </div>
      </div>
    </header>
  );
}
