import "server-only";

import { ApiError, apiFetch } from "@/lib/api/client";
import type { CareRecipient } from "@/features/account/types";

/**
 * Reads for the account screens.
 *
 * The profile itself comes from the bookings feature's getMyProfile — one function, one
 * endpoint. Only the full recipient record lives here, because it is the one read that
 * returns care needs and it should be obvious where that happens.
 */

/** One recipient in full, including care needs. Null when the id is not the caller's. */
export async function getCareRecipient(careRecipientId: string): Promise<CareRecipient | null> {
  try {
    return await apiFetch<CareRecipient>(
      `/api/v1/care-requesters/me/care-recipients/${careRecipientId}`,
    );
  } catch (error) {
    // The API returns 404 for a recipient belonging to someone else, so this covers both
    // "does not exist" and "not yours" — which is the intent.
    if (error instanceof ApiError && error.status === 404) {
      return null;
    }

    throw error;
  }
}

export { getMyProfile } from "@/features/bookings/api";
