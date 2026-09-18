"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { ApiError, SessionExpiredError, apiFetch, apiSend } from "@/lib/api/client";
import type {
  InsuranceCoverageType,
  UpsertCaregiverProfileInput,
  VerificationCheckType,
} from "@/features/caregivers/types";

/**
 * Caregiver mutations.
 *
 * Note what is absent: nothing here sets verification status or certification status.
 * Those belong to Trust and Safety, and a caregiver submitting a check must not be able
 * to approve it — the API would refuse, and offering it would be dishonest.
 */

export interface CaregiverFormState {
  error?: string;
  fieldErrors?: Record<string, string>;
  saved?: boolean;
}

function toState(error: unknown, fallback: string): CaregiverFormState {
  if (error instanceof SessionExpiredError) {
    return { error: "Your session has expired. Log in again to continue." };
  }

  if (error instanceof ApiError) {
    const fieldErrors: Record<string, string> = {};

    for (const [key, messages] of Object.entries(error.problem?.errors ?? {})) {
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

function revalidateCaregiver(): void {
  revalidatePath("/caregiver");
  revalidatePath("/caregiver/account");
}

export async function saveCaregiverProfileAction(
  _previous: CaregiverFormState,
  formData: FormData,
): Promise<CaregiverFormState> {
  const hourlyRate = Number(text(formData, "hourlyRate"));
  // Blank means "I offer no overnight pet care", and clears a rate set before.
  const nightlyRateText = text(formData, "nightlyRate");
  const nightlyRate = nightlyRateText ? Number(nightlyRateText) : null;
  const yearsOfExperience = Number(text(formData, "yearsOfExperience"));
  const serviceRadiusKm = Number(text(formData, "serviceRadiusKm"));
  const line1 = text(formData, "line1");
  const city = text(formData, "city");
  const province = text(formData, "province").toUpperCase();
  const postalCode = text(formData, "postalCode").replace(/\s+/g, "").toUpperCase();

  const fieldErrors: Record<string, string> = {};

  if (!Number.isFinite(hourlyRate) || hourlyRate <= 0) {
    fieldErrors.hourlyRate = "Enter what you charge per hour.";
  }
  if (nightlyRate !== null && (!Number.isFinite(nightlyRate) || nightlyRate <= 0)) {
    fieldErrors.nightlyRate = "Enter what you charge per night, or leave it blank.";
  }
  if (!Number.isInteger(yearsOfExperience) || yearsOfExperience < 0) {
    fieldErrors.yearsOfExperience = "Enter a whole number of years.";
  }
  if (!Number.isInteger(serviceRadiusKm) || serviceRadiusKm <= 0) {
    fieldErrors.serviceRadiusKm = "How far will you travel?";
  }
  if (!line1) fieldErrors.line1 = "Enter the street address.";
  if (!city) fieldErrors.city = "Enter the city or town.";
  if (province.length !== 2) fieldErrors.province = "Use the two-letter code, e.g. ON.";
  if (postalCode.length !== 6) fieldErrors.postalCode = "Enter a postal code, e.g. M5S 1M2.";

  if (Object.keys(fieldErrors).length > 0) {
    return { fieldErrors };
  }

  const input: UpsertCaregiverProfileInput = {
    bio: text(formData, "bio") || null,
    yearsOfExperience,
    hourlyRate,
    nightlyRate,
    address: {
      line1,
      line2: text(formData, "line2") || null,
      city,
      province,
      postalCode,
      country: "CA",
    },
    serviceRadiusKm,
  };

  try {
    await apiFetch("/api/v1/caregivers/me", { method: "PUT", body: input });
  } catch (error) {
    return toState(error, "We could not save your profile.");
  }

  revalidateCaregiver();

  return { saved: true };
}

/**
 * Replaces the services offered.
 *
 * The API takes the whole set, so an unchecked box is a removal — which is why the form
 * always posts every selection rather than a delta.
 */
export async function setServicesAction(
  _previous: CaregiverFormState,
  formData: FormData,
): Promise<CaregiverFormState> {
  const serviceCodes = formData.getAll("serviceCodes").map(String).filter(Boolean);

  try {
    await apiFetch("/api/v1/caregivers/me/services", {
      method: "PUT",
      body: { serviceCodes },
    });
  } catch (error) {
    return toState(error, "We could not save your services.");
  }

  revalidateCaregiver();

  return { saved: true };
}

/**
 * Replaces the weekly availability.
 *
 * Also a whole-set replace. The API substitutes an empty list for a missing `windows`,
 * so this always sends the array explicitly — a malformed body would otherwise clear the
 * schedule and report success.
 */
export async function setAvailabilityAction(
  _previous: CaregiverFormState,
  formData: FormData,
): Promise<CaregiverFormState> {
  // The form posts one row per weekday, each with a checkbox naming the day. An
  // unchecked day contributes no window, which is how a day is cleared.
  const enabled = new Set(formData.getAll("enabled").map(String));

  const windows = formData
    .getAll("day")
    .map(String)
    .filter((day) => enabled.has(day))
    .flatMap((day) => {
      const start = text(formData, `startTime-${day}`);
      const end = text(formData, `endTime-${day}`);

      // A window must move forwards in time. A reversed one is dropped rather than
      // sent, since the API would reject the whole request over it.
      return start && end && start < end
        ? [{ day, startTime: `${start}:00`, endTime: `${end}:00` }]
        : [];
    });

  try {
    await apiFetch("/api/v1/caregivers/me/availability", {
      method: "PUT",
      body: { windows },
    });
  } catch (error) {
    return toState(error, "We could not save your availability.");
  }

  revalidateCaregiver();

  return { saved: true };
}

export async function submitCertificationAction(
  _previous: CaregiverFormState,
  formData: FormData,
): Promise<CaregiverFormState> {
  const certificationCode = text(formData, "certificationCode");
  const issuedOn = text(formData, "issuedOn");
  const expiresOn = text(formData, "expiresOn");

  const fieldErrors: Record<string, string> = {};

  if (!certificationCode) fieldErrors.certificationCode = "Choose a certification.";
  if (!issuedOn) fieldErrors.issuedOn = "When was it issued?";

  if (Object.keys(fieldErrors).length > 0) {
    return { fieldErrors };
  }

  try {
    await apiFetch("/api/v1/caregivers/me/certifications", {
      method: "POST",
      body: {
        certificationCode,
        issuedOn,
        expiresOn: expiresOn || null,
        // Document upload is not wired up yet; the reference is set when it is.
        documentReference: null,
      },
    });
  } catch (error) {
    return toState(error, "We could not submit this certification.");
  }

  revalidateCaregiver();

  return { saved: true };
}

/**
 * Opens or closes this caregiver to new requests.
 *
 * The last condition on being listed. Opening requires verification and the API refuses
 * otherwise; closing is always allowed.
 */
export async function setAcceptingWorkAction(
  acceptingWork: boolean,
): Promise<CaregiverFormState> {
  try {
    await apiFetch("/api/v1/caregivers/me/accepting-work", {
      method: "PUT",
      body: { acceptingWork },
    });
  } catch (error) {
    return toState(
      error,
      acceptingWork
        ? "We could not open you to new requests."
        : "We could not pause your requests.",
    );
  }

  revalidateCaregiver();

  return { saved: true };
}

/**
 * Starts, or resumes, hosted payout onboarding.
 *
 * GoSit never sees bank details — they go straight to the provider — so this returns
 * nothing but a redirect. The link is single-use and short-lived, which is why it is
 * requested fresh each time rather than stored.
 */
export async function startPayoutOnboardingAction(): Promise<CaregiverFormState> {
  let onboarding: { onboardingUrl: string };

  try {
    onboarding = await apiFetch<{ onboardingUrl: string }>(
      "/api/v1/caregivers/me/payout-account",
      { method: "POST" },
    );
  } catch (error) {
    return toState(error, "We could not start your payout setup.");
  }

  redirect(onboarding.onboardingUrl);
}

/**
 * Electronically signs the Caregiver Service Agreement.
 *
 * The typed name is the signature; the version is never sent by the client — the API
 * always records whichever version is current, so this cannot be used to keep an old
 * signature alive past a real revision.
 */
export async function acceptServiceAgreementAction(
  _previous: CaregiverFormState,
  formData: FormData,
): Promise<CaregiverFormState> {
  const signedName = text(formData, "signedName");

  if (!signedName) {
    return { fieldErrors: { signedName: "Type your full legal name to sign." } };
  }

  try {
    await apiFetch("/api/v1/caregivers/me/service-agreement/accept", {
      method: "POST",
      body: { signedName },
    });
  } catch (error) {
    return toState(error, "We could not record your signature.");
  }

  revalidateCaregiver();

  return { saved: true };
}

/**
 * Records an insurance policy the caregiver claims to hold. Stored as Pending — it
 * confers nothing until staff verify it, mirroring how a certification works.
 */
export async function submitInsuranceAction(
  _previous: CaregiverFormState,
  formData: FormData,
): Promise<CaregiverFormState> {
  const coverageType = text(formData, "coverageType") as InsuranceCoverageType;
  const insurer = text(formData, "insurer");
  const policyReference = text(formData, "policyReference");
  const liabilityLimit = Number(text(formData, "liabilityLimit"));
  const effectiveOn = text(formData, "effectiveOn");
  const expiresOn = text(formData, "expiresOn");

  const fieldErrors: Record<string, string> = {};

  if (!insurer) fieldErrors.insurer = "Enter your insurer's name.";
  if (!policyReference) fieldErrors.policyReference = "Enter your policy number.";
  if (!Number.isFinite(liabilityLimit) || liabilityLimit <= 0) {
    fieldErrors.liabilityLimit = "Enter the policy's liability limit.";
  }
  if (!effectiveOn) fieldErrors.effectiveOn = "When does the policy start?";
  if (!expiresOn) fieldErrors.expiresOn = "When does the policy expire?";

  if (Object.keys(fieldErrors).length > 0) {
    return { fieldErrors };
  }

  try {
    await apiFetch("/api/v1/caregivers/me/insurance", {
      method: "POST",
      body: { coverageType, insurer, policyReference, liabilityLimit, effectiveOn, expiresOn },
    });
  } catch (error) {
    return toState(error, "We could not submit this policy.");
  }

  revalidateCaregiver();

  return { saved: true };
}

/**
 * Uploads proof of an already-submitted policy. Two steps rather than one request that
 * carries both the policy's details and the file, mirroring certifications: an upload
 * that fails halfway leaves the policy intact and retryable, and replacing a document
 * that came out unreadable does not mean re-entering the policy's details.
 */
export async function uploadInsuranceDocumentAction(
  caregiverInsurancePolicyId: string,
  _previous: CaregiverFormState,
  formData: FormData,
): Promise<CaregiverFormState> {
  const file = formData.get("file");

  if (!(file instanceof File) || file.size === 0) {
    return { error: "Choose a file to upload." };
  }

  const upload = new FormData();
  upload.set("file", file, file.name);

  try {
    await apiSend(`/api/v1/caregivers/me/insurance/${caregiverInsurancePolicyId}/document`, {
      method: "POST",
      body: upload,
    });
  } catch (error) {
    return toState(error, "We could not upload that file.");
  }

  revalidateCaregiver();

  return { saved: true };
}

export async function submitVerificationAction(
  checkType: VerificationCheckType,
): Promise<CaregiverFormState> {
  try {
    await apiFetch("/api/v1/caregivers/me/verifications", {
      method: "POST",
      body: { checkType, documentReference: null },
    });
  } catch (error) {
    return toState(error, "We could not start this check.");
  }

  revalidateCaregiver();

  return { saved: true };
}

/**
 * Booking lifecycle actions for the caregiver side.
 *
 * Each is its own operation, mirroring the API: there is no "set status", because the
 * state machine belongs to the server.
 */
async function bookingTransition(
  bookingId: string,
  action: string,
  fallback: string,
  body?: unknown,
): Promise<CaregiverFormState> {
  try {
    await apiFetch(`/api/v1/bookings/${bookingId}/${action}`, { method: "POST", body });
  } catch (error) {
    return toState(error, fallback);
  }

  revalidatePath("/caregiver");
  revalidatePath("/caregiver/bookings");
  revalidatePath(`/caregiver/bookings/${bookingId}`);

  return { saved: true };
}

export async function acceptBookingAction(bookingId: string): Promise<CaregiverFormState> {
  return bookingTransition(bookingId, "accept", "We could not accept this booking.");
}

export async function declineBookingAction(
  bookingId: string,
  _previous: CaregiverFormState,
  formData: FormData,
): Promise<CaregiverFormState> {
  const reason = String(formData.get("reason") ?? "").trim();

  return bookingTransition(bookingId, "decline", "We could not decline this booking.", {
    reason: reason || null,
  });
}

/**
 * Starting and finishing a shift, with the device's location if the browser gave one.
 *
 * The coordinates arrive as FormData from a client component that asked the browser for
 * them. They are optional at every step — the field is absent when the reader declined
 * the permission, when the fix timed out, or when the page is served over plain HTTP
 * where the geolocation API does not exist at all. The API treats all of those the same
 * as not sending one, so nothing here has to special-case them.
 *
 * What must not happen is a check-in that fails because a location could not be had. A
 * caregiver at a family's door needs to start work; the location is evidence, not a
 * turnstile.
 */
function fixFrom(formData: FormData): { latitude: number; longitude: number } | undefined {
  const latitude = Number(formData.get("latitude"));
  const longitude = Number(formData.get("longitude"));

  return Number.isFinite(latitude) && Number.isFinite(longitude)
    && formData.get("latitude") !== null
    ? { latitude, longitude }
    : undefined;
}

export async function checkInAction(
  bookingId: string,
  _previous: CaregiverFormState,
  formData: FormData,
): Promise<CaregiverFormState> {
  return bookingTransition(
    bookingId, "check-in", "We could not check you in.", fixFrom(formData) ?? {});
}

export async function checkOutAction(
  bookingId: string,
  _previous: CaregiverFormState,
  formData: FormData,
): Promise<CaregiverFormState> {
  return bookingTransition(
    bookingId, "check-out", "We could not check you out.", fixFrom(formData) ?? {});
}
