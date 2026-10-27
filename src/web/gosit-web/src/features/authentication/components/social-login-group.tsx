import Image from "next/image";

/**
 * Facebook / Apple / Google buttons from Figma 49:8 and 77:12.
 *
 * Social sign-in is a "Should Have" in PLAN.md and no provider is wired up yet, so
 * the controls render in the designed position but are disabled and say so. Showing
 * them as live buttons that silently do nothing would be worse than showing them
 * as not-yet-available.
 *
 * Drawn as one quiet row, so a set of controls that do nothing yet takes up as little
 * of the form as possible.
 */

const UNAVAILABLE = "Social sign-in is not available yet.";

const PROVIDERS: { icon: string; label: string; accessibleName?: string }[] = [
  { icon: "/brand/facebook.png", label: "Facebook", accessibleName: "Continue with Facebook" },
  { icon: "/brand/apple.png", label: "Apple" },
  { icon: "/brand/google.png", label: "Google" },
];

export function SocialLoginGroup() {
  return (
    <div className="flex flex-col gap-3">
      <div className="grid grid-cols-3 gap-2.5">
        {PROVIDERS.map((provider) => (
          <button
            key={provider.label}
            type="button"
            disabled
            title={UNAVAILABLE}
            aria-label={`${provider.accessibleName ?? provider.label} — ${UNAVAILABLE}`}
            className="flex h-11 items-center justify-center gap-2 rounded-xl border border-border bg-surface font-control text-sm text-text-primary opacity-55 disabled:cursor-not-allowed"
          >
            <Image src={provider.icon} alt="" width={20} height={20} className="size-5 object-contain" />
            <span className="max-sm:sr-only">{provider.label}</span>
          </button>
        ))}
      </div>

      <p className="text-center text-xs text-text-muted">Social sign-in is coming soon.</p>

      <div className="flex items-center gap-3" aria-hidden>
        <span className="h-px flex-1 bg-border" />
        <span className="font-control text-xs font-medium uppercase tracking-widest text-text-muted">or</span>
        <span className="h-px flex-1 bg-border" />
      </div>
    </div>
  );
}
