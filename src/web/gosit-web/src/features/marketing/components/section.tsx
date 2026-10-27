import type { ReactNode } from "react";

/** The page's content width. One place, so every section lines up with the header. */
export function Container({ children, className = "" }: { children: ReactNode; className?: string }) {
  return <div className={`mx-auto max-w-7xl px-5 sm:px-8 ${className}`}>{children}</div>;
}

export function Eyebrow({ children, light = false }: { children: ReactNode; light?: boolean }) {
  return (
    <p
      className={`inline-flex items-center gap-2 font-control text-xs font-semibold uppercase tracking-[0.18em] ${
        light ? "text-accent-on-deep" : "text-brand-primary"
      }`}
    >
      <span aria-hidden className={`h-px w-6 ${light ? "bg-accent-on-deep" : "bg-brand-primary"}`} />
      {children}
    </p>
  );
}

/** A section's opening: a small label, a large serif headline, one supporting paragraph. */
export function SectionHeading({
  eyebrow,
  title,
  body,
  align = "left",
  light = false,
}: {
  eyebrow?: string;
  title: ReactNode;
  body?: ReactNode;
  align?: "left" | "center";
  light?: boolean;
}) {
  return (
    <div className={`reveal max-w-2xl ${align === "center" ? "mx-auto text-center" : ""}`}>
      {eyebrow ? <Eyebrow light={light}>{eyebrow}</Eyebrow> : null}
      <h2
        className={`font-display mt-4 text-4xl leading-[1.08] sm:text-5xl ${
          light ? "text-text-on-deep" : "text-text-primary"
        }`}
      >
        {title}
      </h2>
      {body ? (
        <p
          className={`mt-5 text-lg leading-relaxed ${
            light ? "text-text-on-deep/80" : "text-text-secondary"
          }`}
        >
          {body}
        </p>
      ) : null}
    </div>
  );
}

/** The opening band of every inner page. */
export function PageHero({
  eyebrow,
  title,
  body,
  children,
}: {
  eyebrow: string;
  title: ReactNode;
  body: ReactNode;
  children?: ReactNode;
}) {
  return (
    <section className="hero-wash relative overflow-hidden border-b border-border">
      <div aria-hidden className="dot-grid pointer-events-none absolute inset-0 opacity-60" />
      <Container className="relative py-20 sm:py-28">
        <div className="max-w-3xl">
          <Eyebrow>{eyebrow}</Eyebrow>
          <h1 className="font-display mt-5 text-5xl leading-[1.04] text-text-primary sm:text-6xl lg:text-7xl">
            {title}
          </h1>
          <p className="mt-7 max-w-2xl text-xl leading-relaxed text-text-secondary">{body}</p>
          {children ? <div className="mt-9 flex flex-wrap gap-3">{children}</div> : null}
        </div>
      </Container>
    </section>
  );
}
