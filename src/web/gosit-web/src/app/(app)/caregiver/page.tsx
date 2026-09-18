import type { Metadata } from "next";
import Link from "next/link";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { buttonClassName } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { listBookings } from "@/features/bookings/api";
import { formatMoney } from "@/features/bookings/components/booking-status";
import { getEarnings, getMyCaregiverProfile, getMyVerifications } from "@/features/caregivers/api";
import { ListingStatus } from "@/features/caregivers/components/listing-status";
import { WorkCard } from "@/features/caregivers/components/work-card";
import { caregiverNav } from "@/features/caregivers/nav";
import type { Booking } from "@/features/bookings/types";

export const metadata: Metadata = {
  title: "Today",
  description: "Your requests, upcoming work and earnings.",
};

/**
 * The caregiver's home screen — wireframe `148:3`.
 *
 * Ordered by what needs a decision soonest: requests waiting on a reply first, because
 * a family is waiting; then confirmed work; then money, which can wait. Whether they are
 * visible to families sits above all of it, since nothing else matters if the answer
 * is no.
 */
export default async function CaregiverTodayPage() {
  const profile = await getMyCaregiverProfile();

  if (!profile) {
    return (
      <DashboardShell items={caregiverNav} currentPath="/caregiver" heading="Welcome to GoSit">
        <div className="max-w-2xl">
          <EmptyState
            title="Set up your profile"
            description="Tell families what you charge, where you work and what care you offer. Once your checks are done you will start appearing in search."
            action={
              <Link href="/caregiver/account" className={buttonClassName()}>
                Get started
              </Link>
            }
          />
        </div>
      </DashboardShell>
    );
  }

  const [bookings, earnings, verifications] = await Promise.all([
    listBookings(),
    getEarnings(),
    getMyVerifications(),
  ]);

  const pending = bookings.filter((b) => b.status === "Pending");
  const confirmed = upcomingWork(bookings);

  return (
    <DashboardShell
      items={caregiverNav}
      currentPath="/caregiver"
      heading="Today"
      description={
        pending.length > 0
          ? `${pending.length} ${pending.length === 1 ? "request is" : "requests are"} waiting on you`
          : "Nothing waiting on you right now."
      }
    >
      <div className="flex flex-col gap-8">
        <ListingStatus profile={profile} verifications={verifications} />

        {earnings ? (
          <section>
            <div className="flex items-baseline justify-between gap-4">
              <h2 className="font-heading text-lg font-medium text-text-primary">Money</h2>
              <Link
                href="/caregiver/earnings"
                className="font-control text-sm text-brand-primary hover:underline"
              >
                See statement
              </Link>
            </div>

            <dl className="mt-4 grid gap-4 sm:grid-cols-3">
              <Stat label="Waiting to be paid out" value={formatMoney(earnings.pendingBalance)} />
              <Stat label="Paid out so far" value={formatMoney(earnings.totalPaid)} />
              <Stat label="Your rate" value={`${formatMoney(profile.hourlyRate)}/hr`} />
            </dl>
          </section>
        ) : null}

        <Section
          heading="Requests"
          description="Families waiting for an answer. Accepting shares their address and the care needs they have recorded."
          bookings={pending}
          emptyTitle="No new requests"
          emptyDescription={
            profile.isListable
              ? "You are visible in search. New requests will land here."
              : "You will start receiving requests once you are visible to families."
          }
        />

        <Section
          heading="Coming up"
          description="Work you have confirmed."
          bookings={confirmed}
          emptyTitle="Nothing booked yet"
          emptyDescription="Confirmed work appears here with the address and who you will be caring for."
        />
      </div>
    </DashboardShell>
  );
}

function Section({
  heading,
  description,
  bookings,
  emptyTitle,
  emptyDescription,
}: {
  heading: string;
  description: string;
  bookings: Booking[];
  emptyTitle: string;
  emptyDescription: string;
}) {
  return (
    <section>
      <h2 className="font-heading text-lg font-medium text-text-primary">{heading}</h2>
      <p className="mt-1 text-sm text-text-secondary">{description}</p>

      <div className="mt-4">
        {bookings.length === 0 ? (
          <EmptyState title={emptyTitle} description={emptyDescription} />
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
      </div>
    </section>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-card border border-border bg-surface p-4">
      <dt className="text-sm text-text-secondary">{label}</dt>
      <dd className="mt-1 font-heading text-xl font-medium text-text-primary">{value}</dd>
    </div>
  );
}

/**
 * Confirmed work that has not finished yet.
 *
 * `InProgress` is included regardless of the clock: a booking someone is standing in the
 * middle of belongs under "coming up" even once its end time has passed, because they
 * still have to check out of it.
 */
function upcomingWork(bookings: Booking[]): Booking[] {
  // Same reasoning as the bookings list: this runs once per request in a Server
  // Component, so the clock read is "when this page was asked for", not a re-render
  // hazard.
  const now = Date.now();

  return bookings.filter(
    (booking) =>
      booking.status === "InProgress"
      || (booking.status === "Accepted" && new Date(booking.endAtUtc).getTime() >= now),
  );
}
