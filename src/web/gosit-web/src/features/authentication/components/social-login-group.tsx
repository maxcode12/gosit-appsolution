import Image from "next/image";

/**
 * Facebook / Apple / Google buttons from Figma 49:8 and 77:12.
 *
 * Social sign-in is a "Should Have" in PLAN.md and no provider is wired up yet, so
 * the controls render in the designed position but are disabled and say so. Showing
 * them as live buttons that silently do nothing would be worse than showing them
 * as not-yet-available.
 */

const UNAVAILABLE = "Social sign-in is not available yet.";

function SocialButton({
  icon,
  label,
  className = "",
}: {
  icon: string;
  label: string;
  className?: string;
}) {
  return (
    <button
      type="button"
      disabled
      title={UNAVAILABLE}
      aria-label={`${label} — ${UNAVAILABLE}`}
      className={`flex items-center justify-center gap-3 rounded-field bg-surface font-control text-base text-text-primary opacity-60 disabled:cursor-not-allowed ${className}`}
    >
      <Image src={icon} alt="" width={22} height={22} className="size-[1.375rem] object-contain" />
      <span>{label}</span>
    </button>
  );
}

export function SocialLoginGroup() {
  return (
    <div className="flex flex-col gap-4">
      <SocialButton
        icon="/brand/facebook.png"
        label="Continue with Facebook"
        className="h-[3.625rem] w-full border border-black"
      />

      <div className="flex gap-4">
        <SocialButton
          icon="/brand/apple.png"
          label="Apple"
          className="h-[3.125rem] flex-1 border-2 border-brand-primary shadow-[0_4px_4px_rgba(0,0,0,0.25)]"
        />
        <SocialButton
          icon="/brand/google.png"
          label="Google"
          className="h-[3.125rem] flex-1 border-2 border-brand-primary"
        />
      </div>

      <p className="text-center text-sm text-text-secondary">{UNAVAILABLE}</p>

      <div className="flex items-center gap-4" aria-hidden>
        <span className="h-px flex-1 bg-border" />
        <span className="font-control text-2xl text-text-primary">or</span>
        <span className="h-px flex-1 bg-border" />
      </div>
    </div>
  );
}
