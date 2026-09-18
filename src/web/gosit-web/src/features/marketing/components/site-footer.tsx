import Link from "next/link";
import { Wordmark } from "@/components/ui/wordmark";

/**
 * The public footer.
 *
 * Carries the non-medical boundary in plain words. GoSit is a non-medical care
 * marketplace, and a family arranging support for a parent with dementia needs to know
 * that before they book, not after — burying it in terms nobody opens would be the
 * wrong call.
 */
export function SiteFooter() {
  return (
    <footer className="border-t border-border bg-surface">
      <div className="mx-auto max-w-6xl px-4 py-14 sm:px-6">
        <div className="flex flex-col gap-10 md:flex-row md:justify-between">
          <div className="max-w-xs">
            <Wordmark />
            <p className="mt-4 text-sm leading-relaxed text-text-secondary">
              Trusted care for every stage of family life. Serving Ontario, with more of
              Canada to come.
            </p>
          </div>

          <div className="grid grid-cols-2 gap-10 sm:grid-cols-3">
            <FooterColumn
              title="Care"
              links={[
                { href: "#care", label: "Child care" },
                { href: "#care", label: "Senior care" },
                { href: "#care", label: "Specialized support" },
                { href: "#care", label: "Family support" },
                { href: "#care", label: "Pet care" },
              ]}
            />
            <FooterColumn
              title="GoSit"
              links={[
                { href: "#how", label: "How it works" },
                { href: "#safety", label: "Trust & safety" },
                { href: "#caregivers", label: "Become a caregiver" },
              ]}
            />
            <FooterColumn
              title="Account"
              links={[
                { href: "/login", label: "Log in" },
                { href: "/signup", label: "Create an account" },
              ]}
            />
          </div>
        </div>

        <div className="mt-12 border-t border-border pt-8">
          <p className="max-w-3xl text-sm leading-relaxed text-text-muted">
            GoSit arranges <strong className="font-semibold text-text-secondary">non-medical
            care</strong>. Our caregivers do not administer medication, perform nursing
            procedures, or provide therapy or diagnosis. If you need regulated health
            care, speak to a licensed provider.
          </p>
          <p className="mt-4 text-sm text-text-muted">
            © {new Date().getFullYear()} GoSit. Made in Ontario.
          </p>
        </div>
      </div>
    </footer>
  );
}

function FooterColumn({
  title,
  links,
}: {
  title: string;
  links: { href: string; label: string }[];
}) {
  return (
    <div>
      <h2 className="font-control text-sm font-semibold text-text-primary">{title}</h2>
      <ul className="mt-3 flex flex-col gap-2.5">
        {links.map((link) => (
          <li key={link.label}>
            <Link
              href={link.href}
              className="text-sm text-text-secondary transition-colors hover:text-brand-primary"
            >
              {link.label}
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
