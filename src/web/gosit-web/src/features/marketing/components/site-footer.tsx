import Link from "next/link";
import { Wordmark } from "@/components/ui/wordmark";
import { MARKETING_ONLY, getStartedHref } from "@/features/marketing/public-mode";
import { Container } from "@/features/marketing/components/section";

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
      <Container className="py-16">
        <div className="grid gap-12 lg:grid-cols-[1.4fr_2fr]">
          <div className="max-w-sm">
            <Wordmark />
            <p className="font-display mt-6 text-2xl leading-snug text-text-primary">
              Trusted care for every stage of family life.
            </p>
            <p className="mt-4 text-sm leading-relaxed text-text-secondary">
              Serving Ontario, with more of Canada to come.
            </p>
          </div>

          <div className="grid grid-cols-2 gap-10 sm:grid-cols-4">
            <FooterColumn
              title="Families"
              links={[
                { href: "/for-families", label: "For families" },
                { href: "/services", label: "Care we cover" },
                { href: "/how-it-works", label: "How it works" },
                { href: getStartedHref("family"), label: "Find a caregiver" },
              ]}
            />
            <FooterColumn
              title="Caregivers"
              links={[
                { href: "/for-caregivers", label: "Why GoSit" },
                { href: getStartedHref("caregiver"), label: "Become a caregiver" },
                { href: "/faq", label: "Caregiver FAQ" },
              ]}
            />
            <FooterColumn
              title="Company"
              links={[
                { href: "/about", label: "About" },
                { href: "/safety", label: "Trust & safety" },
                { href: "/faq", label: "FAQ" },
                { href: "/contact", label: "Contact" },
              ]}
            />
            <FooterColumn
              title="Account"
              links={
                MARKETING_ONLY
                  ? [{ href: "/get-started", label: "Join the early list" }]
                  : [
                      { href: "/login", label: "Log in" },
                      { href: "/signup", label: "Create an account" },
                    ]
              }
            />
          </div>
        </div>

        <div className="mt-14 border-t border-border pt-8">
          <p className="max-w-3xl text-sm leading-relaxed text-text-muted">
            GoSit is a marketplace that connects families with independent caregivers. It arranges{" "}
            <strong className="font-semibold text-text-secondary">non-medical care</strong>: caregivers on
            GoSit do not administer medication, perform nursing procedures, or provide therapy or
            diagnosis. If you need regulated health care, speak to a licensed provider. Screening
            reduces risk; it is not a guarantee.
          </p>
          <p className="mt-4 text-xs text-text-muted">
            Postal-area locations from{" "}
            <a href="https://www.geonames.org/" className="underline hover:text-text-secondary" rel="noopener">
              GeoNames
            </a>
            , licensed under{" "}
            <a href="https://creativecommons.org/licenses/by/4.0/" className="underline hover:text-text-secondary" rel="noopener">
              CC BY 4.0
            </a>
            .
          </p>
          <p className="mt-3 text-sm text-text-muted">
            © {new Date().getFullYear()} 11419056 Canada Inc., operating as GoSit. Made in Ontario.
          </p>
        </div>
      </Container>
    </footer>
  );
}

function FooterColumn({ title, links }: { title: string; links: { href: string; label: string }[] }) {
  return (
    <div>
      <h2 className="font-control text-sm font-semibold text-text-primary">{title}</h2>
      <ul className="mt-4 flex flex-col gap-3">
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
