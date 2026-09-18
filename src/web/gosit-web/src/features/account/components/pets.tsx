"use client";

import { useActionState, useState, useTransition } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import type { AccountFormState } from "@/features/account/actions";
import {
  addPetAction,
  removePetAction,
  updatePetAction,
} from "@/features/account/pet-actions";
import {
  PET_SIZE_LABELS,
  PET_SIZES,
  PET_SPECIES,
  PET_SPECIES_LABELS,
  type Pet,
} from "@/features/account/types";
import { describePet } from "@/features/bookings/types";

/**
 * A family's pets, for dog walks, drop-in visits and pet sitting.
 *
 * Closed by default, unlike the people care is for: most families booking a nanny have
 * no reason to see a pet form open on their account page.
 */
export function Pets({ pets }: { pets: Pet[] }) {
  const [isAdding, setIsAdding] = useState(false);

  return (
    <div className="flex flex-col gap-4">
      {pets.length > 0 ? (
        <ul className="flex flex-col gap-3">
          {pets.map((pet) => (
            <li key={pet.id}>
              <PetRow pet={pet} />
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-sm text-text-secondary">
          No pets yet. Add one to book a dog walk, a drop-in visit or pet sitting.
        </p>
      )}

      {isAdding ? (
        <PetForm onDone={() => setIsAdding(false)} />
      ) : (
        <div>
          <Button type="button" variant="ghost" onClick={() => setIsAdding(true)}>
            Add a pet
          </Button>
        </div>
      )}
    </div>
  );
}

function PetRow({ pet }: { pet: Pet }) {
  const [isEditing, setIsEditing] = useState(false);
  const [isConfirming, setIsConfirming] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  const hasNotes = Boolean(
    pet.feedingInstructions || pet.medication || pet.behaviour || pet.vetClinic || pet.otherNotes,
  );

  if (isEditing) {
    return <PetForm pet={pet} onDone={() => setIsEditing(false)} />;
  }

  return (
    <div className="rounded-card border border-border bg-surface p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-text-primary">{pet.name}</p>
          <p className="text-sm text-text-secondary">
            {describePet(pet).slice(pet.name.length + 2, -1)}
            {hasNotes ? " · care notes on file" : ""}
          </p>
        </div>

        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => setIsEditing(true)}
            className="font-control text-sm text-brand-primary hover:underline"
          >
            {hasNotes ? "Edit" : "Add care notes"}
          </button>
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
            Remove {pet.name}? Past bookings keep their own record of who was looked after.
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

                  const result = await removePetAction(pet.id);

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

function PetForm({ pet, onDone }: { pet?: Pet; onDone: () => void }) {
  const [state, formAction] = useActionState<AccountFormState, FormData>(
    async (previous, formData) => {
      const result = pet
        ? await updatePetAction(pet.id, previous, formData)
        : await addPetAction(previous, formData);

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
      <h3 className="font-heading text-base font-medium text-text-primary">
        {pet ? `About ${pet.name}` : "Add a pet"}
      </h3>

      {state.error ? (
        <p role="alert" className="rounded-control bg-red-50 px-4 py-3 text-sm text-red-800">
          {state.error}
        </p>
      ) : null}

      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Name" htmlFor="name" error={state.fieldErrors?.name}>
          <input
            id="name"
            name="name"
            required
            maxLength={60}
            defaultValue={pet?.name}
            className={inputClassName}
          />
        </Field>

        <Field label="Kind of pet" htmlFor="species" error={state.fieldErrors?.species}>
          <select
            id="species"
            name="species"
            defaultValue={pet?.species ?? "Dog"}
            className={inputClassName}
          >
            {PET_SPECIES.map((species) => (
              <option key={species} value={species}>
                {PET_SPECIES_LABELS[species]}
              </option>
            ))}
          </select>
        </Field>

        <Field label="Breed (optional)" htmlFor="breed" error={state.fieldErrors?.breed}>
          <input
            id="breed"
            name="breed"
            maxLength={80}
            defaultValue={pet?.breed ?? ""}
            className={inputClassName}
          />
        </Field>

        <Field label="Size (optional)" htmlFor="size" error={state.fieldErrors?.size}>
          <select
            id="size"
            name="size"
            defaultValue={pet?.size ?? ""}
            className={inputClassName}
          >
            <option value="">Not sure, or not relevant</option>
            {PET_SIZES.map((size) => (
              <option key={size} value={size}>
                {PET_SIZE_LABELS[size]}
              </option>
            ))}
          </select>
        </Field>
      </div>

      <fieldset className="flex flex-col gap-4 border-t border-border pt-4">
        <legend className="font-control text-sm font-medium text-text-primary">Care notes</legend>

        {/*
          Said where people are about to type it. The notes reach a sitter only after they
          accept, but a door code does not need to live on file at all — it belongs on the
          booking it is for.
        */}
        <p className="-mt-2 text-sm text-text-secondary">
          A sitter sees these only after accepting your booking. Keep door codes and when
          you will be away for the booking notes, rather than on file here.
        </p>

        <Note
          name="behaviour"
          label="Temperament, and anything to watch for"
          placeholder="Friendly with people, reactive to other dogs on the lead."
          defaultValue={pet?.behaviour}
          error={state.fieldErrors?.behaviour}
        />
        <Note
          name="feedingInstructions"
          label="Feeding"
          placeholder="One cup of dry food at 6pm. No table scraps."
          defaultValue={pet?.feedingInstructions}
          error={state.fieldErrors?.feedingInstructions}
        />
        <Note
          name="medication"
          label="Medication"
          placeholder="Half a tablet with breakfast, hidden in cheese."
          defaultValue={pet?.medication}
          error={state.fieldErrors?.medication}
        />
        <Note
          name="vetClinic"
          label="Vet"
          placeholder="Annex Animal Hospital, 416-555-0199"
          defaultValue={pet?.vetClinic}
          error={state.fieldErrors?.vetClinic}
          rows={1}
        />
        <Note
          name="otherNotes"
          label="Anything else"
          defaultValue={pet?.otherNotes}
          error={state.fieldErrors?.otherNotes}
        />
      </fieldset>

      <div className="flex gap-2">
        <SubmitButton isEditing={Boolean(pet)} />
        <Button type="button" variant="ghost" onClick={onDone}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

function Field({
  label,
  htmlFor,
  error,
  children,
}: {
  label: string;
  htmlFor: string;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <div>
      <label htmlFor={htmlFor} className="font-control text-sm text-text-secondary">
        {label}
      </label>
      <div className="mt-1">{children}</div>
      {error ? <p className="mt-1 text-sm text-red-700">{error}</p> : null}
    </div>
  );
}

function Note({
  name,
  label,
  placeholder,
  defaultValue,
  error,
  rows = 2,
}: {
  name: string;
  label: string;
  placeholder?: string;
  defaultValue?: string | null;
  error?: string;
  rows?: number;
}) {
  return (
    <div>
      <label htmlFor={name} className="font-control text-sm text-text-secondary">
        {label}
      </label>
      <textarea
        id={name}
        name={name}
        rows={rows}
        maxLength={2000}
        placeholder={placeholder}
        defaultValue={defaultValue ?? ""}
        className="mt-1 w-full rounded-control border border-border bg-surface p-3 font-control text-sm text-text-primary"
      />
      {error ? <p className="mt-1 text-sm text-red-700">{error}</p> : null}
    </div>
  );
}

const inputClassName =
  "h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary";

function SubmitButton({ isEditing }: { isEditing: boolean }) {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" disabled={pending}>
      {pending ? "Saving…" : isEditing ? "Save" : "Add"}
    </Button>
  );
}
