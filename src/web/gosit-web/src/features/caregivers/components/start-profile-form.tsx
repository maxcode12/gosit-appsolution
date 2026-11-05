"use client";

import { useActionState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { startCaregiverProfileAction, type CaregiverFormState } from "@/features/caregivers/actions";

/**
 * The first thing a new caregiver does: two answers, and everything else opens up.
 *
 * Their documents, checks, insurance, services and hours cannot be saved until a profile
 * exists, and the full profile form asks for an address and more. This asks only for what a
 * profile needs to exist, so someone can start uploading their documents straight away.
 * The page re-renders on save, and the steps that were waiting on it become available.
 */
export function StartProfileForm() {
  const [state, formAction] = useActionState<CaregiverFormState, FormData>(
    startCaregiverProfileAction,
    {},
  );

  return (
    <form
      id="start-profile"
      action={formAction}
      noValidate
      className="scroll-mt-24 flex flex-col gap-4 rounded-card border border-brand-accent/40 bg-brand-accent/5 p-5"
    >
      <div>
        <h2 className="font-heading text-lg font-medium text-text-primary">Start your profile</h2>
        <p className="mt-1 text-sm text-text-secondary">
          Two quick answers, then you can upload your documents and start your checks. You can add
          your address, services and hours afterwards.
        </p>
      </div>

      {state.error ? (
        <p role="alert" className="rounded-control bg-red-50 px-4 py-3 text-sm text-red-800">
          {state.error}
        </p>
      ) : null}

      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label htmlFor="hourlyRate" className="font-control text-sm text-text-secondary">
            Your hourly rate (CAD)
          </label>
          <input
            id="hourlyRate"
            name="hourlyRate"
            type="number"
            inputMode="decimal"
            min="17.60"
            step="0.01"
            required
            placeholder="25.00"
            className="mt-1 h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          />
          {state.fieldErrors?.hourlyRate ? (
            <p className="mt-1 text-sm text-red-700">{state.fieldErrors.hourlyRate}</p>
          ) : (
            <p className="mt-1 text-xs text-text-muted">Families see this. You can change it any time.</p>
          )}
        </div>

        <div>
          <label htmlFor="yearsOfExperience" className="font-control text-sm text-text-secondary">
            Years of experience
          </label>
          <input
            id="yearsOfExperience"
            name="yearsOfExperience"
            type="number"
            inputMode="numeric"
            min="0"
            step="1"
            required
            defaultValue={0}
            className="mt-1 h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          />
          {state.fieldErrors?.yearsOfExperience ? (
            <p className="mt-1 text-sm text-red-700">{state.fieldErrors.yearsOfExperience}</p>
          ) : null}
        </div>
      </div>

      <div>
        <SubmitButton />
      </div>
    </form>
  );
}

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" disabled={pending}>
      {pending ? "Saving…" : "Save and continue"}
    </Button>
  );
}
