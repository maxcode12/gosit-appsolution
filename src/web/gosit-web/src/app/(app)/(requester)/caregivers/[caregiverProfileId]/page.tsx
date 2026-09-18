import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Chip, RatingStars, VerifiedBadge, humanizeCode } from "@/components/ui/badges";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { buttonClassName } from "@/components/ui/button";
import { PinIcon } from "@/components/ui/icons";
import { getMyProfile } from "@/features/bookings/api";
import { FavouriteButton } from "@/features/favorites/components/favourite-button";
import { RequestInterview } from "@/features/interviews/components/request-interview";
import { StartConversation } from "@/features/messaging/components/start-conversation";
import { getCaregiverReviews } from "@/features/reviews/api";
import { ReviewList } from "@/features/reviews/components/review-list";
import { careRequesterNav } from "@/features/search/nav";
import { getCaregiverDetail } from "@/features/search/api";
import { ApiError } from "@/lib/api/client";
import type { AvailabilityWindow, CaregiverDetail } from "@/features/search/types";

export const metadata: Metadata = {
  title: "Caregiver profile",
};

/**
 * A caregiver's public profile.
 *
 * No wireframe exists for this screen, so the layout is designed here, following the
 * visual language of the rest of the app. What it shows is constrained by the API: a
 * first name and last initial, an approximate location, currently-valid certifications,
 * and a weekly availability summary. There is no exact address or contact detail in the
 * payload to render.
 */
export default async function CaregiverProfilePage({
  params,
}: {
  params: Promise<{ caregiverProfileId: string }>;
}) {
  const { caregiverProfileId } = await params;

  let caregiver: CaregiverDetail;

  try {
    caregiver = await getCaregiverDetail(caregiverProfileId);
  } catch (error) {
    // The API returns 404 for a caregiver who is not listable — unverified, delisted or
    // suspended — as well as one that does not exist. Both are "not found" here.
    if (error instanceof ApiError && error.status === 404) {
      notFound();
    }

    throw error;
  }

  // Both actions in the sidebar need a care-requester profile to exist. Checking here
  // means the action that cannot work is never offered as though it could.
  const [profile, reviews] = await Promise.all([
    getMyProfile(),
    getCaregiverReviews(caregiverProfileId),
  ]);

  const name = `${caregiver.firstName}${caregiver.lastInitial ? ` ${caregiver.lastInitial}.` : ""}`;

  return (
    <DashboardShell
      items={careRequesterNav}
      currentPath="/dashboard"
      heading={name}
      description={caregiver.approximateLocation}
      actions={
        <FavouriteButton
          caregiverProfileId={caregiver.caregiverProfileId}
          caregiverName={name}
          isFavourite={caregiver.isFavorite}
        />
      }
    >
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="flex flex-col gap-6">
          <section className="rounded-card border border-border bg-surface p-6">
            <div className="flex flex-wrap items-center gap-3">
              {caregiver.isVerified ? <VerifiedBadge /> : null}
              <RatingStars
                averageRating={caregiver.averageRating}
                reviewCount={caregiver.reviewCount}
              />
            </div>

            {caregiver.bio ? (
              <p className="mt-4 whitespace-pre-line text-text-secondary">{caregiver.bio}</p>
            ) : (
              <p className="mt-4 text-text-secondary">
                This caregiver has not added an introduction yet.
              </p>
            )}

            <dl className="mt-6 grid grid-cols-2 gap-4 border-t border-border pt-6 sm:grid-cols-3">
              <Stat label="Experience" value={`${caregiver.yearsOfExperience} yrs`} />
              <Stat label="Hourly rate" value={`${caregiver.hourlyRate.toFixed(2)}`} />
              {caregiver.nightlyRate !== null ? (
                <Stat label="Overnight pet care, per night" value={`${caregiver.nightlyRate.toFixed(2)}`} />
              ) : null}
              <Stat label="Travels up to" value={`${caregiver.serviceRadiusKm} km`} />
            </dl>
          </section>

          <section className="rounded-card border border-border bg-surface p-6">
            <h2 className="font-heading text-lg font-medium text-text-primary">
              Services offered
            </h2>

            {caregiver.services.length === 0 ? (
              <p className="mt-3 text-text-secondary">No services listed yet.</p>
            ) : (
              <ul className="mt-4 flex flex-wrap gap-2">
                {caregiver.services.map((service) => (
                  <li key={service.code}>
                    <Chip>{service.name}</Chip>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="rounded-card border border-border bg-surface p-6">
            <h2 className="font-heading text-lg font-medium text-text-primary">
              Verified certifications
            </h2>
            <p className="mt-1 text-sm text-text-secondary">
              Only certifications GoSit has checked and that have not expired.
            </p>

            {caregiver.certificationCodes.length === 0 ? (
              <p className="mt-3 text-text-secondary">None recorded.</p>
            ) : (
              <ul className="mt-4 flex flex-wrap gap-2">
                {caregiver.certificationCodes.map((code) => (
                  <li key={code}>
                    <Chip>{humanizeCode(code)}</Chip>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section>
            <h2 className="font-heading text-lg font-semibold text-text-primary">
              What families say
            </h2>
            <p className="mt-1 text-sm text-text-secondary">
              Written after care has happened, by families who booked {caregiver.firstName}.
              Neither side sees the other&apos;s review until both are in.
            </p>

            <div className="mt-4">
              <ReviewList
                reviews={reviews}
                emptyMessage={`${caregiver.firstName} has not been reviewed yet. Someone has to be first — and every review here comes from a real, completed booking.`}
              />
            </div>
          </section>
        </div>

        <aside className="flex flex-col gap-6">
          {/*
            The two things anyone reads a profile in order to do. Booking is the primary
            action; asking a question first is the common and reasonable alternative, so
            it sits beside it rather than being buried.
          */}
          <section className="flex flex-col gap-3 rounded-card border border-border bg-surface p-6">
            <Link
              href={`/caregivers/${caregiver.caregiverProfileId}/book`}
              className={buttonClassName("primary", "w-full")}
            >
              Request a booking
            </Link>

            <StartConversation
              caregiverProfileId={caregiver.caregiverProfileId}
              caregiverName={caregiver.firstName}
              hasProfile={profile !== null}
            />

            {/*
              Meeting on video before booking is what most families want and what most
              of them would otherwise do off the platform. Offered here, on GoSit,
              recorded, so it never has to be.
            */}
            <RequestInterview
              caregiverProfileId={caregiver.caregiverProfileId}
              caregiverName={caregiver.firstName}
              hasProfile={profile !== null}
              compact
            />
          </section>

          <section className="rounded-card border border-border bg-surface p-6">
            <h2 className="font-heading text-lg font-medium text-text-primary">
              Weekly availability
            </h2>

            {caregiver.availability.length === 0 ? (
              <p className="mt-3 text-text-secondary">No availability set.</p>
            ) : (
              <ul className="mt-4 flex flex-col gap-2 text-sm">
                {groupByDay(caregiver.availability).map(([day, windows]) => (
                  <li key={day} className="flex justify-between gap-4">
                    <span className="text-text-secondary">{day}</span>
                    <span className="text-right text-text-primary">
                      {windows.map((w) => `${short(w.startTime)}–${short(w.endTime)}`).join(", ")}
                    </span>
                  </li>
                ))}
              </ul>
            )}

            <p className="mt-4 flex items-start gap-1.5 text-sm text-text-secondary">
              <PinIcon />
              {caregiver.approximateLocation}
            </p>
          </section>
        </aside>
      </div>
    </DashboardShell>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-sm text-text-secondary">{label}</dt>
      <dd className="mt-0.5 font-heading text-lg font-medium text-text-primary">{value}</dd>
    </div>
  );
}

/** Groups windows by weekday, keeping the API's ordering. */
function groupByDay(availability: AvailabilityWindow[]): [string, AvailabilityWindow[]][] {
  const byDay = new Map<string, AvailabilityWindow[]>();

  for (const window of availability) {
    byDay.set(window.day, [...(byDay.get(window.day) ?? []), window]);
  }

  return [...byDay.entries()];
}

/** "09:00:00" to "09:00". Seconds are noise in a schedule. */
function short(time: string): string {
  return time.slice(0, 5);
}
