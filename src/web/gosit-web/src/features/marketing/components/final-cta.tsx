import Link from "next/link";
import { buttonClassName } from "@/components/ui/button";
import { ArrowRightIcon } from "@/components/ui/marketing-icons";
import { getStartedHref } from "@/features/marketing/public-mode";
import { Container } from "@/features/marketing/components/section";

/** The closing call to action, shared by every page so none of them ends in a dead end. */
export function FinalCta({
  title = "Ready when you are.",
  body = "Whether you need care or want to give it, getting started takes a few minutes.",
}: {
  title?: string;
  body?: string;
}) {
  return (
    <section className="pb-20 pt-6 sm:pb-28">
      <Container>
        <div className="deep-wash relative overflow-hidden rounded-[2rem] px-6 py-16 text-center sm:px-14 sm:py-24">
          <div aria-hidden className="dot-grid pointer-events-none absolute inset-0 opacity-20" />
          <div className="relative mx-auto max-w-2xl">
            <h2 className="font-display text-4xl leading-[1.08] text-text-on-deep sm:text-6xl">{title}</h2>
            <p className="mt-6 text-lg leading-relaxed text-text-on-deep/80">{body}</p>
            <div className="mt-10 flex flex-col justify-center gap-3 sm:flex-row">
              <Link href={getStartedHref("family")} className={buttonClassName("light", "!rounded-full", "xl")}>
                I need care
                <ArrowRightIcon />
              </Link>
              <Link
                href={getStartedHref("caregiver")}
                className={buttonClassName("secondary", "!rounded-full", "xl")}
              >
                I provide care
              </Link>
            </div>
          </div>
        </div>
      </Container>
    </section>
  );
}
