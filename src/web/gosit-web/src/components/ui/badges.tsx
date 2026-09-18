import { ShieldIcon, StarIcon } from "@/components/ui/icons";

/**
 * The verified badge.
 *
 * Only ever shown for a caregiver the API reports as verified — the client must not
 * derive this from anything else, because it is the single strongest trust signal a
 * family reads before letting someone into their home.
 */
export function VerifiedBadge() {
  return (
    <span className="inline-flex items-center gap-1.5 rounded-pill border border-brand-soft-border bg-brand-soft px-2.5 py-1 text-xs font-semibold text-brand-primary">
      <ShieldIcon />
      Verified
    </span>
  );
}

/**
 * A rating and its count.
 *
 * An unrated caregiver shows "New" rather than zero stars: a new caregiver has not
 * been rated badly, and showing them as zero would be a false signal.
 */
export function RatingStars({
  averageRating,
  reviewCount,
}: {
  averageRating: number | null;
  reviewCount: number;
}) {
  if (averageRating === null || reviewCount === 0) {
    return (
      <span className="text-sm text-text-secondary">New to GoSit</span>
    );
  }

  const rounded = Math.round(averageRating);

  return (
    <span
      className="inline-flex items-center gap-1.5 text-sm text-text-primary"
      aria-label={`Rated ${averageRating} out of 5 from ${reviewCount} reviews`}
    >
      <span aria-hidden className="flex text-brand-accent">
        {[1, 2, 3, 4, 5].map((star) => (
          <StarIcon key={star} filled={star <= rounded} />
        ))}
      </span>
      <span className="font-semibold">{averageRating.toFixed(1)}</span>
      <span className="text-text-secondary">({reviewCount})</span>
    </span>
  );
}

/** A neutral chip for a service or certification code. */
export function Chip({ children }: { children: React.ReactNode }) {
  return (
    <span className="inline-flex items-center rounded-pill bg-surface-muted px-3 py-1.5 text-xs font-medium text-text-secondary">
      {children}
    </span>
  );
}

/**
 * An initial in a tinted circle, standing in for a photo.
 *
 * The tint is derived from the name rather than random, so the same person is the same
 * colour everywhere — which is what makes it useful for recognition rather than just
 * decoration.
 */
const AVATAR_TINTS = [
  "bg-brand-soft text-brand-primary",
  "bg-accent-soft text-brand-secondary",
  "bg-success-soft text-success-accent",
  "bg-surface-muted text-text-secondary",
];

export function Avatar({
  name,
  size = "md",
}: {
  name: string;
  size?: "sm" | "md" | "lg";
}) {
  const sizeClass =
    size === "lg" ? "size-14 text-xl" : size === "sm" ? "size-9 text-sm" : "size-11 text-lg";

  // Sum of char codes: stable, cheap, and spreads short names across the ramp.
  const tint =
    AVATAR_TINTS[
      [...name].reduce((total, character) => total + character.charCodeAt(0), 0)
        % AVATAR_TINTS.length
    ];

  return (
    <span
      aria-hidden
      className={`grid shrink-0 place-items-center rounded-full font-heading font-semibold ${sizeClass} ${tint}`}
    >
      {name.slice(0, 1).toUpperCase()}
    </span>
  );
}

/**
 * Turns a stable code like OVERNIGHT_CHILD_CARE into readable text.
 *
 * A fallback only. The API returns display names for services, and those are preferred
 * wherever they are available — this exists for certification codes, which have no
 * display name on the search contract.
 */
export function humanizeCode(code: string): string {
  return code
    .toLowerCase()
    .split("_")
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(" ");
}
