"use client";

import { useActionState, useState, useTransition } from "react";
import { useFormStatus } from "react-dom";
import { Button, type ButtonVariant } from "@/components/ui/button";
import {
  cancelBookingAction,
  startPaymentAction,
  submitReviewAction,
  type BookingFormState,
} from "@/features/bookings/actions";
import { PaymentForm } from "@/features/bookings/components/payment-form";
import type { Booking } from "@/features/bookings/types";

/** A submit button that reports the enclosing form's pending state. */
function PendingButton({
  idle,
  busy,
  variant = "primary",
  className = "",
}: {
  idle: string;
  busy: string;
  variant?: ButtonVariant;
  className?: string;
}) {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" variant={variant} disabled={pending} className={className}>
      {pending ? busy : idle}
    </Button>
  );
}

/**
 * What a requester can do to a booking right now.
 *
 * Which actions exist is decided from the status, mirroring the server's state machine.
 * The server re-checks everything — this only avoids offering a button that would be
 * refused.
 */
export function BookingActions({
  booking,
  hasPaid,
  stripePublishableKey,
}: {
  booking: Booking;
  hasPaid: boolean;
  stripePublishableKey: string | null;
}) {
  const canCancel = booking.status === "Pending" || booking.status === "Accepted";
  const canPay = !hasPaid && (booking.status === "Accepted" || booking.status === "Completed");
  const canReview = booking.status === "Completed" || booking.status === "Disputed";

  if (!canCancel && !canPay && !canReview) {
    return null;
  }

  return (
    <div className="flex flex-col gap-4">
      {canPay ? (
        <PayButton bookingId={booking.id} stripePublishableKey={stripePublishableKey} />
      ) : null}
      {canReview ? <ReviewForm bookingId={booking.id} /> : null}
      {canCancel ? <CancelBooking booking={booking} /> : null}
    </div>
  );
}

/**
 * Starts payment, then mounts Stripe's Payment Element once a client secret exists.
 *
 * Without a configured publishable key this falls back to saying the intent was
 * created and card entry is unavailable — the same honesty the placeholder had, kept
 * for an environment that has not set `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY`.
 */
function PayButton({
  bookingId,
  stripePublishableKey,
}: {
  bookingId: string;
  stripePublishableKey: string | null;
}) {
  const [isPending, startTransition] = useTransition();
  const [clientSecret, setClientSecret] = useState<string | null>(null);
  const [unavailable, setUnavailable] = useState(false);
  const [error, setError] = useState<string | null>(null);

  return (
    <div className="rounded-card border border-border bg-surface p-5">
      <h2 className="font-heading text-base font-medium text-text-primary">Payment</h2>

      {clientSecret && stripePublishableKey ? null : (
        <p className="mt-1 text-sm text-text-secondary">
          You are charged for the time actually worked, never more than the estimate
          without your agreement.
        </p>
      )}

      {unavailable ? (
        <p className="mt-2 text-sm text-text-secondary">
          Payment is ready, but card entry is not connected in this environment. Nothing
          has been charged.
        </p>
      ) : null}

      {error ? (
        <p role="alert" className="mt-2 text-sm text-red-700">
          {error}
        </p>
      ) : null}

      {clientSecret && stripePublishableKey ? (
        <PaymentForm
          publishableKey={stripePublishableKey}
          clientSecret={clientSecret}
          bookingId={bookingId}
        />
      ) : (
        <Button
          type="button"
          variant="secondary"
          disabled={isPending || unavailable}
          className="mt-4"
          onClick={() =>
            startTransition(async () => {
              setError(null);

              const result = await startPaymentAction(bookingId);

              if (result.error) {
                setError(result.error);
                return;
              }

              if (!result.clientSecret || !stripePublishableKey) {
                setUnavailable(true);
                return;
              }

              setClientSecret(result.clientSecret);
            })
          }
        >
          {isPending ? "Preparing…" : "Pay for this booking"}
        </Button>
      )}
    </div>
  );
}

function ReviewForm({ bookingId }: { bookingId: string }) {
  const [state, formAction] = useActionState<BookingFormState, FormData>(
    submitReviewAction.bind(null, bookingId),
    {},
  );

  const [rating, setRating] = useState(5);

  if (state.saved) {
    return (
      <div className="rounded-card border border-border bg-surface p-5">
        <h2 className="font-heading text-base font-medium text-text-primary">Thank you</h2>
        <p className="mt-1 text-sm text-text-secondary">
          Your review stays private until your caregiver has written theirs, or the
          two-week window closes.
        </p>
      </div>
    );
  }

  return (
    <form action={formAction} className="rounded-card border border-border bg-surface p-5">
      <h2 className="font-heading text-base font-medium text-text-primary">
        How did it go?
      </h2>
      <p className="mt-1 text-sm text-text-secondary">
        Neither of you sees the other&apos;s review until both are in, so you can be honest.
      </p>

      <fieldset className="mt-4">
        <legend className="sr-only">Rating</legend>
        <div className="flex gap-2">
          {[1, 2, 3, 4, 5].map((value) => (
            <label
              key={value}
              className={`grid size-11 cursor-pointer place-items-center rounded-control border font-control text-sm transition-colors ${
                rating === value
                  ? "border-brand-primary bg-brand-primary text-text-on-brand"
                  : "border-border text-text-secondary hover:border-brand-primary"
              }`}
            >
              <input
                type="radio"
                name="rating"
                value={value}
                checked={rating === value}
                onChange={() => setRating(value)}
                className="sr-only"
              />
              {value}
            </label>
          ))}
        </div>
      </fieldset>

      <textarea
        name="comment"
        rows={3}
        maxLength={2000}
        placeholder="What went well? Anything the next family should know?"
        aria-label="Review comment"
        className="mt-4 w-full rounded-control border border-border bg-surface p-3 font-control text-sm text-text-primary"
      />

      {state.error ? (
        <p role="alert" className="mt-2 text-sm text-red-700">
          {state.error}
        </p>
      ) : null}

      <PendingButton idle="Submit review" busy="Sending…" className="mt-4" />
    </form>
  );
}

/**
 * Cancels the booking.
 *
 * Behind a confirmation step, and it says plainly when a cancellation counts as late —
 * finding that out afterwards would be the wrong way to learn it.
 */
function CancelBooking({ booking }: { booking: Booking }) {
  const [state, formAction] = useActionState<BookingFormState, FormData>(
    cancelBookingAction.bind(null, booking.id),
    {},
  );

  // Whether this counts as late is decided from the clock, so it is read in the click
  // handler rather than during render — the answer must not change under the reader
  // mid-confirmation, and re-rendering must not change it either.
  const [isLate, setIsLate] = useState<boolean | null>(null);

  if (isLate === null) {
    return (
      <div>
        <Button
          type="button"
          variant="ghost"
          onClick={() =>
            setIsLate(
              new Date(booking.startAtUtc).getTime() - Date.now() < 24 * 3_600_000,
            )
          }
        >
          Cancel this booking
        </Button>
      </div>
    );
  }

  return (
    <form action={formAction} className="rounded-card border border-border bg-surface p-5">
      <h2 className="font-heading text-base font-medium text-text-primary">
        Cancel this booking?
      </h2>

      <p className="mt-1 text-sm text-text-secondary">
        {isLate
          ? "This is within 24 hours of the start, so it counts as a late cancellation. Your caregiver has kept the time free."
          : "Your caregiver will be told. You can book them again any time."}
      </p>

      <textarea
        name="reason"
        rows={2}
        maxLength={500}
        placeholder="Reason (optional)"
        aria-label="Reason for cancelling"
        className="mt-4 w-full rounded-control border border-border bg-surface p-3 font-control text-sm text-text-primary"
      />

      {state.error ? (
        <p role="alert" className="mt-2 text-sm text-red-700">
          {state.error}
        </p>
      ) : null}

      <div className="mt-4 flex gap-2">
        <PendingButton idle="Yes, cancel" busy="Cancelling…" variant="secondary" />
        <Button type="button" variant="ghost" onClick={() => setIsLate(null)}>
          Keep it
        </Button>
      </div>
    </form>
  );
}
