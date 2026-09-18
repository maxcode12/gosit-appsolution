import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { CalendarIcon, PersonIcon, PinIcon } from "@/components/ui/icons";
import { formatAddress } from "@/features/bookings/address";
import { getBooking, getPaymentReceipt } from "@/features/bookings/api";
import { BookingActions } from "@/features/bookings/components/booking-actions";
import { DisputeOutcome } from "@/features/bookings/components/dispute-outcome";
import {
  BookingStatusBadge,
  formatDuration,
  formatMoney,
  formatWindow,
} from "@/features/bookings/components/booking-status";
import { careRequesterNav } from "@/features/search/nav";
import { ApiError } from "@/lib/api/client";
import { env } from "@/lib/env";
import { describePet, arrivalNote } from "@/features/bookings/types";
import type { Booking, BookingStatus, PaymentReceipt } from "@/features/bookings/types";

/**
 * One booking, in full.
 *
 * Designed here rather than taken from a wireframe — no node covers it. The order is
 * what someone actually wants to know, in order: what is happening now, when and where,
 * who it is for, what it costs, and what they can do about it.
 */
export const metadata: Metadata = {
  title: "Booking",
};

export default async function BookingDetailPage({
  params,
}: {
  params: Promise<{ bookingId: string }>;
}) {
  const { bookingId } = await params;

  let booking: Booking;

  try {
    booking = await getBooking(bookingId);
  } catch (error) {
    // The API answers 404 rather than 403 for someone else's booking, so ids cannot be
    // probed. Both land here, which is the point.
    if (error instanceof ApiError && error.status === 404) {
      notFound();
    }

    throw error;
  }

  const receipt = await getPaymentReceipt(bookingId);

  const caregiver = `${booking.caregiverFirstName}${
    booking.caregiverLastInitial ? ` ${booking.caregiverLastInitial}.` : ""
  }`;


  return (
    <DashboardShell
      items={careRequesterNav}
      currentPath="/bookings"
      heading={booking.careServiceName}
      description={`with ${caregiver}`}
      actions={<BookingStatusBadge status={booking.status} />}
    >
      <div className="max-w-2xl">
        <Link
          href="/bookings"
          className="font-control text-sm text-text-secondary hover:text-text-primary"
        >
          ← All bookings
        </Link>

        <div className="mt-4 flex flex-col gap-4">
          <StatusNote booking={booking} caregiver={caregiver} />
          <DisputeOutcome booking={booking} />

          <section className="rounded-card border border-border bg-surface p-5">
            <h2 className="font-heading text-base font-medium text-text-primary">Details</h2>

            <dl className="mt-3 flex flex-col gap-3 text-sm">
              <Row icon={<CalendarIcon />} label="When">
                {formatWindow(booking)}
                <span className="text-text-secondary"> · {formatDuration(booking)}</span>
              </Row>

              <Row icon={<PinIcon />} label="Where">
                {booking.address ? (
                  formatAddress(booking.address)
                ) : (
                  <>
                    {/*
                      Only boarding reaches here: the address is the caregiver's home, and a
                      family sees it once the caregiver accepts.
                    */}
                    At {booking.caregiverFirstName}&apos;s home in {booking.area.city},{" "}
                    {booking.area.province}
                    <span className="block text-text-secondary">
                      Their address is shared with you when they accept.
                    </span>
                  </>
                )}
              </Row>

              <Row icon={<PersonIcon />} label={booking.pets.length > 0 ? "Pets" : "Who"}>
                {booking.pets.length > 0 ? (
                  <ul className="flex flex-col gap-0.5">
                    {booking.pets.map((pet) => (
                      <li key={pet.petId}>{describePet(pet)}</li>
                    ))}
                  </ul>
                ) : null}
                <ul className="flex flex-col gap-0.5">
                  {booking.recipients.map((recipient) => (
                    <li key={recipient.careRecipientId}>
                      {recipient.firstName}
                      <span className="text-text-secondary">
                        {" "}
                        · {recipient.age} · {recipient.recipientType}
                      </span>
                      {/*
                        Whether needs are on file, never what they say. Deliberately not
                        "shared with <caregiver>": no endpoint discloses care needs to a
                        caregiver yet, so that would be a false claim about who has read
                        a child's private record.
                      */}
                      {recipient.hasRecordedCareNeeds ? (
                        <span className="text-text-secondary"> · care needs on file</span>
                      ) : null}
                    </li>
                  ))}
                </ul>
              </Row>
            </dl>

            {booking.instructions ? (
              <div className="mt-4 border-t border-border pt-4">
                <h3 className="font-control text-sm text-text-secondary">Your notes</h3>
                <p className="mt-1 whitespace-pre-wrap text-sm text-text-primary">
                  {booking.instructions}
                </p>
              </div>
            ) : null}
          </section>

          <Cost booking={booking} receipt={receipt} />

          <BookingActions
            booking={booking}
            hasPaid={receipt?.status === "Succeeded"}
            stripePublishableKey={env.stripePublishableKey}
          />
        </div>
      </div>
    </DashboardShell>
  );
}

/**
 * A sentence about what is happening and what happens next.
 *
 * Statuses that speak for themselves get no line rather than a filler one.
 */
function StatusNote({ booking, caregiver }: { booking: Booking; caregiver: string }) {
  const checkedInAt = booking.checkedInAtUtc
    ? new Date(booking.checkedInAtUtc).toLocaleTimeString("en-CA", {
        hour: "numeric",
        minute: "2-digit",
      })
    : null;

  const notes: Partial<Record<BookingStatus, string>> = {
    Pending: `${caregiver} has been asked and will reply. Nothing is charged until they confirm.`,
    Accepted: `${caregiver} has confirmed. They will check in when they arrive.`,
    Declined: `${caregiver} could not take this one. Plenty of others can.`,
    InProgress: checkedInAt
      ? `${caregiver} checked in at ${checkedInAt}.`
      : `${caregiver} has started.`,
    Completed: booking.wasAutoCompleted
      ? "This booking was closed automatically because no one checked out, so the estimated hours were used. Tell us if that is wrong."
      : "Care is finished. You can leave a review below.",
    Cancelled: booking.isLateCancellation
      ? "Cancelled within 24 hours of the start."
      : "This booking was cancelled.",
    // A settled dispute keeps the Disputed status, so this must not keep promising that
    // someone is still looking at it — the decision itself is shown by DisputeOutcome.
    Disputed: booking.disputeResolvedAtUtc
      ? "This dispute has been settled."
      : "You have raised a dispute. Our team is looking into it.",
  };

  const note = notes[booking.status];

  if (!note) {
    return null;
  }

  /*
   * Whether the caregiver was at the address, shown only once they have checked in and
   * only when it says something. A booking still waiting on a reply has nothing to
   * report, and a phone that could not fix a location is not news — see arrivalNote.
   */
  const arrival =
    booking.checkedInAtUtc === null ? null : arrivalNote(booking.checkInVerification);

  return (
    <div className="rounded-card bg-surface-muted px-5 py-4">
      <p className="text-sm text-text-primary">{note}</p>
      {arrival ? <p className="mt-1 text-sm text-text-secondary">{arrival}</p> : null}
    </div>
  );
}

/**
 * What this costs, and what has actually been taken.
 *
 * Both figures are the server's. Once care has happened the actual total is shown beside
 * the booked estimate rather than replacing it — seeing the two together is how someone
 * checks a bill.
 */
function Cost({ booking, receipt }: { booking: Booking; receipt: PaymentReceipt | null }) {
  return (
    <section className="rounded-card border border-border bg-surface p-5">
      <h2 className="font-heading text-base font-medium text-text-primary">Cost</h2>

      <dl className="mt-3 flex flex-col gap-2 text-sm">
        <div className="flex justify-between">
          <dt className="text-text-secondary">Rate</dt>
          <dd className="text-text-primary">
            {booking.pricingUnit === "Nightly" && booking.nightlyRate !== null
              ? `${formatMoney(booking.nightlyRate)} / night`
              : `${formatMoney(booking.hourlyRate)} / hour`}
          </dd>
        </div>

        <div className="flex justify-between">
          <dt className="text-text-secondary">
            {booking.actualTotal === null ? "Estimated total" : "Booked"}
          </dt>
          <dd className="text-text-primary">{formatMoney(booking.estimatedTotal)}</dd>
        </div>

        {booking.actualTotal !== null ? (
          <div className="flex justify-between border-t border-border pt-2">
            <dt className="font-control text-text-primary">
              {booking.pricingUnit === "Nightly" ? "Total for nights stayed" : "Total for time worked"}
            </dt>
            <dd className="font-heading text-lg font-medium text-text-primary">
              {formatMoney(booking.actualTotal)}
            </dd>
          </div>
        ) : null}

        {receipt ? (
          <>
            {/*
              Only a succeeded payment is called "paid". An intent that exists but has
              not been confirmed is money nobody has taken, and labelling it as paid
              would be the single most misleading thing this page could say.
            */}
            <div className="flex justify-between border-t border-border pt-2">
              <dt className="text-text-secondary">
                {receipt.status === "Succeeded" ? "Paid" : "Charged"}
              </dt>
              <dd className="text-text-primary">
                {receipt.status === "Succeeded"
                  ? `${formatMoney(receipt.amount)}${
                      receipt.succeededAtUtc
                        ? ` on ${new Date(receipt.succeededAtUtc).toLocaleDateString("en-CA", {
                            day: "numeric",
                            month: "short",
                            year: "numeric",
                          })}`
                        : ""
                    }`
                  : receipt.status === "Failed"
                    ? "Could not be taken"
                    : "Not taken yet"}
              </dd>
            </div>

            {receipt.refundedAmount > 0 ? (
              <div className="flex justify-between">
                <dt className="text-text-secondary">Refunded</dt>
                <dd className="text-text-primary">−{formatMoney(receipt.refundedAmount)}</dd>
              </div>
            ) : null}
          </>
        ) : null}
      </dl>
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
