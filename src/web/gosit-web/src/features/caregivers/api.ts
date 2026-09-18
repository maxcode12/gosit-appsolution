import "server-only";

import { ApiError, apiFetch } from "@/lib/api/client";
import type {
  BookingCareNeeds,
  CaregiverInsights,
  CaregiverProfile,
  Earnings,
  EmergencyContact,
  PayoutAccount,
  VerificationSummary,
} from "@/features/caregivers/types";

/**
 * Reads for the caregiver screens. Every route resolves the profile from the access
 * token — none takes a profile id — so one caregiver cannot address another's data.
 */

/**
 * Whether a failure means "you have no caregiver profile".
 *
 * 404 is the obvious case. 403 is the wrong-role case: these routes are gated by the
 * API's `CaregiverOnly` policy, so a care requester reaching one gets forbidden rather
 * than not-found. Both mean the same thing to a page — there is no profile here — and
 * treating only 404 as such left a 403 thrown mid-render, racing the layout redirect
 * that was about to send them elsewhere anyway.
 */
function isMissingProfile(status: number): boolean {
  return status === 404 || status === 403;
}

/** The caller's caregiver profile, or null before they have created one. */
export async function getMyCaregiverProfile(): Promise<CaregiverProfile | null> {
  try {
    return await apiFetch<CaregiverProfile>("/api/v1/caregivers/me");
  } catch (error) {
    if (error instanceof ApiError && isMissingProfile(error.status)) {
      return null;
    }

    throw error;
  }
}

/** The wallet and statement. Null before a profile exists. */
export async function getEarnings(): Promise<Earnings | null> {
  try {
    return await apiFetch<Earnings>("/api/v1/caregivers/me/earnings");
  } catch (error) {
    if (error instanceof ApiError && isMissingProfile(error.status)) {
      return null;
    }

    throw error;
  }
}

/** Where the caregiver stands with payouts. Null before a profile exists. */
export async function getPayoutAccount(): Promise<PayoutAccount | null> {
  try {
    return await apiFetch<PayoutAccount>("/api/v1/caregivers/me/payout-account");
  } catch (error) {
    if (error instanceof ApiError && isMissingProfile(error.status)) {
      return null;
    }

    throw error;
  }
}

export async function getMyVerifications(): Promise<VerificationSummary | null> {
  try {
    return await apiFetch<VerificationSummary>("/api/v1/caregivers/me/verifications");
  } catch (error) {
    if (error instanceof ApiError && isMissingProfile(error.status)) {
      return null;
    }

    throw error;
  }
}

/**
 * The care needs of one recipient on one booking.
 *
 * The API discloses these only to a caregiver who has accepted the booking, and audits
 * every read. A 404 covers every refusal — not accepted yet, not this caregiver's
 * booking, recipient not on it — so this returns null and the caller explains rather
 * than treating it as an error.
 */
export async function getBookingCareNeeds(
  bookingId: string,
  careRecipientId: string,
): Promise<BookingCareNeeds | null> {
  try {
    return await apiFetch<BookingCareNeeds>(
      `/api/v1/bookings/${bookingId}/recipients/${careRecipientId}/care-needs`,
    );
  } catch (error) {
    if (error instanceof ApiError && isMissingProfile(error.status)) {
      return null;
    }

    throw error;
  }
}

/**
 * How the caller's own work is going. Null before a profile exists.
 *
 * Deliberately does NOT treat 403 as a missing profile, unlike the reads above: this
 * route is also gated on the caregiver's plan, so a 403 can mean "not entitled". The
 * screen resolves entitlement from the subscription before calling this, so a 403
 * reaching here is a real fault and should surface as one rather than be swallowed into
 * an empty state that says the profile does not exist.
 */
export async function getInsights(): Promise<CaregiverInsights | null> {
  try {
    return await apiFetch<CaregiverInsights>("/api/v1/caregivers/me/insights");
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return null;
    }

    throw error;
  }
}

/**
 * Who to ring if the family cannot be reached during this booking.
 *
 * Null before the booking is accepted, which is the API's rule rather than this
 * function's: a pending request discloses nothing, so the screen has nothing to show.
 */
export async function getBookingEmergencyContacts(
  bookingId: string,
): Promise<EmergencyContact[]> {
  try {
    const response = await apiFetch<{ contacts: EmergencyContact[] }>(
      `/api/v1/bookings/${bookingId}/emergency-contacts`,
    );

    return response.contacts;
  } catch (error) {
    if (error instanceof ApiError && isMissingProfile(error.status)) {
      return [];
    }

    throw error;
  }
}
