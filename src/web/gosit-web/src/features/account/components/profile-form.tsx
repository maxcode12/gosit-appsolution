"use client";

import { useActionState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { saveProfileAction, type AccountFormState } from "@/features/account/actions";
import type { CareRequesterProfile } from "@/features/bookings/types";

/**
 * Contact details and the address where care happens.
 *
 * One form for both, because they are saved by one API call and because splitting them
 * would mean asking someone to press save twice to become bookable.
 */
export function ProfileForm({ profile }: { profile: CareRequesterProfile | null }) {
  const [state, formAction] = useActionState<AccountFormState, FormData>(
    saveProfileAction,
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
        <p role="status" className="rounded-control bg-brand-primary/10 px-4 py-3 text-sm text-brand-primary">
          Saved.
        </p>
      ) : null}

      <Field label="Phone number" name="phoneNumber" error={state.fieldErrors?.phoneNumber}>
        <input
          id="phoneNumber"
          name="phoneNumber"
          type="tel"
          autoComplete="tel"
          required
          defaultValue={profile?.phoneNumber ?? ""}
          placeholder="416 555 0134"
          className={inputClassName}
        />
        <p className="mt-1 text-sm text-text-secondary">
          Shared with a caregiver only once you have a confirmed booking together.
        </p>
      </Field>

      <Field label="About your family" name="aboutMe">
        <textarea
          id="aboutMe"
          name="aboutMe"
          rows={3}
          maxLength={2000}
          defaultValue={profile?.aboutMe ?? ""}
          placeholder="A sentence or two so a caregiver knows who they would be helping."
          className="w-full rounded-control border border-border bg-surface p-3 font-control text-sm text-text-primary"
        />
      </Field>

      <fieldset className="flex flex-col gap-4 border-t border-border pt-5">
        <legend className="font-heading text-base font-medium text-text-primary">
          Where care happens
        </legend>

        <p className="text-sm text-text-secondary">
          Your address stays private until a caregiver confirms a booking with you.
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
      </fieldset>

      <SaveButton />
    </form>
  );
}

const inputClassName =
  "h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary";

function SaveButton() {
  const { pending } = useFormStatus();

  return (
    <div>
      <Button type="submit" disabled={pending}>
        {pending ? "Saving…" : "Save details"}
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
