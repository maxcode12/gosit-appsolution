"use server";

import { revalidatePath } from "next/cache";
import { ApiError, SessionExpiredError, apiFetch, apiSend } from "@/lib/api/client";
import type {
  AddCareRecipientInput,
  CareRecipientType,
  UpsertProfileInput,
} from "@/features/account/types";
import { CARE_RECIPIENT_TYPES } from "@/features/account/types";

/**
 * Account mutations.
 *
 * Care needs are written here too. That data never travels anywhere else: it is posted
 * to the API and nothing about it is returned to a list, put in a URL, or logged.
 */

export interface AccountFormState {
  error?: string;
  fieldErrors?: Record<string, string>;
  saved?: boolean;
}

function toState(error: unknown, fallback: string): AccountFormState {
  if (error instanceof SessionExpiredError) {
    return { error: "Your session has expired. Log in again to continue." };
  }

  if (error instanceof ApiError) {
    const fieldErrors: Record<string, string> = {};

    for (const [key, messages] of Object.entries(error.problem?.errors ?? {})) {
      // "Address.Line1" and "PhoneNumber" both arrive here; the form names its fields
      // in camelCase without the prefix, so the last segment is what matches.
      const segment = key.split(".").pop() ?? key;
      const field = segment.charAt(0).toLowerCase() + segment.slice(1);

      if (messages.length > 0) {
        fieldErrors[field] = messages[0];
      }
    }

    return { error: error.problem?.title ?? fallback, fieldErrors };
  }

  throw error;
}

function text(formData: FormData, name: string): string {
  return String(formData.get(name) ?? "").trim();
}

/** Creates the profile on first save and updates it after — the API is an upsert. */
export async function saveProfileAction(
  _previous: AccountFormState,
  formData: FormData,
): Promise<AccountFormState> {
  const phoneNumber = text(formData, "phoneNumber");
  const aboutMe = text(formData, "aboutMe");
  const line1 = text(formData, "line1");
  const city = text(formData, "city");
  const province = text(formData, "province").toUpperCase();
  const postalCode = text(formData, "postalCode").replace(/\s+/g, "").toUpperCase();

  const fieldErrors: Record<string, string> = {};

  if (!phoneNumber) fieldErrors.phoneNumber = "A phone number is needed for bookings.";
  if (!line1) fieldErrors.line1 = "Enter the street address.";
  if (!city) fieldErrors.city = "Enter the city or town.";
  if (province.length !== 2) fieldErrors.province = "Use the two-letter code, e.g. ON.";
  if (postalCode.length !== 6) fieldErrors.postalCode = "Enter a postal code, e.g. M5S 1M2.";

  if (Object.keys(fieldErrors).length > 0) {
    return { fieldErrors };
  }

  const input: UpsertProfileInput = {
    phoneNumber,
    aboutMe: aboutMe || null,
    address: {
      line1,
      line2: text(formData, "line2") || null,
      city,
      province,
      postalCode,
      country: "CA",
    },
  };

  try {
    await apiFetch("/api/v1/care-requesters/me", { method: "PUT", body: input });
  } catch (error) {
    return toState(error, "We could not save your details.");
  }

  revalidatePath("/account");

  return { saved: true };
}

export async function addCareRecipientAction(
  _previous: AccountFormState,
  formData: FormData,
): Promise<AccountFormState> {
  const firstName = text(formData, "firstName");
  const dateOfBirth = text(formData, "dateOfBirth");
  const rawType = text(formData, "recipientType");

  const fieldErrors: Record<string, string> = {};

  if (!firstName) fieldErrors.firstName = "Enter their first name.";
  if (!dateOfBirth) fieldErrors.dateOfBirth = "Enter their date of birth.";
  if (!CARE_RECIPIENT_TYPES.includes(rawType as CareRecipientType)) {
    fieldErrors.recipientType = "Choose who this is.";
  }

  if (Object.keys(fieldErrors).length > 0) {
    return { fieldErrors };
  }

  const input: AddCareRecipientInput = {
    firstName,
    lastName: text(formData, "lastName") || null,
    dateOfBirth,
    recipientType: rawType as CareRecipientType,
  };

  try {
    await apiFetch("/api/v1/care-requesters/me/care-recipients", {
      method: "POST",
      body: input,
    });
  } catch (error) {
    return toState(error, "We could not add this person.");
  }

  revalidatePath("/account");

  return { saved: true };
}

export async function removeCareRecipientAction(
  careRecipientId: string,
): Promise<AccountFormState> {
  try {
    await apiSend(`/api/v1/care-requesters/me/care-recipients/${careRecipientId}`, {
      method: "DELETE",
    });
  } catch (error) {
    return toState(error, "We could not remove this person.");
  }

  revalidatePath("/account");

  return { saved: true };
}

/**
 * Replaces the recorded care needs for one recipient.
 *
 * The API takes the whole record, so an omitted field clears it — the form therefore
 * always posts every field, including the ones left blank.
 */
export async function saveCareNeedsAction(
  careRecipientId: string,
  _previous: AccountFormState,
  formData: FormData,
): Promise<AccountFormState> {
  const optional = (name: string) => text(formData, name) || null;

  const body = {
    mobilityNeeds: optional("mobilityNeeds"),
    communicationStyle: optional("communicationStyle"),
    sensoryNeeds: optional("sensoryNeeds"),
    behaviourSupportNotes: optional("behaviourSupportNotes"),
    dietaryRestrictions: optional("dietaryRestrictions"),
    allergies: optional("allergies"),
    toiletingAssistance: formData.get("toiletingAssistance") === "on",
    feedingAssistance: formData.get("feedingAssistance") === "on",
    routinePreferences: optional("routinePreferences"),
    triggers: optional("triggers"),
    calmingStrategies: optional("calmingStrategies"),
    emergencyInstructions: optional("emergencyInstructions"),
    requiredSkills: formData
      .getAll("requiredSkills")
      .map(String)
      .filter((skill) => skill.length > 0),
  };

  try {
    await apiFetch(
      `/api/v1/care-requesters/me/care-recipients/${careRecipientId}/care-needs`,
      { method: "PUT", body },
    );
  } catch (error) {
    return toState(error, "We could not save these care needs.");
  }

  revalidatePath("/account");
  revalidatePath(`/account/care-recipients/${careRecipientId}`);

  return { saved: true };
}

/**
 * Records someone to call when the family cannot be reached during a booking.
 *
 * The number is disclosed to a caregiver only once they have taken a booking on, and the
 * API audits every disclosure — so what the form asks for here is a real commitment by a
 * third party, and the screen says so rather than treating it as another address field.
 */
export async function addEmergencyContactAction(
  _previous: AccountFormState,
  formData: FormData,
): Promise<AccountFormState> {
  const fullName = text(formData, "fullName");
  const relationship = text(formData, "relationship");
  const phoneNumber = text(formData, "phoneNumber");

  const fieldErrors: Record<string, string> = {};

  if (!fullName) fieldErrors.fullName = "Enter their name.";
  // Asked for because it is what tells a caregiver who to ring first, and whether this is
  // someone who may collect a child.
  if (!relationship) fieldErrors.relationship = "Say how you know them.";
  if (!phoneNumber) fieldErrors.phoneNumber = "Enter a phone number.";

  if (Object.keys(fieldErrors).length > 0) {
    return { fieldErrors };
  }

  try {
    await apiFetch("/api/v1/care-requesters/me/emergency-contacts", {
      method: "POST",
      body: { fullName, relationship, phoneNumber },
    });
  } catch (error) {
    return toState(error, "We could not add this contact.");
  }

  revalidatePath("/account");

  return { saved: true };
}

export async function removeEmergencyContactAction(
  emergencyContactId: string,
): Promise<AccountFormState> {
  try {
    await apiSend(`/api/v1/care-requesters/me/emergency-contacts/${emergencyContactId}`, {
      method: "DELETE",
    });
  } catch (error) {
    return toState(error, "We could not remove this contact.");
  }

  revalidatePath("/account");

  return { saved: true };
}
