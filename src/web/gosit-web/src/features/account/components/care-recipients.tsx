"use client";

import Link from "next/link";
import { useActionState, useState, useTransition } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import {
  addCareRecipientAction,
  removeCareRecipientAction,
  type AccountFormState,
} from "@/features/account/actions";
import { CARE_RECIPIENT_TYPES } from "@/features/account/types";
import type { CareRecipientSummary } from "@/features/bookings/types";

/**
 * The people care is for.
 *
 * The list shows a name, an age and whether needs are recorded — never the needs. To see
 * or change those you open one recipient, which is one deliberate act per person rather
 * than a page that loads everyone's private detail at once.
 */
export function CareRecipients({ recipients }: { recipients: CareRecipientSummary[] }) {
  const [isAdding, setIsAdding] = useState(recipients.length === 0);

  return (
    <div className="flex flex-col gap-4">
      {recipients.length > 0 ? (
        <ul className="flex flex-col gap-3">
          {recipients.map((recipient) => (
            <li key={recipient.id}>
              <RecipientRow recipient={recipient} />
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-sm text-text-secondary">
          Add the person who needs care. You can book as soon as there is one.
        </p>
      )}

      {isAdding ? (
        <AddRecipientForm onDone={() => setIsAdding(false)} />
      ) : (
        <div>
          <Button type="button" variant="ghost" onClick={() => setIsAdding(true)}>
            Add someone
          </Button>
        </div>
      )}
    </div>
  );
}

function RecipientRow({ recipient }: { recipient: CareRecipientSummary }) {
  const [isPending, startTransition] = useTransition();
  const [isConfirming, setIsConfirming] = useState(false);
  const [error, setError] = useState<string | null>(null);

  return (
    <div className="rounded-card border border-border bg-surface p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-text-primary">
            {recipient.firstName}
            {recipient.lastName ? ` ${recipient.lastName}` : ""}
          </p>
          <p className="text-sm text-text-secondary">
            {recipient.age} · {recipient.recipientType}
            {recipient.hasRecordedCareNeeds ? " · care needs on file" : ""}
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Link
            href={`/account/care-recipients/${recipient.id}`}
            className="font-control text-sm text-brand-primary hover:underline"
          >
            {recipient.hasRecordedCareNeeds ? "Care needs" : "Add care needs"}
          </Link>

          <button
            type="button"
            onClick={() => setIsConfirming(true)}
            className="font-control text-sm text-text-secondary hover:text-text-primary"
          >
            Remove
          </button>
        </div>
      </div>

      {isConfirming ? (
        <div className="mt-3 border-t border-border pt-3">
          <p className="text-sm text-text-primary">
            Remove {recipient.firstName}? Anything recorded about their care goes with
            them. Past bookings are not affected.
          </p>

          {error ? (
            <p role="alert" className="mt-2 text-sm text-red-700">
              {error}
            </p>
          ) : null}

          <div className="mt-3 flex gap-2">
            <Button
              type="button"
              variant="secondary"
              disabled={isPending}
              onClick={() =>
                startTransition(async () => {
                  setError(null);

                  const result = await removeCareRecipientAction(recipient.id);

                  if (result.error) {
                    setError(result.error);
                  }
                })
              }
            >
              {isPending ? "Removing…" : "Yes, remove"}
            </Button>
            <Button type="button" variant="ghost" onClick={() => setIsConfirming(false)}>
              Keep
            </Button>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function AddRecipientForm({ onDone }: { onDone: () => void }) {
  const [state, formAction] = useActionState<AccountFormState, FormData>(
    async (previous, formData) => {
      const result = await addCareRecipientAction(previous, formData);

      if (result.saved) {
        onDone();
      }

      return result;
    },
    {},
  );

  const today = new Date().toISOString().slice(0, 10);

  return (
    <form
      action={formAction}
      className="flex flex-col gap-4 rounded-card border border-border bg-surface p-4"
      noValidate
    >
      <h3 className="font-heading text-base font-medium text-text-primary">
        Who needs care?
      </h3>

      {state.error ? (
        <p role="alert" className="rounded-control bg-red-50 px-4 py-3 text-sm text-red-800">
          {state.error}
        </p>
      ) : null}

      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label htmlFor="firstName" className="font-control text-sm text-text-secondary">
            First name
          </label>
          <input
            id="firstName"
            name="firstName"
            required
            className={`mt-1 ${inputClassName}`}
          />
          {state.fieldErrors?.firstName ? (
            <p className="mt-1 text-sm text-red-700">{state.fieldErrors.firstName}</p>
          ) : null}
        </div>

        <div>
          <label htmlFor="lastName" className="font-control text-sm text-text-secondary">
            Last name (optional)
          </label>
          <input id="lastName" name="lastName" className={`mt-1 ${inputClassName}`} />
        </div>

        <div>
          <label htmlFor="dateOfBirth" className="font-control text-sm text-text-secondary">
            Date of birth
          </label>
          <input
            id="dateOfBirth"
            name="dateOfBirth"
            type="date"
            required
            max={today}
            className={`mt-1 ${inputClassName}`}
          />
          {state.fieldErrors?.dateOfBirth ? (
            <p className="mt-1 text-sm text-red-700">{state.fieldErrors.dateOfBirth}</p>
          ) : null}
          <p className="mt-1 text-sm text-text-secondary">
            Used to show an age, so a caregiver knows what to expect.
          </p>
        </div>

        <div>
          <label htmlFor="recipientType" className="font-control text-sm text-text-secondary">
            They are
          </label>
          <select
            id="recipientType"
            name="recipientType"
            defaultValue="Child"
            className={`mt-1 ${inputClassName}`}
          >
            {CARE_RECIPIENT_TYPES.map((type) => (
              <option key={type} value={type}>
                {type === "Senior" ? "A senior" : type === "Adult" ? "An adult" : "A child"}
              </option>
            ))}
          </select>
          {state.fieldErrors?.recipientType ? (
            <p className="mt-1 text-sm text-red-700">{state.fieldErrors.recipientType}</p>
          ) : null}
        </div>
      </div>

      <div className="flex gap-2">
        <SubmitButton />
        <Button type="button" variant="ghost" onClick={onDone}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

const inputClassName =
  "h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary";

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" disabled={pending}>
      {pending ? "Adding…" : "Add"}
    </Button>
  );
}
