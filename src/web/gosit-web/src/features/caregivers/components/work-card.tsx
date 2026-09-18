import Link from "next/link";
import { CalendarIcon, PersonIcon, PinIcon } from "@/components/ui/icons";
import {
  BookingStatusBadge,
  formatMoney,
  formatWindow,
} from "@/features/bookings/components/booking-status";
import { careForSummary, type Booking } from "@/features/bookings/types";

/**
 * One job, from the caregiver's side.
 *
 * The mirror of the requester's booking card, and different in the two ways that matter:
 * it names the family rather than the caregiver, and the money it leads with is what
 * they will be paid rather than what will be charged.
 */
export function WorkCard({ booking }: { booking: Booking }) {
  const family = `${booking.careRequesterFirstName}${
    booking.careRequesterLastInitial ? ` ${booking.careRequesterLastInitial}.` : ""
  }`;

  return (
    <article className="relative flex flex-col gap-3 rounded-card border border-border bg-surface p-5 transition-shadow focus-within:shadow-md hover:shadow-md">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="font-heading text-lg font-medium text-text-primary">
            <Link
              href={`/caregiver/bookings/${booking.id}`}
              className="after:absolute after:inset-0 after:content-[''] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
            >
              {booking.careServiceName}
            </Link>
          </h3>
          <p className="mt-0.5 text-sm text-text-secondary">for {family}</p>
        </div>

        <BookingStatusBadge status={booking.status} />
      </div>

      <dl className="flex flex-col gap-1.5 text-sm text-text-secondary">
        <div className="flex items-center gap-2">
          <dt className="sr-only">When</dt>
          <CalendarIcon />
          <dd>{formatWindow(booking)}</dd>
        </div>

        <div className="flex items-center gap-2">
          <dt className="sr-only">Who</dt>
          <PersonIcon />
          <dd>
            {careForSummary(booking)}
          </dd>
        </div>

        <div className="flex items-center gap-2">
          <dt className="sr-only">Where</dt>
          <PinIcon />
          <dd>
            {/*
              The area, never the address. Until a request is accepted the API does not
              send the address at all, and a list does not need it either way.
            */}
            {booking.area.city}, {booking.area.province}
          </dd>
        </div>
      </dl>

      <div className="mt-auto flex items-end justify-between gap-3 border-t border-border pt-3">
        {/*
          The booking total, not take-home — the platform fee comes off it, and only the
          statement knows the rate. Calling this "earned" would overstate what lands in
          someone's account, so it says what it actually is.
        */}
        <p className="text-sm text-text-secondary">
          {booking.actualTotal === null ? "Job total, before fee" : "Job total"}
        </p>
        <p className="font-heading text-lg font-medium text-text-primary">
          {formatMoney(booking.actualTotal ?? booking.estimatedTotal)}
        </p>
      </div>
    </article>
  );
}
