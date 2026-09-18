"use client";

import { useActionState, useState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { setAvailabilityAction, type CaregiverFormState } from "@/features/caregivers/actions";
import type { AvailabilityWindow } from "@/features/caregivers/types";

const DAYS = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
] as const;

/**
 * The hours this caregiver is usually free.
 *
 * One row per weekday with a single window, which covers what almost everyone actually
 * works. The API's model allows several windows in a day; the form does not offer that
 * yet, and it says so rather than silently discarding the extras when someone who has
 * them saves.
 */
export function AvailabilityForm({ availability }: { availability: AvailabilityWindow[] }) {
  const [state, formAction] = useActionState<CaregiverFormState, FormData>(
    setAvailabilityAction,
    {},
  );

  // Existing windows keyed by day. Where a day has more than one, the first is shown.
  const existing = new Map(availability.map((window) => [window.day, window]));
  const hasMultiple = availability.length > new Set(availability.map((w) => w.day)).size;

  return (
    <form action={formAction} className="flex flex-col gap-5">
      {state.error ? (
        <p role="alert" className="rounded-control bg-red-50 px-4 py-3 text-sm text-red-800">
          {state.error}
        </p>
      ) : null}

      {state.saved ? (
        <p
          role="status"
          className="rounded-control bg-brand-primary/10 px-4 py-3 text-sm text-brand-primary"
        >
          Saved.
        </p>
      ) : null}

      {hasMultiple ? (
        <p className="rounded-control bg-brand-accent/10 px-4 py-3 text-sm text-text-primary">
          You have more than one window on some days. This form shows the first of each,
          and saving will replace the rest — leave it alone until split days are
          supported here.
        </p>
      ) : null}

      <ul className="flex flex-col gap-2">
        {DAYS.map((day) => (
          <DayRow key={day} day={day} window={existing.get(day)} />
        ))}
      </ul>

      <p className="text-sm text-text-secondary">
        A guide, not a commitment. Families see these hours when deciding whether to ask
        you, and you can still decline any request.
      </p>

      <SaveButton />
    </form>
  );
}

function DayRow({ day, window }: { day: string; window: AvailabilityWindow | undefined }) {
  const [enabled, setEnabled] = useState(window !== undefined);

  /*
   * Held in state only so the overnight note below can react to it. An end time earlier
   * in the day than the start is now a legitimate window that runs past midnight, and a
   * caregiver typing one has no way to tell whether they have said "overnight" or made a
   * typo unless the form says which.
   */
  const [startTime, setStartTime] = useState(window ? window.startTime.slice(0, 5) : "09:00");
  const [endTime, setEndTime] = useState(window ? window.endTime.slice(0, 5) : "17:00");

  const crossesMidnight = endTime <= startTime && endTime !== startTime;

  return (
    <li className="flex flex-wrap items-center gap-3 rounded-control border border-border p-3">
      {/* Posted for every day so the action can pair a day with its times by name. */}
      <input type="hidden" name="day" value={day} />

      <label className="flex w-32 cursor-pointer items-center gap-3">
        <input
          type="checkbox"
          name="enabled"
          value={day}
          checked={enabled}
          onChange={(event) => setEnabled(event.target.checked)}
          className="size-4 accent-[var(--color-brand-primary)]"
        />
        <span className="text-text-primary">{day}</span>
      </label>

      {enabled ? (
        <div className="flex items-center gap-2">
          <input
            type="time"
            name={`startTime-${day}`}
            value={startTime}
            onChange={(event) => setStartTime(event.target.value)}
            aria-label={`${day} start time`}
            className="h-11 rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          />
          <span className="text-text-secondary">to</span>
          <input
            type="time"
            name={`endTime-${day}`}
            value={endTime}
            onChange={(event) => setEndTime(event.target.value)}
            aria-label={`${day} end time`}
            className="h-11 rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          />
        </div>
      ) : (
        <span className="text-sm text-text-secondary">Not available</span>
      )}

      {enabled && crossesMidnight ? (
        <p className="w-full text-sm text-text-secondary">
          Overnight — this runs through the night and ends the next morning.
        </p>
      ) : null}
    </li>
  );
}

function SaveButton() {
  const { pending } = useFormStatus();

  return (
    <div>
      <Button type="submit" disabled={pending}>
        {pending ? "Saving…" : "Save availability"}
      </Button>
    </div>
  );
}
