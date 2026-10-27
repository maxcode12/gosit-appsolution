import type { Metadata } from "next";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { CaregiverCard } from "@/features/search/components/caregiver-card";
import { SearchFilters } from "@/features/search/components/search-filters";
import { careRequesterNav } from "@/features/search/nav";
import { getCareServiceCatalogue, searchCaregivers } from "@/features/search/api";
import type { CaregiverSearchFilters, CaregiverSearchResponse } from "@/features/search/types";
import { getMySubscription } from "@/features/subscriptions/api";
import { SubscriptionFeature } from "@/features/subscriptions/types";

export const metadata: Metadata = {
  title: "Find care",
  description: "Search verified caregivers near you.",
};

/**
 * The care requester's main screen: filters beside a list of caregivers.
 *
 * Follows the wireframes' dashboard layout — a navigation rail beside a grid of
 * caregiver cards — with the card and filter designs worked out here, since the
 * wireframe shows them at thumbnail fidelity only.
 *
 * Filters come from the URL, so a search is shareable and the back button works.
 */
export default async function DashboardPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const filters = toFilters(params);

  // The catalogue is anonymous and cached; the other two are per-user and are not.
  const [catalogue, results, subscription] = await Promise.all([
    getCareServiceCatalogue(),
    searchCaregivers(filters),
    getMySubscription(),
  ]);

  // Read from the plan the API reports, never inferred from the plan code. The server
  // refuses a gated filter regardless; this only avoids offering an input that errors.
  const canUseAdvancedFilters = subscription.features.includes(
    SubscriptionFeature.AdvancedSearchFilters,
  );

  return (
    <DashboardShell
      items={careRequesterNav}
      currentPath="/dashboard"
      heading="Find care"
      description={
        results.totalMatches === 1
          ? "1 caregiver matches your search."
          : `${results.totalMatches} caregivers match your search.`
      }
    >
      <div className="grid gap-6 lg:grid-cols-[18rem_minmax(0,1fr)]">
        <SearchFilters
          catalogue={catalogue}
          canUseAdvancedFilters={canUseAdvancedFilters}
          className="lg:sticky lg:top-6 lg:self-start"
        />

        <div className="@container">
          {/*
            Explains why results are not nearest-first. Without this, ordering that
            ignores distance looks broken rather than unavailable.

            The wording follows the reason, because for two of the three there is
            nothing the reader can do — and this used to tell someone who had already
            saved an address to go and add one, which sends them hunting through their
            profile for a field that is already filled in.
          */}
          {results.results.length > 0 ? (
            <DistanceNotice reason={results.distanceUnavailableReason} />
          ) : null}

          {results.results.length === 0 ? (
            <EmptyState
              title="No caregivers matched"
              description="Try widening the filters — a higher hourly rate, fewer years of experience, or a nearby city often helps."
              action={
                <Link href="/dashboard" className={buttonClassName("ghost")}>
                  Clear filters
                </Link>
              }
            />
          ) : (
            <ul className="grid gap-4 @xl:grid-cols-2 @5xl:grid-cols-3">
              {results.results.map((caregiver) => (
                <li key={caregiver.caregiverProfileId} className="flex">
                  <CaregiverCard caregiver={caregiver} />
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </DashboardShell>
  );
}

/**
 * Reads filters from the query string.
 *
 * Anything unparseable is dropped rather than passed on: the API validates too, but a
 * bad value in a shared link should narrow nothing instead of returning an error page.
 */
function toFilters(params: Record<string, string | string[] | undefined>): CaregiverSearchFilters {
  const single = (key: string) => {
    const value = params[key];
    return Array.isArray(value) ? value[0] : value;
  };

  const number = (key: string) => {
    const parsed = Number(single(key));
    return Number.isFinite(parsed) ? parsed : undefined;
  };

  return {
    serviceCode: single("serviceCode"),
    city: single("city"),
    postalCode: single("postalCode"),
    radiusKm: number("radiusKm"),
    maxHourlyRate: number("maxHourlyRate"),
    minYearsOfExperience: number("minYearsOfExperience"),
    minRating: number("minRating"),
    page: number("page") ?? 1,
  };
}

/**
 * Why results are not sorted by how near someone is.
 *
 * One of these is advice and two are apologies, which is the whole reason the API
 * reports a cause rather than a boolean. Silence would be worse than any of them —
 * ordering that ignores distance reads as broken rather than unavailable.
 */
function DistanceNotice({
  reason,
}: {
  reason: CaregiverSearchResponse["distanceUnavailableReason"];
}) {
  if (reason === null) {
    return null;
  }

  const body =
    reason === "NoAddress"
      ? "Add an address to your profile to sort caregivers by how near they are."
      : reason === "AddressNotLocated"
        ? "We could not place your address on a map yet, so these are not sorted by distance. Your address is saved — there is nothing to fix."
        : "None of these caregivers have been placed on a map yet, so these are not sorted by distance.";

  return (
    <p className="mb-4 rounded-control bg-surface px-4 py-3 text-sm text-text-secondary">
      {body}
    </p>
  );
}
