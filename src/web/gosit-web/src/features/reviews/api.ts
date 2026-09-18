import "server-only";

import { ApiError, apiFetch } from "@/lib/api/client";
import type { CaregiverReviews } from "@/features/reviews/types";

/**
 * Published reviews about a caregiver.
 *
 * Anonymous-safe by design: the endpoint returns only reviews that have been published,
 * so a blind review still in its window is never included whoever is asking.
 */
export async function getCaregiverReviews(
  caregiverProfileId: string,
  take = 20,
): Promise<CaregiverReviews> {
  try {
    return await apiFetch<CaregiverReviews>(
      `/api/v1/caregivers/${caregiverProfileId}/reviews?take=${take}`,
    );
  } catch (error) {
    // A caregiver with no reviews yet, or one who is no longer listed. Neither is an
    // error worth breaking a profile page over.
    if (error instanceof ApiError && error.status === 404) {
      return { averageRating: null, reviewCount: 0, reviews: [] };
    }

    throw error;
  }
}
