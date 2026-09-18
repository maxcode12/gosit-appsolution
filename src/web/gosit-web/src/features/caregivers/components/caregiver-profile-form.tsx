"use client";

import { useActionState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import {
  saveCaregiverProfileAction,
  type CaregiverFormState,
} from "@/features/caregivers/actions";
import type { CaregiverProfile } from "@/features/caregivers/types";

const inputClassName =
  "h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary";

/**
 * Rate, experience, introduction and the area worked.
 *
 * The address here is the caregiver's own, used to place them in search and to measure
 * the travel radius. It is never shown to families — search returns an approximate
 * location only — and the form says so, because being asked for a home address without
 * being told why is a fair reason to abandon a signup.
 */
export function CaregiverProfileForm({ profile }: { profile: CaregiverProfile | null }) {
  const [state, formAction] = useActionState<CaregiverFormState, FormData>(
    saveCaregiverProfileAction,
    {},
  );

  const address = profile?.address;

  return (
    <form action={formAction} className="flex flex-col gap-5" noValidate>
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

      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Your hourly rate (CAD)" name="hourlyRate" error={state.fieldErrors?.hourlyRate}>
          <input
            id="hourlyRate"
            name="hourlyRate"
            type="number"
            min="1"
            step="0.01"
            required
            defaultValue={profile?.hourlyRate ?? ""}
            className={inputClassName}
          />
        </Field>

        <Field
          label="Nightly rate for overnight pet care (CAD, optional)"
          name="nightlyRate"
          error={state.fieldErrors?.nightlyRate}
        >
          <input
            id="nightlyRate"
            name="nightlyRate"
            type="number"
            min="10"
            max="500"
            step="0.01"
            placeholder="Boarding and in-home sitting"
            defaultValue={profile?.nightlyRate ?? ""}
            className={inputClassName}
          />
        </Field>

        <Field
          label="Years of experience"
          name="yearsOfExperience"
          error={state.fieldErrors?.yearsOfExperience}
        >
          <input
            id="yearsOfExperience"
            name="yearsOfExperience"
            type="number"
            min="0"
            max="70"
            step="1"
            required
            defaultValue={profile?.yearsOfExperience ?? 0}
            className={inputClassName}
          />
        </Field>
      </div>

      <Field label="Introduce yourself" name="bio">
        <textarea
          id="bio"
          name="bio"
          rows={5}
          maxLength={2000}
          defaultValue={profile?.bio ?? ""}
          placeholder="What kind of care you do, who you have worked with, and what a family can expect from you."
          className="w-full rounded-control border border-border bg-surface p-3 font-control text-sm text-text-primary"
        />
        <p className="mt-1 text-sm text-text-secondary">
          This is the first thing a family reads about you.
        </p>
      </Field>

      <fieldset className="flex flex-col gap-4 border-t border-border pt-5">
        <legend className="font-heading text-base font-medium text-text-primary">
          Where you work
        </legend>

        <p className="text-sm text-text-secondary">
          Used to match you with families nearby. Your exact address is never shown —
          searches only see the area you work in.
        </p>

        <Field label="Street address" name="line1" error={state.fieldErrors?.line1}>
          <input
            id="line1"
            name="line1"
            autoComplete="address-line1"
            required
            defaultValue={address?.line1 ?? ""}
            className={inputClassName}
          />
        </Field>

        <Field label="Apartment, unit (optional)" name="line2">
          <input
            id="line2"
            name="line2"
            autoComplete="address-line2"
            defaultValue={address?.line2 ?? ""}
            className={inputClassName}
          />
        </Field>

        <div className="grid gap-4 sm:grid-cols-3">
          <Field label="City" name="city" error={state.fieldErrors?.city}>
            <input
              id="city"
              name="city"
              autoComplete="address-level2"
              required
              defaultValue={address?.city ?? ""}
              className={inputClassName}
            />
          </Field>

          <Field label="Province" name="province" error={state.fieldErrors?.province}>
            <input
              id="province"
              name="province"
              autoComplete="address-level1"
              required
              maxLength={2}
              defaultValue={address?.province ?? "ON"}
              className={`${inputClassName} uppercase`}
            />
          </Field>

          <Field label="Postal code" name="postalCode" error={state.fieldErrors?.postalCode}>
            <input
              id="postalCode"
              name="postalCode"
              autoComplete="postal-code"
              required
              maxLength={7}
              defaultValue={
                address?.postalCode
                  ? `${address.postalCode.slice(0, 3)} ${address.postalCode.slice(3)}`
                  : ""
              }
              placeholder="M5S 1M2"
              className={`${inputClassName} uppercase`}
            />
          </Field>
        </div>

        <Field
          label="How far will you travel? (km)"
          name="serviceRadiusKm"
          error={state.fieldErrors?.serviceRadiusKm}
        >
          <input
            id="serviceRadiusKm"
            name="serviceRadiusKm"
            type="number"
            min="1"
            max="200"
            step="1"
            required
            defaultValue={profile?.serviceRadiusKm ?? 15}
            className={inputClassName}
          />
        </Field>
      </fieldset>

      <SaveButton />
    </form>
  );
}

function SaveButton() {
  const { pending } = useFormStatus();

  return (
    <div>
      <Button type="submit" disabled={pending}>
        {pending ? "Saving…" : "Save profile"}
      </Button>
    </div>
  );
}

function Field({
  label,
  name,
  error,
  children,
}: {
  label: string;
  name: string;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <div>
      <label htmlFor={name} className="font-control text-sm text-text-secondary">
        {label}
      </label>
      <div className="mt-1">{children}</div>
      {error ? <p className="mt-1 text-sm text-red-700">{error}</p> : null}
    </div>
  );
}
