import Image from "next/image";
import type { ReactNode } from "react";
import { CheckIcon } from "@/components/ui/marketing-icons";

/**
 * Sign-in and sign-up sit beside a brand panel on a wide screen, and on their own on a
 * phone. The panel is there so the first screen after the website still looks like the
 * same product, and to say again, at the moment of handing over details, what GoSit does
 * with them.
 */
export default function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <main className="grid min-h-screen bg-canvas lg:grid-cols-[1fr_1.05fr]">
      {/* Pinned to the viewport so the headline stays in view however tall the form is. */}
      <aside className="deep-wash relative hidden self-start overflow-hidden lg:sticky lg:top-0 lg:block lg:h-screen">
        <Image
          src="/images/hero.jpg"
          alt=""
          fill
          sizes="45vw"
          className="object-cover object-[70%_center] opacity-25 mix-blend-luminosity"
        />
        <div aria-hidden className="absolute inset-0 bg-gradient-to-t from-[var(--color-brand-deep)] via-transparent to-transparent" />

        <div className="relative flex h-full flex-col justify-end p-14 xl:p-20">
          <h2 className="font-display max-w-md text-5xl leading-[1.05] text-text-on-deep">
            Trusted care for every stage of family life.
          </h2>
          <ul className="mt-10 space-y-4 text-text-on-deep/90">
            {[
              "Caregivers are identity-checked and screened for the care they offer",
              "Your address and care notes stay private until you book",
              "You are charged for the time actually worked",
            ].map((point) => (
              <li key={point} className="flex items-start gap-3 text-base">
                <span className="mt-0.5 grid size-6 shrink-0 place-items-center rounded-full bg-white/15 text-accent-on-deep">
                  <CheckIcon className="size-3.5" />
                </span>
                {point}
              </li>
            ))}
          </ul>
        </div>
      </aside>

      <div className="relative flex items-center justify-center overflow-hidden px-4 py-12">
        {/* The same soft washes as the website, so the two feel like one product. */}
        <div aria-hidden className="pointer-events-none absolute inset-0 -z-10">
          <div className="absolute -right-40 -top-40 size-[34rem] rounded-full bg-brand-soft blur-3xl" />
          <div className="absolute -bottom-40 -left-40 size-[30rem] rounded-full bg-accent-soft blur-3xl" />
        </div>

        {children}
      </div>
    </main>
  );
}
