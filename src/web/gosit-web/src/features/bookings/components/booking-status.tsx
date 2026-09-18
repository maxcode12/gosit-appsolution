import type { Booking, BookingStatus } from "@/features/bookings/types";

/**
 * How each status reads to a person.
 *
 * The wording matters: "Awaiting reply" tells a requester what is happening, where
 * "Pending" only names an internal state. Tone is neutral for outcomes that are nobody's
 * fault — a declined booking is not an error.
 */
const STATUS_LABELS: Record<BookingStatus, string> = {
  Pending: "Awaiting reply",
  Accepted: "Confirmed",
  Declined: "Declined",
  Cancelled: "Cancelled",
  InProgress: "Care in progress",
  Completed: "Completed",
  Disputed: "Disputed",
};

const STATUS_STYLES: Record<BookingStatus, string> = {
  Pending: "bg-brand-accent/15 text-text-primary",
  Accepted: "bg-brand-primary/10 text-brand-primary",
  Declined: "bg-surface-muted text-text-secondary",
  Cancelled: "bg-surface-muted text-text-secondary",
  InProgress: "bg-brand-primary text-text-on-brand",
  Completed: "bg-brand-secondary/15 text-text-primary",
  Disputed: "bg-danger-accent/10 text-danger-accent",
};

export function BookingStatusBadge({ status }: { status: BookingStatus }) {
  return (
    <span
      className={`inline-flex items-center rounded-pill px-3 py-1 text-xs font-semibold ${STATUS_STYLES[status]}`}
    >
      {STATUS_LABELS[status]}
    </span>
  );
}

export function statusLabel(status: BookingStatus): string {
  return STATUS_LABELS[status];
}

/**
 * Formats the booking window as one line.
 *
 * Times are rendered in the reader's own zone. The API stores UTC, and the launch market
 * is Ontario, so for now those agree — but formatting from the instant rather than from
 * a stored local string keeps that true when they stop agreeing.
 */
export function formatWindow(booking: Booking): string {
  const start = new Date(booking.startAtUtc);
  const end = new Date(booking.endAtUtc);

  const date = start.toLocaleDateString("en-CA", {
    weekday: "short",
    day: "numeric",
    month: "short",
    year: "numeric",
  });

  const time = (value: Date) =>
    value.toLocaleTimeString("en-CA", { hour: "numeric", minute: "2-digit" });

  // A stay, or care running past midnight, needs the end date too: "5:00 p.m. – 10:00 a.m."
  // does not say which morning.
  if (start.toDateString() !== end.toDateString()) {
    const endDate = end.toLocaleDateString("en-CA", {
      weekday: "short",
      day: "numeric",
      month: "short",
    });

    return `${date}, ${time(start)} – ${endDate}, ${time(end)}`;
  }

  return `${date}, ${time(start)} – ${time(end)}`;
}

export function formatDuration(booking: Booking): string {
  if (booking.pricingUnit === "Nightly" && booking.nights !== null) {
    return `${booking.nights} ${booking.nights === 1 ? "night" : "nights"}`;
  }

  const hours =
    (new Date(booking.endAtUtc).getTime() - new Date(booking.startAtUtc).getTime()) /
    3_600_000;

  return `${Number.isInteger(hours) ? hours : hours.toFixed(1)} ${hours === 1 ? "hour" : "hours"}`;
}

export function formatMoney(amount: number): string {
  return amount.toLocaleString("en-CA", { style: "currency", currency: "CAD" });
}
