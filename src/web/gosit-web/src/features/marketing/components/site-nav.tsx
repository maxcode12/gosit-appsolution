"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { buttonClassName } from "@/components/ui/button";
import { CloseIcon, MenuIcon } from "@/components/ui/marketing-icons";
import { ThemeToggle } from "@/components/ui/theme-toggle";
import { Wordmark } from "@/components/ui/wordmark";
import { MARKETING_ONLY, getStartedHref } from "@/features/marketing/public-mode";

const LINKS = [
  { href: "/for-families", label: "For families" },
  { href: "/for-caregivers", label: "For caregivers" },
  { href: "/services", label: "Care we cover" },
  { href: "/how-it-works", label: "How it works" },
  { href: "/safety", label: "Trust & safety" },
  { href: "/faq", label: "FAQ" },
];

/**
 * The public header: a floating, translucent bar with the site's pages, a theme toggle and
 * the one action. Below `lg` the links fold into a full-width sheet, because the old
 * header simply lost them on a phone.
 */
export function SiteNav() {
  const pathname = usePathname();
  // The sheet is open for one specific page, so navigating away closes it without an effect.
  const [openFor, setOpenFor] = useState<string | null>(null);
  const open = openFor === pathname;


  return (
    <header className="sticky top-0 z-50 px-3 pt-3 sm:px-5">
      <div className="mx-auto max-w-7xl rounded-2xl border border-border/70 bg-surface/80 shadow-md backdrop-blur-xl">
        <div className="flex h-16 items-center justify-between gap-4 px-4 sm:px-6">
          <Link href="/" aria-label="GoSit home">
            <Wordmark />
          </Link>

          <nav aria-label="Main" className="hidden items-center gap-0.5 xl:flex">
            {LINKS.map((link) => {
              const current = pathname === link.href;

              return (
                <Link
                  key={link.href}
                  href={link.href}
                  aria-current={current ? "page" : undefined}
                  className={`whitespace-nowrap rounded-full px-3.5 py-2 font-control text-sm font-medium transition-colors ${
                    current
                      ? "bg-brand-soft text-brand-primary"
                      : "text-text-secondary hover:bg-surface-muted hover:text-text-primary"
                  }`}
                >
                  {link.label}
                </Link>
              );
            })}
          </nav>

          <div className="flex items-center gap-2">
            <ThemeToggle />
            {MARKETING_ONLY ? null : (
              <Link
                href="/login"
                className="hidden rounded-full px-3.5 py-2 font-control text-sm font-semibold text-text-primary transition-colors hover:text-brand-primary sm:block"
              >
                Log in
              </Link>
            )}
            <Link
              href={getStartedHref()}
              className={buttonClassName("primary", "!rounded-full max-sm:!hidden whitespace-nowrap", "md")}
            >
              Get started
            </Link>
            <button
              type="button"
              aria-expanded={open}
              aria-controls="mobile-menu"
              aria-label={open ? "Close menu" : "Open menu"}
              onClick={() => setOpenFor(open ? null : pathname)}
              className="grid size-10 place-items-center rounded-full text-text-primary transition-colors hover:bg-surface-muted xl:hidden"
            >
              {open ? <CloseIcon /> : <MenuIcon />}
            </button>
          </div>
        </div>

        {open ? (
          <nav id="mobile-menu" aria-label="Mobile" className="border-t border-border px-4 pb-5 pt-3 xl:hidden">
            <ul className="flex flex-col">
              {LINKS.map((link) => (
                <li key={link.href}>
                  <Link
                    href={link.href}
                    className="block rounded-xl px-3 py-3.5 font-heading text-lg font-semibold text-text-primary hover:bg-surface-muted"
                  >
                    {link.label}
                  </Link>
                </li>
              ))}
            </ul>
            <div className="mt-3 flex gap-3">
              <Link href={getStartedHref()} className={buttonClassName("primary", "!rounded-full flex-1", "lg")}>
                Get started
              </Link>
              {MARKETING_ONLY ? null : (
                <Link href="/login" className={buttonClassName("ghost", "!rounded-full flex-1", "lg")}>
                  Log in
                </Link>
              )}
            </div>
          </nav>
        ) : null}
      </div>
    </header>
  );
}
