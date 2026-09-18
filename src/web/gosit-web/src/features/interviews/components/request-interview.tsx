"use client";

import Link from "next/link";
import { useActionState, useState } from "react";
import { useFormStatus } from "react-dom";
import { Button, buttonClassName } from "@/components/ui/button";
import { inputClassName } from "@/components/ui/text-input";
import { requestInterviewAction, type InterviewFormState } from "@/features/interviews/actions";
import { DURATION_OPTIONS } from "@/features/interviews/types";

/**
 * Asks a caregiver for a video interview.
 *
 * Says up front that the call is recorded. Someone deciding whether to meet a stranger on
 * camera should know that before they pick a time, not on the day when they join.
 */
export function RequestInterview({
  caregiverProfileId,
  caregiverName,
  hasProfile,
  /** Compact for a sidebar; the conversation page gives it more room. */
  compact = false,
}: {
  caregiverProfileId: string;
  caregiverName: string;
  hasProfile: boolean;
  compact?: boolean;
}) {
  const [isOpen, setIsOpen] = useState(false);

  const [state, formAction] = useActionState<InterviewFormState, FormData>(
    requestInterviewAction.bind(null, caregiverProfileId),
    {},
  );

  if (!hasProfile) {
    return (
      <div>
        <Link href="/account" className={buttonClassName("ghost", "w-full")}>
          Video interview with {caregiverName}
        </Link>
        <p className="mt-2 text-xs text-text-secondary">
          Add your details first so {caregiverName} knows who is asking.
        </p>
      </div>
    );
  }

  if (!isOpen) {
    return (
      <Button
        type="button"
        variant={compact ? "ghost" : "secondary"}
        className={compact ? "w-full" : ""}
        onClick={() => setIsOpen(true)}
      >
        Video interview with {caregiverName}
      </Button>
    );
  }

  // Earliest the API allows is an hour ahead; the picker starts from today so a slip of
  // the finger cannot pick last week.
  const today = new Date().toISOString().slice(0, 10);

  return (
    <form action={formAction} className="flex flex-col gap-3">
      <p className="font-control text-sm font-medium text-text-primary">
        Propose a time to meet {caregiverName} on video
      </p>

      <div className={`grid gap-3 ${compact ? "" : "sm:grid-cols-3"}`}>
        <label className="flex flex-col gap-1.5">
          <span className="font-control text-sm text-text-secondary">Date</span>
          <input type="date" name="date" min={today} required className={inputClassName} />
        </label>

        <label className="flex flex-col gap-1.5">
          <span className="font-control text-sm text-text-secondary">Time</span>
          <input type="time" name="time" step={900} required className={inputClassName} />
        </label>

        <label className="flex flex-col gap-1.5">
          <span className="font-control text-sm text-text-secondary">Length</span>
          <select name="durationMinutes" defaultValue={30} className={inputClassName}>
            {DURATION_OPTIONS.map((minutes) => (
              <option key={minutes} value={minutes}>
                {minutes} minutes
              </option>
            ))}
          </select>
        </label>
      </div>

      {state.error ? (
        <p role="alert" className="text-sm text-danger-accent">
          {state.error}
        </p>
      ) : null}

      <p className="text-xs text-text-secondary">
        Interviews happen on GoSit and are recorded, so there is a record if either of you
        ever raises a concern. Only Trust &amp; Safety staff can watch a recording, and it is
        deleted after 90 days. {caregiverName} can accept or decline; you can both join from
        10 minutes before.
      </p>

      <div className="flex gap-2">
        <SubmitButton />
        <Button type="button" variant="ghost" onClick={() => setIsOpen(false)}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" disabled={pending}>
      {pending ? "Sending…" : "Send request"}
    </Button>
  );
}
