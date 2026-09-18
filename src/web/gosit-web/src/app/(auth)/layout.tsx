import type { ReactNode } from "react";

/**
 * The auth screens sit on the muted page surface with the card centred
 * (Figma 49:8, 77:12).
 */
export default function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <main className="relative flex min-h-screen items-center justify-center overflow-hidden bg-canvas px-4 py-12">
      {/* The same soft washes as the landing hero, so the two feel like one product. */}
      <div aria-hidden className="pointer-events-none absolute inset-0 -z-10">
        <div className="absolute -left-40 -top-40 size-[34rem] rounded-full bg-brand-soft blur-3xl" />
        <div className="absolute -bottom-40 -right-40 size-[30rem] rounded-full bg-accent-soft blur-3xl" />
      </div>

      {children}
    </main>
  );
}
