import Image from "next/image";
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

/**
 * A small card that floats over a photograph, showing what the product promises as it
 * would look in use. Illustration, not testimony: no names, no people, because the
 * photographs are licensed stock and must never read as real GoSit caregivers (see
 * public/images/CREDITS.md).
 */
export type HeroBadge = { icon: ReactNode; title: string; body: string };

export type HeroImage = { src: string; alt: string; badge?: HeroBadge; position?: string };

/** The opening band of every inner page: words on one side, a large photograph on the other. */
export function PageHero({
  eyebrow,
  title,
  body,
  children,
  image,
}: {
  eyebrow: string;
  title: ReactNode;
  body: ReactNode;
  children?: ReactNode;
  image?: HeroImage;
}) {
  return (
    <section className="hero-wash relative overflow-hidden border-b border-border">
      <div aria-hidden className="dot-grid pointer-events-none absolute inset-0 opacity-60" />
      <Container
        className={`relative py-12 sm:py-16 ${
          image ? "grid items-center gap-12 lg:grid-cols-[1.05fr_0.95fr]" : ""
        }`}
      >
        <div className={image ? "" : "max-w-3xl"}>
          <Eyebrow>{eyebrow}</Eyebrow>
          <h1 className="font-display mt-5 text-5xl leading-[1.04] text-text-primary sm:text-6xl lg:text-[4.25rem]">
            {title}
          </h1>
          <p className="mt-6 max-w-xl text-lg leading-relaxed text-text-secondary sm:text-xl">{body}</p>
          {children ? <div className="mt-8 flex flex-wrap gap-3">{children}</div> : null}
        </div>

        {image ? (
          <div className="relative">
            <div className="relative aspect-[5/4] overflow-hidden rounded-[2rem] shadow-lg ring-1 ring-border">
              <Image
                src={image.src}
                alt={image.alt}
                fill
                priority
                sizes="(min-width: 1024px) 45vw, 100vw"
                className={`object-cover ${image.position ?? ""}`}
              />
            </div>
            {image.badge ? (
              <div className="drift absolute -bottom-5 left-4 flex items-center gap-3 rounded-2xl border border-border bg-surface p-3.5 pr-5 shadow-lg sm:-left-5">
                <span className="grid size-11 place-items-center rounded-xl bg-brand-soft text-brand-primary">
                  {image.badge.icon}
                </span>
                <div>
                  <p className="font-heading text-sm font-bold text-text-primary">{image.badge.title}</p>
                  <p className="text-xs text-text-secondary">{image.badge.body}</p>
                </div>
              </div>
            ) : null}
          </div>
        ) : null}
      </Container>
    </section>
  );
}

/**
 * A full-bleed photograph with the headline set over it. The scrim uses fixed dark
 * values: the ground is a photograph, which does not follow the light or dark theme.
 */
export function PageBanner({
  eyebrow,
  title,
  body,
  image,
  alt,
  chips = [],
}: {
  eyebrow: string;
  title: ReactNode;
  body: ReactNode;
  image: string;
  alt: string;
  chips?: string[];
}) {
  return (
    <section className="relative isolate overflow-hidden">
      <Image src={image} alt={alt} fill priority sizes="100vw" className="object-cover object-[60%_22%]" />
      <div
        aria-hidden
        className="absolute inset-0 bg-gradient-to-t from-[rgba(12,20,22,0.9)] via-[rgba(12,20,22,0.6)] to-[rgba(12,20,22,0.35)] lg:bg-gradient-to-r lg:from-[rgba(12,20,22,0.92)] lg:via-[rgba(12,20,22,0.7)] lg:to-[rgba(12,20,22,0.1)]"
      />
      <Container className="relative flex min-h-[30rem] flex-col justify-end py-14 sm:min-h-[36rem] sm:py-20 lg:justify-center">
        <div className="max-w-2xl">
          <Eyebrow light>{eyebrow}</Eyebrow>
          <h1 className="font-display mt-5 text-5xl leading-[1.04] text-text-on-deep sm:text-6xl lg:text-7xl">
            {title}
          </h1>
          <p className="mt-6 max-w-xl text-lg leading-relaxed text-text-on-deep/85 sm:text-xl">{body}</p>
          {chips.length > 0 ? (
            <ul className="mt-8 flex flex-wrap gap-2">
              {chips.map((chip) => (
                <li
                  key={chip}
                  className="rounded-full border border-white/25 bg-white/10 px-4 py-2 font-control text-sm font-medium text-text-on-deep backdrop-blur-sm"
                >
                  {chip}
                </li>
              ))}
            </ul>
          ) : null}
        </div>
      </Container>
    </section>
  );
}
