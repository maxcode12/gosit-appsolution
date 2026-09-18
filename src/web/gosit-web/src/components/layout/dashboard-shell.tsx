import Link from "next/link";
import type { ReactNode } from "react";
import { SignOutIcon } from "@/components/ui/icons";
import { ThemeToggle } from "@/components/ui/theme-toggle";
import { Wordmark } from "@/components/ui/wordmark";
import { logoutAction } from "@/features/authentication/actions";

/**
 * The signed-in application frame: a navigation rail beside a content column.
 *
 * On small screens the rail becomes a horizontal scroller above the content rather than
 * a drawer: it holds five or six destinations, so a drawer would add a tap for no gain.
 *
 * The rail is sticky on desktop and the content column is capped at a readable measure
 * rather than stretching to the window — a form field three feet wide is nobody's idea
 * of a good time.
 */

export interface NavItem {
  href: string;
  label: string;
  /** Rendered as a decorative glyph; the label carries the meaning. */
  icon: ReactNode;
  /** Shown as a count badge, e.g. unread messages. Hidden when zero. */
  badge?: number;
  /**
   * What the badge counts, for assistive technology — "3 unread", "3 waiting". The
   * number alone is meaningless read aloud, and "unread" is wrong for a staff queue.
   */
  badgeLabel?: string;
}

interface DashboardShellProps {
  items: NavItem[];
  /** Path of the current page, used to mark the active destination. */
  currentPath: string;
  heading: string;
  description?: string;
  /** Optional controls rendered beside the heading — a search bar, a primary action. */
  actions?: ReactNode;
  children: ReactNode;
}

export function DashboardShell({
  items,
  currentPath,
  heading,
  description,
  actions,
  children,
}: DashboardShellProps) {
  // The rail's first destination is each side's home, which is where the wordmark goes.
  const homeHref = items[0]?.href ?? "/";

  return (
    <div className="min-h-screen bg-canvas">
      <div className="mx-auto flex max-w-[88rem] flex-col gap-6 px-4 py-5 lg:flex-row lg:gap-10 lg:px-8 lg:py-8">
        <div className="lg:w-60 lg:shrink-0">
          <div className="lg:sticky lg:top-8">
            <Link
              href={homeHref}
              className="mb-6 hidden items-center lg:inline-flex"
              aria-label="GoSit home"
            >
              <Wordmark />
            </Link>

            {/*
              Row on small screens so the rail keeps its single line and sign-out sits at
              the end of it; stacked underneath on desktop, where the rail is a column.
            */}
            <div className="flex items-center gap-2 lg:flex-col lg:items-stretch lg:gap-5">
              <div className="min-w-0 flex-1">
                <SideNav items={items} currentPath={currentPath} />
              </div>

              <ThemeToggle />
              <SignOut />
            </div>
          </div>
        </div>

        <main className="min-w-0 flex-1 pb-16">
          <header className="mb-8 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="min-w-0">
              <h1 className="font-heading text-2xl font-bold tracking-tight text-text-primary sm:text-3xl">
                {heading}
              </h1>
              {description ? (
                <p className="mt-1.5 text-text-secondary">{description}</p>
              ) : null}
            </div>
            {actions ? <div className="shrink-0">{actions}</div> : null}
          </header>

          {children}
        </main>
      </div>
    </div>
  );
}

/**
 * Signing out.
 *
 * A plain form posting to a Server Action, deliberately: it works before React has
 * hydrated and with client JavaScript disabled entirely, which is the right property for
 * the one control someone reaches for on a shared or borrowed machine. That also keeps
 * the shell a Server Component — a `"use client"` here would pull every screen's frame
 * into the client bundle to serve one button.
 *
 * Set apart from the rail rather than listed in it. Sign-out is not a destination, and
 * putting it among the destinations invites the mis-tap it cannot be undone from — the
 * session is revoked server-side, so signing back in is the only way back.
 *
 * No confirmation step. Logging in again is a small cost, and a dialog on every sign-out
 * is a larger one paid by everyone who meant it.
 */
function SignOut() {
  return (
    <form action={logoutAction} className="lg:border-t lg:border-border lg:pt-5">
      <button
        type="submit"
        className="group flex w-full items-center gap-3 whitespace-nowrap rounded-control px-3.5 py-2.5 font-control text-sm font-medium text-text-secondary transition-colors duration-200 hover:bg-surface hover:text-text-primary"
      >
        <span
          aria-hidden
          className="grid size-5 place-items-center text-text-muted transition-colors group-hover:text-text-primary"
        >
          <SignOutIcon />
        </span>
        <span>Log out</span>
      </button>
    </form>
  );
}

function SideNav({ items, currentPath }: { items: NavItem[]; currentPath: string }) {
  return (
    <nav aria-label="Main">
      <ul className="flex gap-1.5 overflow-x-auto pb-1 lg:flex-col lg:overflow-visible lg:pb-0">
        {items.map((item) => {
          // Exact match for each side's home, prefix match elsewhere, so a detail page
          // keeps its section highlighted without the home entry matching everything.
          const isHome = item.href === "/dashboard" || item.href === "/caregiver";

          const isActive = isHome
            ? currentPath === item.href
            : currentPath.startsWith(item.href);

          return (
            <li key={item.href} className="shrink-0 lg:shrink">
              <Link
                href={item.href}
                aria-current={isActive ? "page" : undefined}
                className={`group flex items-center gap-3 whitespace-nowrap rounded-control px-3.5 py-2.5 font-control text-sm font-medium transition-all duration-200 ${
                  isActive
                    ? "bg-brand-primary text-text-on-brand shadow-brand"
                    : "text-text-secondary hover:bg-surface hover:text-text-primary"
                }`}
              >
                <span
                  aria-hidden
                  className={`grid size-5 place-items-center transition-colors ${
                    isActive ? "" : "text-text-muted group-hover:text-brand-primary"
                  }`}
                >
                  {item.icon}
                </span>
                <span>{item.label}</span>
                {item.badge ? (
                  <span
                    className={`ml-auto rounded-pill px-2 py-0.5 text-xs font-semibold ${
                      isActive
                        ? "bg-white/20 text-text-on-brand"
                        : "bg-brand-primary text-text-on-brand"
                    }`}
                    aria-label={`${item.badge} ${item.badgeLabel ?? "unread"}`}
                  >
                    {item.badge > 99 ? "99+" : item.badge}
                  </span>
                ) : null}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
