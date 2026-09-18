import Link from "next/link";
import { CalendarIcon, PersonIcon, PinIcon } from "@/components/ui/icons";
import {
  BookingStatusBadge,
  formatMoney,
  formatWindow,
} from "@/features/bookings/components/booking-status";
import { careForSummary, type Booking } from "@/features/bookings/types";

/**
 * One booking in a list.
 *
 * Shows the recipients by first name and age only — that is all the list contract
 * carries, and all a summary needs. Whether care needs are recorded is surfaced, but
 * never what they say.
 */
export function BookingCard({ booking }: { booking: Booking }) {
  const caregiver = `${booking.caregiverFirstName}${
    booking.caregiverLastInitial ? ` ${booking.caregiverLastInitial}.` : ""
  }`;

  return (
    <article className="relative flex flex-col gap-3 rounded-card border border-border bg-surface p-5 transition-shadow focus-within:shadow-md hover:shadow-md">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="font-heading text-lg font-medium text-text-primary">
            <Link
              href={`/bookings/${booking.id}`}
              className="after:absolute after:inset-0 after:content-[''] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
            >
              {booking.careServiceName}
            </Link>
          </h3>
          <p className="mt-0.5 text-sm text-text-secondary">with {caregiver}</p>
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
            {booking.area.city}, {booking.area.province}
          </dd>
        </div>
      </dl>

      <div className="mt-auto flex items-end justify-between gap-3 border-t border-border pt-3">
        <p className="text-sm text-text-secondary">
          {/* Once care has happened the real figure replaces the estimate. */}
          {booking.actualTotal === null ? "Estimated" : "Total"}
        </p>
        <p className="font-heading text-lg font-medium text-text-primary">
          {formatMoney(booking.actualTotal ?? booking.estimatedTotal)}
        </p>
      </div>
    </article>
  );
}
