import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { CalendarIcon, PersonIcon, PinIcon } from "@/components/ui/icons";
import { formatAddress } from "@/features/bookings/address";
import { getBooking } from "@/features/bookings/api";
import { DisputeOutcome } from "@/features/bookings/components/dispute-outcome";
import {
  BookingStatusBadge,
  formatDuration,
  formatMoney,
  formatWindow,
} from "@/features/bookings/components/booking-status";
import {
  getBookingCareNeeds,
  getBookingEmergencyContacts,
} from "@/features/caregivers/api";
import { CareNeedsPanel } from "@/features/caregivers/components/care-needs-panel";
import { PetCareNotesPanel } from "@/features/caregivers/components/pet-care-notes-panel";
import { EmergencyContactsPanel } from "@/features/caregivers/components/emergency-contacts-panel";
import { WorkActions } from "@/features/caregivers/components/work-actions";
import { caregiverNav } from "@/features/caregivers/nav";
import { ApiError } from "@/lib/api/client";
import { describePet, type Booking, type BookingStatus } from "@/features/bookings/types";

/**
 * A static title. The family's name and what the job is do not belong in browser
 * history or a tab strip.
 */
export const metadata: Metadata = {
  title: "Job",
};

/**
 * One job, from the caregiver's side.
 *
 * The screen that finally closes the gap this product was missing: once a booking is
 * accepted, the caregiver can read the care needs recorded for whoever they are looking
 * after. Before acceptance the API discloses nothing, so this page explains that rather
 * than showing an error.
 */
export default async function CaregiverBookingPage({
  params,
}: {
  params: Promise<{ bookingId: string }>;
}) {
  const { bookingId } = await params;

  let booking: Booking;

  try {
    booking = await getBooking(bookingId);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      notFound();
    }

    throw error;
  }

  const family = `${booking.careRequesterFirstName}${
    booking.careRequesterLastInitial ? ` ${booking.careRequesterLastInitial}.` : ""
  }`;

  // Care needs are disclosed only once the job has been taken on. Asking earlier would
  // be refused, so the requests are not even made.
  const isDisclosing = (
    ["Accepted", "InProgress", "Completed", "Disputed"] as BookingStatus[]
  ).includes(booking.status);

  const careNeeds = isDisclosing
    ? (
        await Promise.all(
          booking.recipients
            .filter((recipient) => recipient.hasRecordedCareNeeds)
            .map((recipient) => getBookingCareNeeds(booking.id, recipient.careRecipientId)),
        )
      ).filter((record) => record !== null)
    : [];

  // Gated on the same states, for the same reason: a pending request is not consent to
  // learn a family's phone numbers, so the request is not made.
  const emergencyContacts = isDisclosing
    ? await getBookingEmergencyContacts(booking.id)
    : [];

  return (
    <DashboardShell
      items={caregiverNav}
      currentPath="/caregiver/bookings"
      heading={booking.careServiceName}
      description={`for ${family}`}
      actions={<BookingStatusBadge status={booking.status} />}
    >
      <div className="max-w-2xl">
        <Link
          href="/caregiver/bookings"
          className="font-control text-sm text-text-secondary hover:text-text-primary"
        >
          ← All work
        </Link>

        <div className="mt-4 flex flex-col gap-4">
          <WorkActions booking={booking} />

          <DisputeOutcome booking={booking} />

          <section className="rounded-card border border-border bg-surface p-5">
            <h2 className="font-heading text-base font-medium text-text-primary">The job</h2>

            <dl className="mt-3 flex flex-col gap-3 text-sm">
              <Row icon={<CalendarIcon />} label="When">
                {formatWindow(booking)}
                <span className="text-text-secondary"> · {formatDuration(booking)}</span>
              </Row>

              <Row icon={<PinIcon />} label="Where">
                {/*
                  The full address once accepted, because a caregiver has to get there.
                  Before that, only the area: a request is not consent to learn where a
                  family lives, and the API does not send it.
                */}
                {booking.address ? (
                  <>
                    {formatAddress(booking.address)}
                    {booking.careLocation === "CaregiverHome" && !booking.householdDetailsDisclosed ? (
                      <span className="block text-text-secondary">
                        Your home. {booking.careRequesterFirstName} sees this address, and you
                        see their notes, once you accept.
                      </span>
                    ) : null}
                  </>
                ) : (
                  <>
                    {booking.area.city}, {booking.area.province} {booking.area.postalCodePrefix}
                    <span className="block text-text-secondary">
                      The exact address, and any notes from {booking.careRequesterFirstName},
                      are shared when you accept.
                    </span>
                  </>
                )}
              </Row>

              <Row icon={<PersonIcon />} label={booking.pets.length > 0 ? "Pets" : "Who"}>
                {booking.pets.length > 0
                  ? booking.pets.map(describePet).join(", ")
                  : booking.recipients
                      .map((r) => `${r.firstName} (${r.age}, ${r.recipientType.toLowerCase()})`)
                      .join(", ")}
              </Row>
            </dl>

            {booking.instructions ? (
              <div className="mt-4 border-t border-border pt-4">
                <h3 className="font-control text-sm text-text-secondary">
                  Notes from {booking.careRequesterFirstName}
                </h3>
                <p className="mt-1 whitespace-pre-wrap text-sm text-text-primary">
                  {booking.instructions}
                </p>
              </div>
            ) : null}
          </section>

          <Pay booking={booking} />

          {careNeeds.map((record) => (
            <CareNeedsPanel key={record.careRecipientId} record={record} />
          ))}

          <PetCareNotesPanel pets={booking.pets} />

          <EmergencyContactsPanel contacts={emergencyContacts} />

          {!isDisclosing && booking.recipients.some((r) => r.hasRecordedCareNeeds) ? (
            <p className="rounded-card bg-surface-muted px-5 py-4 text-sm text-text-primary">
              {family} has recorded care needs for this booking. You will be able to read
              them as soon as you accept.
            </p>
          ) : null}
        </div>
      </div>
    </DashboardShell>
  );
}

/**
 * What the job pays.
 *
 * Shows the booking total and says plainly that a platform fee comes off it, because
 * the exact fee is only known once the payout is created — it is on the statement, and
 * inventing a figure here would be worse than pointing there.
 */
function Pay({ booking }: { booking: Booking }) {
  return (
    <section className="rounded-card border border-border bg-surface p-5">
      <h2 className="font-heading text-base font-medium text-text-primary">Pay</h2>

      <dl className="mt-3 flex flex-col gap-2 text-sm">
        <div className="flex justify-between">
          <dt className="text-text-secondary">Your rate</dt>
          <dd className="text-text-primary">
            {booking.pricingUnit === "Nightly" && booking.nightlyRate !== null
              ? `${formatMoney(booking.nightlyRate)} / night`
              : `${formatMoney(booking.hourlyRate)} / hour`}
          </dd>
        </div>

        <div className="flex justify-between">
          <dt className="text-text-secondary">Booked</dt>
          <dd className="text-text-primary">{formatMoney(booking.estimatedTotal)}</dd>
        </div>

        {booking.actualTotal !== null ? (
          <div className="flex justify-between border-t border-border pt-2">
            <dt className="font-control text-text-primary">
              {booking.pricingUnit === "Nightly" ? "For the nights stayed" : "For the time you worked"}
            </dt>
            <dd className="font-heading text-lg font-medium text-text-primary">
              {formatMoney(booking.actualTotal)}
            </dd>
          </div>
        ) : null}
      </dl>

      <p className="mt-3 text-sm text-text-secondary">
        GoSit&apos;s fee comes off this before payout.{" "}
        <Link href="/caregiver/earnings" className="text-brand-primary hover:underline">
          Your statement
        </Link>{" "}
        shows the exact split for each job.
      </p>
    </section>
  );
}

function Row({
  icon,
  label,
  children,
}: {
  icon: React.ReactNode;
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex gap-3">
      <dt className="flex items-center gap-2 text-text-secondary">
        <span aria-hidden>{icon}</span>
        <span className="sr-only">{label}</span>
      </dt>
      <dd className="text-text-primary">{children}</dd>
    </div>
  );
}
