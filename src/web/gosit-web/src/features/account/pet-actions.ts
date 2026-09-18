"use server";

import { revalidatePath } from "next/cache";
import type { AccountFormState } from "@/features/account/actions";
import {
  PET_SIZES,
  PET_SPECIES,
  type PetSize,
  type PetSpecies,
  type SavePetInput,
} from "@/features/account/types";
import { ApiError, SessionExpiredError, apiFetch, apiSend } from "@/lib/api/client";

/**
 * A family's pets.
 *
 * Care notes travel as FormData rather than as scalar Server Action arguments, for the
 * same reason as the rest of the account actions: Next's development server echoes scalar
 * arguments to the console. The pet id is a scalar because it is an identifier, not content.
 */

export async function addPetAction(
  _previous: AccountFormState,
  formData: FormData,
): Promise<AccountFormState> {
  const { input, fieldErrors } = readPet(formData);

  if (!input) {
    return { fieldErrors };
  }

  try {
    await apiFetch("/api/v1/care-requesters/me/pets", { method: "POST", body: input });
  } catch (error) {
    return toState(error, "We could not add this pet.");
  }

  revalidatePath("/account");

  return { saved: true };
}

export async function updatePetAction(
  petId: string,
  _previous: AccountFormState,
  formData: FormData,
): Promise<AccountFormState> {
  const { input, fieldErrors } = readPet(formData);

  if (!input) {
    return { fieldErrors };
  }

  try {
    await apiFetch(`/api/v1/care-requesters/me/pets/${petId}`, { method: "PUT", body: input });
  } catch (error) {
    return toState(error, "We could not save these changes.");
  }

  revalidatePath("/account");

  return { saved: true };
}

export async function removePetAction(petId: string): Promise<AccountFormState> {
  try {
    await apiSend(`/api/v1/care-requesters/me/pets/${petId}`, { method: "DELETE" });
  } catch (error) {
    return toState(error, "We could not remove this pet.");
  }

  revalidatePath("/account");

  return { saved: true };
}

function text(formData: FormData, key: string): string {
  return String(formData.get(key) ?? "").trim();
}

function readPet(formData: FormData): {
  input?: SavePetInput;
  fieldErrors?: Record<string, string>;
} {
  const name = text(formData, "name");
  const species = text(formData, "species");
  const size = text(formData, "size");

  const fieldErrors: Record<string, string> = {};

  if (!name) fieldErrors.name = "Enter your pet's name.";
  if (!PET_SPECIES.includes(species as PetSpecies)) {
    fieldErrors.species = "Choose what kind of pet this is.";
  }
  if (size && !PET_SIZES.includes(size as PetSize)) {
    fieldErrors.size = "Choose a size, or leave it blank.";
  }

  if (Object.keys(fieldErrors).length > 0) {
    return { fieldErrors };
  }

  return {
    input: {
      name,
      species: species as PetSpecies,
      breed: text(formData, "breed") || null,
      size: size ? (size as PetSize) : null,
      feedingInstructions: text(formData, "feedingInstructions") || null,
      medication: text(formData, "medication") || null,
      behaviour: text(formData, "behaviour") || null,
      vetClinic: text(formData, "vetClinic") || null,
      otherNotes: text(formData, "otherNotes") || null,
    },
  };
}

function toState(error: unknown, fallback: string): AccountFormState {
  if (error instanceof SessionExpiredError) {
    return { error: "Your session has expired. Log in again to continue." };
  }

  if (error instanceof ApiError) {
    const fieldErrors: Record<string, string> = {};

    for (const [key, messages] of Object.entries(error.problem?.errors ?? {})) {
      const segment = key.split(".").pop() ?? key;
      const field = segment.charAt(0).toLowerCase() + segment.slice(1);
      const first = Array.isArray(messages) ? messages[0] : undefined;

      if (typeof first === "string") {
        fieldErrors[field] = first;
      }
    }

    return Object.keys(fieldErrors).length > 0
      ? { fieldErrors }
      : { error: error.problem?.title ?? fallback };
  }

  return { error: fallback };
}
