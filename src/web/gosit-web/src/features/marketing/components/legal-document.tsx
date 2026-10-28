import type { ReactNode } from "react";
import Link from "next/link";
import { Container, Eyebrow } from "@/features/marketing/components/section";

/**
 * Shown on the privacy policy and the terms until counsel has signed them off. The
 * drafts in docs/legal say they must not reach users unreviewed; this keeps the pages
 * honest about that in the meantime. Set to false, in one place, when they are cleared.
 */
export const LEGAL_REVIEW_PENDING = true;

/** The date the wording last changed. Bump it with every edit to either document. */
export const LEGAL_LAST_UPDATED = "October 28, 2026";

export interface LegalSection {
  id: string;
  title: string;
  body: ReactNode;
}

/** A readable long-form page: a contents list, then numbered sections of prose. */
export function LegalDocument({
  eyebrow,
  title,
  intro,
  sections,
}: {
  eyebrow: string;
  title: string;
  intro: ReactNode;
  sections: LegalSection[];
}) {
  return (
    <>
      <section className="hero-wash relative overflow-hidden border-b border-border">
        <div aria-hidden className="dot-grid pointer-events-none absolute inset-0 opacity-60" />
        <Container className="relative max-w-3xl py-12 sm:py-16">
          <Eyebrow>{eyebrow}</Eyebrow>
          <h1 className="font-display mt-5 text-5xl leading-[1.04] text-text-primary sm:text-6xl">{title}</h1>
          <p className="mt-4 font-control text-sm text-text-muted">Last updated {LEGAL_LAST_UPDATED}</p>
          <div className="mt-6 text-lg leading-relaxed text-text-secondary">{intro}</div>
        </Container>
      </section>

      <section className="py-12 sm:py-16">
        <Container className="grid gap-12 lg:grid-cols-[16rem_1fr]">
          <nav aria-label="Contents" className="lg:sticky lg:top-24 lg:self-start">
            <p className="font-control text-xs font-semibold uppercase tracking-[0.18em] text-text-muted">
              Contents
            </p>
            <ol className="mt-4 space-y-2 text-sm">
              {sections.map((section, index) => (
                <li key={section.id}>
                  <a
                    href={`#${section.id}`}
                    className="text-text-secondary transition-colors hover:text-brand-primary"
                  >
                    {index + 1}. {section.title}
                  </a>
                </li>
              ))}
            </ol>
          </nav>

          <div className="max-w-3xl">
            {LEGAL_REVIEW_PENDING ? (
              <p
                role="note"
                className="mb-10 rounded-2xl border border-border bg-surface-muted p-5 text-sm leading-relaxed text-text-secondary"
              >
                <strong className="font-semibold text-text-primary">Pending legal review.</strong> This
                document describes what GoSit does today. It is being reviewed by Canadian counsel and
                will be revised before the platform opens more widely.
              </p>
            ) : null}

            <div className="space-y-12">
              {sections.map((section, index) => (
                <section key={section.id} id={section.id} className="scroll-mt-24">
                  <h2 className="font-heading text-2xl font-bold text-text-primary">
                    {index + 1}. {section.title}
                  </h2>
                  <div className="mt-4 space-y-4 text-base leading-relaxed text-text-secondary">
                    {section.body}
                  </div>
                </section>
              ))}
            </div>

            <p className="mt-16 border-t border-border pt-6 text-sm text-text-muted">
              Questions? <Link href="/contact" className="underline hover:text-text-secondary">Contact us</Link>.
            </p>
          </div>
        </Container>
      </section>
    </>
  );
}

/** A bulleted list that lives inside a legal section. */
export function Points({ items }: { items: ReactNode[] }) {
  return (
    <ul className="list-disc space-y-2 pl-5 marker:text-brand-primary">
      {items.map((item, index) => (
        <li key={index}>{item}</li>
      ))}
    </ul>
  );
}
