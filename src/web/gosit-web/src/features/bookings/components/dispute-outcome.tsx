import type { Booking } from "@/features/bookings/types";

/**
 * How staff settled a dispute on this booking.
 *
 * One component rendered on both the family's and the caregiver's page, showing the same
 * words to each. Two versions of the same decision, differing by audience, would be worse
 * than showing nothing — the point of a settlement is that both people can see it is the
 * same settlement.
 *
 * This is also the only place the decision can be read. The notification that announces it
 * says a decision was made and where to look, because it travels by email and a dispute is
 * about something that happened in a family's home.
 */
export function DisputeOutcome({ booking }: { booking: Booking }) {
  if (!booking.disputeResolutionNotes || !booking.disputeResolvedAtUtc) {
    return null;
  }

  const settled = new Date(booking.disputeResolvedAtUtc).toLocaleDateString("en-CA", {
    year: "numeric",
    month: "long",
    day: "numeric",
  });

  return (
    <section className="rounded-card border border-border bg-surface p-5">
      <h2 className="font-heading text-base font-medium text-text-primary">
        How this dispute was settled
      </h2>

      <p className="mt-1 text-xs text-text-secondary">Decided {settled} by GoSit.</p>

      {/* The staff member's words, unedited. Paraphrasing a decision about money is how
          two people end up arguing about what they were each told. */}
      <p className="mt-3 whitespace-pre-line text-sm text-text-primary">
        {booking.disputeResolutionNotes}
      </p>

      <p className="mt-3 text-sm text-text-secondary">
        Any refund and any change to the caregiver&rsquo;s payment are shown in the cost
        below.
      </p>
    </section>
  );
}
