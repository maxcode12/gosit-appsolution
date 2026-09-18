"use client";

import { useActionState, useState, useTransition } from "react";
import { useFormStatus } from "react-dom";
import { Button, type ButtonVariant } from "@/components/ui/button";
import {
  acceptBookingAction,
  checkInAction,
  checkOutAction,
  declineBookingAction,
  type CaregiverFormState,
} from "@/features/caregivers/actions";
import type { Booking } from "@/features/bookings/types";

/**
 * What a caregiver can do to a job right now.
 *
 * The available actions come from the booking's status, mirroring the server's state
 * machine. The server re-checks everything; this only avoids offering a button that
 * would be refused.
 */
export function WorkActions({ booking }: { booking: Booking }) {
  if (booking.status === "Pending") {
    return <RespondToRequest booking={booking} />;
  }

  if (booking.status === "Accepted") {
    return (
      <Transition
        bookingId={booking.id}
        action={checkInAction}
        idle="I have arrived — check in"
        busy="Checking in…"
        title="Starting the job"
        description="Check in when you arrive. This starts the clock, and it is what the family sees to know you are there."
      />
    );
  }

  if (booking.status === "InProgress") {
    return (
      <Transition
        bookingId={booking.id}
        action={checkOutAction}
        idle="Finish and check out"
        busy="Checking out…"
        variant="secondary"
        title="Finishing the job"
        description="Check out when you leave. The family is charged for the time you actually worked, rounded up to the nearest fifteen minutes."
      />
    );
  }

  return null;
}

/** Accept or decline, with declining behind a confirmation and an optional reason. */
function RespondToRequest({ booking }: { booking: Booking }) {
  const [isPending, startTransition] = useTransition();
  const [isDeclining, setIsDeclining] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [declineState, declineAction] = useActionState<CaregiverFormState, FormData>(
    declineBookingAction.bind(null, booking.id),
    {},
  );

  const family = booking.careRequesterFirstName;

  if (isDeclining) {
    return (
      <form action={declineAction} className="rounded-card border border-border bg-surface p-5">
        <h2 className="font-heading text-base font-medium text-text-primary">
          Decline this request?
        </h2>
        <p className="mt-1 text-sm text-text-secondary">
          {family} will be told and can look for someone else. A short reason helps them,
          but it is up to you.
        </p>

        <textarea
          name="reason"
          rows={2}
          maxLength={500}
          placeholder="Reason (optional)"
          aria-label="Reason for declining"
          className="mt-4 w-full rounded-control border border-border bg-surface p-3 font-control text-sm text-text-primary"
        />

        {declineState.error ? (
          <p role="alert" className="mt-2 text-sm text-red-700">
            {declineState.error}
          </p>
        ) : null}

        <div className="mt-4 flex gap-2">
          <PendingButton idle="Yes, decline" busy="Declining…" variant="ghost" />
          <Button type="button" variant="ghost" onClick={() => setIsDeclining(false)}>
            Go back
          </Button>
        </div>
      </form>
    );
  }

  return (
    <div className="rounded-card border border-border bg-surface p-5">
      <h2 className="font-heading text-base font-medium text-text-primary">
        {family} is asking you to take this on
      </h2>
      <p className="mt-1 text-sm text-text-secondary">
        Accepting shares your reply with {family} and gives you the care needs they have
        recorded for whoever you will be looking after.
      </p>

      {error ? (
        <p role="alert" className="mt-2 text-sm text-red-700">
          {error}
        </p>
      ) : null}

      <div className="mt-4 flex flex-wrap gap-2">
        <Button
          type="button"
          disabled={isPending}
          onClick={() =>
            startTransition(async () => {
              setError(null);

              const result = await acceptBookingAction(booking.id);

              if (result.error) {
                setError(result.error);
              }
            })
          }
        >
          {isPending ? "Accepting…" : "Accept"}
        </Button>

        <Button type="button" variant="ghost" onClick={() => setIsDeclining(true)}>
          Decline
        </Button>
      </div>
    </div>
  );
}

/** A single-button lifecycle step: check in, check out. */
function Transition({
  bookingId,
  action,
  idle,
  busy,
  title,
  description,
  variant = "primary",
}: {
  bookingId: string;
  action: (
    bookingId: string,
    previous: CaregiverFormState,
    formData: FormData,
  ) => Promise<CaregiverFormState>;
  idle: string;
  busy: string;
  title: string;
  description: string;
  variant?: ButtonVariant;
}) {
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  return (
    <div className="rounded-card border border-border bg-surface p-5">
      <h2 className="font-heading text-base font-medium text-text-primary">{title}</h2>
      <p className="mt-1 text-sm text-text-secondary">{description}</p>

      {error ? (
        <p role="alert" className="mt-2 text-sm text-red-700">
          {error}
        </p>
      ) : null}

      <Button
        type="button"
        variant={variant}
        disabled={isPending}
        className="mt-4"
        onClick={() =>
          startTransition(async () => {
            setError(null);

            const formData = new FormData();
            const fix = await currentPosition();

            if (fix) {
              formData.set("latitude", String(fix.latitude));
              formData.set("longitude", String(fix.longitude));
            }

            const result = await action(bookingId, {}, formData);

            if (result.error) {
              setError(result.error);
            }
          })
        }
      >
        {isPending ? busy : idle}
      </Button>
    </div>
  );
}

function PendingButton({
  idle,
  busy,
  variant = "primary",
}: {
  idle: string;
  busy: string;
  variant?: ButtonVariant;
}) {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" variant={variant} disabled={pending}>
      {pending ? busy : idle}
    </Button>
  );
}

/**
 * The device's location, if it will give one quickly.
 *
 * Never rejects and never blocks. Every failure — permission declined, no GPS fix,
 * plain HTTP where the API does not exist, a device that simply takes too long —
 * resolves to undefined, and the check-in proceeds without a location.
 *
 * That is the whole design, and it is deliberate: a caregiver standing at a family's
 * door must be able to start work. Making the location a precondition would strand
 * exactly the people with the oldest phones and the least patience for permission
 * dialogs, and it would do so at the worst possible moment.
 *
 * The eight-second cap exists for the same reason. A browser will happily wait far
 * longer for a fix it is never going to get, and a button that hangs is a button someone
 * presses again.
 */
async function currentPosition(): Promise<{ latitude: number; longitude: number } | undefined> {
  if (typeof navigator === "undefined" || !navigator.geolocation) {
    return undefined;
  }

  return new Promise((resolve) => {
    navigator.geolocation.getCurrentPosition(
      (position) =>
        resolve({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
        }),
      () => resolve(undefined),
      { enableHighAccuracy: true, timeout: 8000, maximumAge: 60_000 },
    );
  });
}
