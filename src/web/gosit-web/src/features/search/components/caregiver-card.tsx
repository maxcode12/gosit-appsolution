import Link from "next/link";
import { Chip, RatingStars, VerifiedBadge, humanizeCode } from "@/components/ui/badges";
import { PinIcon } from "@/components/ui/icons";
import { FavouriteButton } from "@/features/favorites/components/favourite-button";
import type { CaregiverSearchResult } from "@/features/search/types";

/**
 * One caregiver in a result list.
 *
 * Shows only what the API returns, which is deliberately narrow: a first name and last
 * initial, an approximate location, and currently-valid certifications. There is no
 * street address, contact detail, or full name to render even if the card wanted one.
 *
 * The whole card is a link to the profile, with the save control layered above it —
 * a nested interactive element inside an anchor would be invalid, so the anchor covers
 * the card via a pseudo-element and the button sits above on its own stacking context.
 */
export function CaregiverCard({ caregiver }: { caregiver: CaregiverSearchResult }) {
  const name = `${caregiver.firstName}${caregiver.lastInitial ? ` ${caregiver.lastInitial}.` : ""}`;

  return (
    <article className="relative flex flex-col gap-4 rounded-card border border-border bg-surface p-5 transition-shadow focus-within:shadow-md hover:shadow-md">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="font-heading text-lg font-medium text-text-primary">
            <Link
              href={`/caregivers/${caregiver.caregiverProfileId}`}
              className="after:absolute after:inset-0 after:content-[''] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
            >
              {name}
            </Link>
          </h3>
          <p className="mt-1 flex items-center gap-1.5 text-sm text-text-secondary">
            <PinIcon />
            {caregiver.approximateLocation}
            {caregiver.distanceKm !== null ? (
              <span className="text-text-muted">· {caregiver.distanceKm} km away</span>
            ) : null}
          </p>
        </div>

        {/* Above the card-covering link so it stays clickable. */}
        <div className="relative z-10 shrink-0">
          <FavouriteButton
            caregiverProfileId={caregiver.caregiverProfileId}
            caregiverName={name}
            isFavourite={caregiver.isFavorite}
          />
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        {caregiver.isVerified ? <VerifiedBadge /> : null}
        <RatingStars
          averageRating={caregiver.averageRating}
          reviewCount={caregiver.reviewCount}
        />
      </div>

      {caregiver.bio ? (
        <p className="line-clamp-3 text-sm text-text-secondary">{caregiver.bio}</p>
      ) : null}

      {caregiver.certificationCodes.length > 0 ? (
        <ul className="flex flex-wrap gap-2">
          {caregiver.certificationCodes.slice(0, 4).map((code) => (
            <li key={code}>
              <Chip>{humanizeCode(code)}</Chip>
            </li>
          ))}
          {caregiver.certificationCodes.length > 4 ? (
            <li>
              <Chip>+{caregiver.certificationCodes.length - 4} more</Chip>
            </li>
          ) : null}
        </ul>
      ) : null}

      <div className="mt-auto flex items-end justify-between gap-3 border-t border-border pt-4">
        <p className="text-sm text-text-secondary">
          {caregiver.yearsOfExperience}{" "}
          {caregiver.yearsOfExperience === 1 ? "year" : "years"} of experience
        </p>
        <p className="font-heading text-lg font-medium text-text-primary">
          ${caregiver.hourlyRate.toFixed(2)}
          <span className="text-sm font-normal text-text-secondary">/hr</span>
        </p>
      </div>
    </article>
  );
}
