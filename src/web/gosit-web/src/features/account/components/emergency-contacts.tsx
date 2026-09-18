"use client";

import { useActionState, useState, useTransition } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { inputClassName } from "@/components/ui/text-input";
import {
  addEmergencyContactAction,
  removeEmergencyContactAction,
  type AccountFormState,
} from "@/features/account/actions";
import { formatPhoneNumber } from "@/features/account/phone";
import type { CareRequesterProfile } from "@/features/bookings/types";

type EmergencyContact = CareRequesterProfile["emergencyContacts"][number];

/**
 * People a caregiver can ring when the family cannot be reached.
 *
 * These have existed in the data model since the beginning and there was no way to add
 * one, so every family's list was empty and a caregiver alone in a house with an
 * unanswered phone had nobody to call.
 *
 * The screen is explicit about who sees these and when, because a phone number given here
 * belongs to a third party who is not a GoSit user and cannot consent through the
 * product. Someone should know what they are agreeing to on that person's behalf.
 */
export function EmergencyContacts({ contacts }: { contacts: EmergencyContact[] }) {
  const [isAdding, setIsAdding] = useState(contacts.length === 0);

  return (
    <div className="flex flex-col gap-4">
      {contacts.length > 0 ? (
        <ul className="flex flex-col gap-3">
          {contacts.map((contact) => (
            <li key={contact.id}>
              <ContactRow contact={contact} />
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-sm text-text-secondary">
          Nobody added yet. A caregiver who cannot reach you has no one else to try.
        </p>
      )}

      {isAdding ? (
        <AddContactForm onDone={() => setIsAdding(false)} />
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

function ContactRow({ contact }: { contact: EmergencyContact }) {
  const [isPending, startTransition] = useTransition();
  const [isConfirming, setIsConfirming] = useState(false);
  const [error, setError] = useState<string | null>(null);

  return (
    <div className="rounded-card border border-border bg-surface p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="text-text-primary">{contact.fullName}</p>
          <p className="text-sm text-text-secondary">
            {contact.relationship} · {formatPhoneNumber(contact.phoneNumber)}
          </p>
        </div>

        <button
          type="button"
          onClick={() => setIsConfirming(true)}
          className="font-control text-sm text-text-secondary hover:text-text-primary"
        >
          Remove
        </button>
      </div>

      {isConfirming ? (
        <div className="mt-3 border-t border-border pt-3">
          <p className="text-sm text-text-primary">
            Remove {contact.fullName}? Caregivers on your bookings will no longer see this
            number.
          </p>

          {error ? (
            <p role="alert" className="mt-2 text-sm text-danger-accent">
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

                  const result = await removeEmergencyContactAction(contact.id);

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

function AddContactForm({ onDone }: { onDone: () => void }) {
  const [state, formAction] = useActionState<AccountFormState, FormData>(
    async (previous, formData) => {
      const result = await addEmergencyContactAction(previous, formData);

      if (result.saved) {
        onDone();
      }

      return result;
    },
    {},
  );

  return (
    <form
      action={formAction}
      className="flex flex-col gap-4 rounded-card border border-border bg-surface p-4"
      noValidate
    >
      <div>
        <h3 className="font-heading text-base font-medium text-text-primary">
          Who should a caregiver call?
        </h3>
        <p className="mt-1 text-sm text-text-secondary">
          Ask them first. A caregiver sees this only after they have accepted one of your
          bookings, and we record every time it is shown.
        </p>
      </div>

      {state.error ? (
        <p role="alert" className="rounded-control bg-danger-soft px-4 py-3 text-sm text-text-primary">
          {state.error}
        </p>
      ) : null}

      <div>
        <label htmlFor="fullName" className="font-control text-sm text-text-secondary">
          Their name
        </label>
        <input id="fullName" name="fullName" required className={`mt-1 ${inputClassName}`} />
        <FieldError message={state.fieldErrors?.fullName} />
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label htmlFor="relationship" className="font-control text-sm text-text-secondary">
            How you know them
          </label>
          <input
            id="relationship"
            name="relationship"
            required
            placeholder="Grandmother, neighbour, uncle"
            className={`mt-1 ${inputClassName}`}
          />
          <FieldError message={state.fieldErrors?.relationship} />
        </div>

        <div>
          <label htmlFor="phoneNumber" className="font-control text-sm text-text-secondary">
            Phone number
          </label>
          <input
            id="phoneNumber"
            name="phoneNumber"
            type="tel"
            required
            className={`mt-1 ${inputClassName}`}
          />
          <FieldError message={state.fieldErrors?.phoneNumber} />
        </div>
      </div>

      <div className="flex gap-2">
        <SaveButton />
        <Button type="button" variant="ghost" onClick={onDone}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

function FieldError({ message }: { message?: string }) {
  if (!message) {
    return null;
  }

  return (
    <p role="alert" className="mt-1 text-sm text-danger-accent">
      {message}
    </p>
  );
}

function SaveButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" disabled={pending}>
      {pending ? "Adding…" : "Add contact"}
    </Button>
  );
}
