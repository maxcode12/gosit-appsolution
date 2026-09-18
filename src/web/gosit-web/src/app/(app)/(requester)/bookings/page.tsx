import type { Metadata } from "next";
import Link from "next/link";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { buttonClassName } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { listBookings } from "@/features/bookings/api";
import { BookingCard } from "@/features/bookings/components/booking-card";
import { careRequesterNav } from "@/features/search/nav";
import type { BookingStatus } from "@/features/bookings/types";

export const metadata: Metadata = {
  title: "Bookings",
  description: "Your care bookings.",
};

/**
 * The requester's bookings.
 *
 * No wireframe exists for this screen, so it is designed here in the app's visual
 * language. Two tabs rather than a status dropdown: what people actually want is
 * "what is coming up" and "what has happened", and a dropdown of seven internal states
 * would ask them to learn the state machine.
 */
const TABS = [
  { key: "upcoming", label: "Upcoming" },
  { key: "past", label: "Past" },
] as const;

type TabKey = (typeof TABS)[number]["key"];

/** Statuses that belong in the past view even if their window has not passed. */
const FINISHED: BookingStatus[] = ["Declined", "Cancelled", "Completed", "Disputed"];

export default async function BookingsPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const raw = Array.isArray(params.tab) ? params.tab[0] : params.tab;
  const tab: TabKey = raw === "past" ? "past" : "upcoming";

  // The API's upcomingOnly filters by end time, which is not quite the same question:
  // a booking cancelled for tomorrow is not "upcoming" to a person. So everything is
  // fetched and split here on both the window and the status.
  const all = await listBookings();

  // Reading the clock during render is unstable in a client component, which is what the
  // purity rule guards against. This is a Server Component fetching with no-store, so it
  // renders once per request and the timestamp is simply "when this page was asked for".
  // eslint-disable-next-line react-hooks/purity
  const now = Date.now();

  const bookings = all.filter((booking) => {
    const isFinished =
      FINISHED.includes(booking.status) || new Date(booking.endAtUtc).getTime() < now;

    return tab === "past" ? isFinished : !isFinished;
  });

  return (
    <DashboardShell
      items={careRequesterNav}
      currentPath="/bookings"
      heading="Bookings"
      actions={
        <Link href="/dashboard" className={buttonClassName()}>
          Book care
        </Link>
      }
    >
      <nav aria-label="Booking filter" className="mb-6 flex gap-2">
        {TABS.map((option) => {
          const isActive = option.key === tab;

          return (
            <Link
              key={option.key}
              href={option.key === "upcoming" ? "/bookings" : "/bookings?tab=past"}
              aria-current={isActive ? "page" : undefined}
              className={`rounded-pill px-4 py-2 font-control text-sm transition-colors ${
                isActive
                  ? "bg-brand-primary text-text-on-brand"
                  : "bg-surface text-text-secondary hover:text-text-primary"
              }`}
            >
              {option.label}
            </Link>
          );
        })}
      </nav>

      {bookings.length === 0 ? (
        <EmptyState
          title={tab === "past" ? "No past bookings" : "No bookings yet"}
          description={
            tab === "past"
              ? "Bookings appear here once they are finished, declined or cancelled."
              : "Find a caregiver you like and request the times you need. They will confirm before anything is charged."
          }
          action={
            tab === "upcoming" ? (
              <Link href="/dashboard" className={buttonClassName()}>
                Find care
              </Link>
            ) : undefined
          }
        />
      ) : (
        <div className="@container">
          <ul className="grid gap-4 @xl:grid-cols-2 @5xl:grid-cols-3">
            {bookings.map((booking) => (
              <li key={booking.id} className="flex">
                <BookingCard booking={booking} />
              </li>
            ))}
          </ul>
        </div>
      )}
    </DashboardShell>
  );
}
