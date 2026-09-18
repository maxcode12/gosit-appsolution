"use client";

import { useState, useTransition } from "react";
import { Button } from "@/components/ui/button";
import { setAcceptingWorkAction } from "@/features/caregivers/actions";
import type { CaregiverProfile } from "@/features/caregivers/types";

/**
 * Open or closed to new requests.
 *
 * The asymmetry is deliberate and matches the domain: opening requires verification,
 * closing never does. Someone who needs to stop taking work — illness, an emergency —
 * must be able to do it in one press without meeting a condition first.
 */
export function AcceptingWorkToggle({ profile }: { profile: CaregiverProfile }) {
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  const canOpen = profile.verificationStatus === "Verified";

  return (
    <div className="rounded-card border border-border bg-surface p-5">
      <h2 className="font-heading text-base font-medium text-text-primary">
        {profile.acceptingWork ? "Open to new requests" : "Not taking new requests"}
      </h2>

      <p className="mt-1 text-sm text-text-secondary">
        {profile.acceptingWork
          ? "Families can find you and ask you to care for someone. Existing bookings are unaffected if you close."
          : canOpen
            ? "You will not appear in search until you open up."
            : "You can open up once your checks are cleared."}
      </p>

      {error ? (
        <p role="alert" className="mt-2 text-sm text-red-700">
          {error}
        </p>
      ) : null}

      <Button
        type="button"
        variant={profile.acceptingWork ? "ghost" : "primary"}
        disabled={isPending || (!profile.acceptingWork && !canOpen)}
        className="mt-4"
        onClick={() =>
          startTransition(async () => {
            setError(null);

            const result = await setAcceptingWorkAction(!profile.acceptingWork);

            if (result.error) {
              setError(result.error);
            }
          })
        }
      >
        {isPending
          ? "Saving…"
          : profile.acceptingWork
            ? "Stop taking requests"
            : "Start taking requests"}
      </Button>
    </div>
  );
}
