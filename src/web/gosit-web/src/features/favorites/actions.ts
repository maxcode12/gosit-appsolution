"use server";

import { revalidatePath } from "next/cache";
import { ApiError, SessionExpiredError, apiFetch, apiSend } from "@/lib/api/client";
import type { CaregiverSearchResult } from "@/features/search/types";

/**
 * Saved caregivers. Every call is scoped to the signed-in requester by the API — the
 * client never supplies a profile id, so one requester cannot touch another's list.
 */

export interface FavoriteActionState {
  error?: string;
}

export async function listFavorites(): Promise<CaregiverSearchResult[]> {
  return apiFetch<CaregiverSearchResult[]>("/api/v1/care-requesters/me/favorites");
}

export async function addFavoriteAction(caregiverProfileId: string): Promise<FavoriteActionState> {
  return toggleFavorite(caregiverProfileId, "PUT");
}

export async function removeFavoriteAction(caregiverProfileId: string): Promise<FavoriteActionState> {
  return toggleFavorite(caregiverProfileId, "DELETE");
}

async function toggleFavorite(
  caregiverProfileId: string,
  method: "PUT" | "DELETE",
): Promise<FavoriteActionState> {
  try {
    await apiSend(`/api/v1/care-requesters/me/favorites/${caregiverProfileId}`, { method });
  } catch (error) {
    if (error instanceof SessionExpiredError) {
      return { error: "Your session has expired. Log in again to save caregivers." };
    }

    if (error instanceof ApiError) {
      if (error.status === 404) {
        return { error: "That caregiver is no longer available." };
      }

      /*
       * A refusal the API can explain, explained in its own words.
       *
       * The plan cap arrives as a 409 whose title names the limit and what lifts it.
       * Collapsing that into "we could not update your saved caregivers" turns a rule
       * someone can act on into what looks like a bug — they press the heart again,
       * get the same nothing, and have no idea a limit exists.
       */
      if (error.status === 409 && error.problem?.title) {
        return { error: error.problem.title };
      }

      return { error: "We could not update your saved caregivers." };
    }

    throw error;
  }

  // Both the saved list and any search results showing this caregiver are now stale.
  // The search lives at /dashboard — "/search" is not a route, so revalidating it was
  // quietly doing nothing and leaving a stale heart on the screen it was pressed from.
  revalidatePath("/saved");
  revalidatePath("/dashboard");

  return {};
}
