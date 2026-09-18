import type { Metadata } from "next";
import Link from "next/link";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { EmptyState } from "@/components/ui/empty-state";
import { listBookings } from "@/features/bookings/api";
import { WorkCard } from "@/features/caregivers/components/work-card";
import { caregiverNav } from "@/features/caregivers/nav";
import type { BookingStatus } from "@/features/bookings/types";

export const metadata: Metadata = {
  title: "Work",
  description: "Requests, confirmed work and past jobs.",
};

/**
 * Everything this caregiver has been asked to do — wireframe `148:3`'s list view.
 *
 * Three tabs rather than the requester's two: a caregiver has a decision to make on
 * pending requests, and burying those among confirmed work would cost them jobs.
 */
const TABS = [
  { key: "requests", label: "Requests" },
  { key: "upcoming", label: "Upcoming" },
  { key: "past", label: "Past" },
] as const;

type TabKey = (typeof TABS)[number]["key"];

const FINISHED: BookingStatus[] = ["Declined", "Cancelled", "Completed", "Disputed"];

export default async function CaregiverBookingsPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const raw = Array.isArray(params.tab) ? params.tab[0] : params.tab;
  const tab: TabKey = raw === "past" ? "past" : raw === "upcoming" ? "upcoming" : "requests";

  const all = await listBookings();

  // Reading the clock during render is unstable in a client component, which is what the
  // purity rule guards against. This is a Server Component fetching with no-store, so it
  // renders once per request and the timestamp is simply "when this page was asked for".
  // eslint-disable-next-line react-hooks/purity
  const now = Date.now();

  const bookings = all.filter((booking) => {
    if (tab === "requests") {
      return booking.status === "Pending";
    }

    const isFinished =
      FINISHED.includes(booking.status) || new Date(booking.endAtUtc).getTime() < now;

    // In-progress work stays under "upcoming" past its end time — it still needs a
    // check-out before it is done.
    return tab === "past"
      ? isFinished && booking.status !== "InProgress"
      : !isFinished || booking.status === "InProgress";
  });

  const pendingCount = all.filter((booking) => booking.status === "Pending").length;

  return (
    <DashboardShell items={caregiverNav} currentPath="/caregiver/bookings" heading="Work">
      <nav aria-label="Work filter" className="mb-6 flex flex-wrap gap-2">
        {TABS.map((option) => {
          const isActive = option.key === tab;

          return (
            <Link
              key={option.key}
              href={
                option.key === "requests"
                  ? "/caregiver/bookings"
                  : `/caregiver/bookings?tab=${option.key}`
              }
              aria-current={isActive ? "page" : undefined}
              className={`rounded-pill px-4 py-2 font-control text-sm transition-colors ${
                isActive
                  ? "bg-brand-primary text-text-on-brand"
                  : "bg-surface text-text-secondary hover:text-text-primary"
              }`}
            >
              {option.label}
              {option.key === "requests" && pendingCount > 0 ? (
                <span
                  className={`ml-2 rounded-pill px-1.5 py-0.5 text-xs font-semibold ${
                    isActive ? "bg-text-on-brand/20" : "bg-brand-accent/20 text-text-primary"
                  }`}
                >
                  {pendingCount}
                  <span className="sr-only"> waiting</span>
                </span>
              ) : null}
            </Link>
          );
        })}
      </nav>

      {bookings.length === 0 ? (
        <EmptyState
          title={emptyTitle(tab)}
          description={emptyDescription(tab)}
        />
      ) : (
        <div className="@container">
          <ul className="grid gap-4 @xl:grid-cols-2 @5xl:grid-cols-3">
            {bookings.map((booking) => (
              <li key={booking.id} className="flex">
                <WorkCard booking={booking} />
              </li>
            ))}
          </ul>
        </div>
      )}
    </DashboardShell>
  );
}

function emptyTitle(tab: TabKey): string {
  if (tab === "requests") return "No requests waiting";
  if (tab === "past") return "No past work yet";
  return "Nothing coming up";
}

function emptyDescription(tab: TabKey): string {
  if (tab === "requests") {
    return "When a family asks you to care for someone, their request appears here for you to accept or decline.";
  }

  if (tab === "past") {
    return "Finished, declined and cancelled work is kept here.";
  }

  return "Work you have accepted shows up here with the address and who you will be caring for.";
}
