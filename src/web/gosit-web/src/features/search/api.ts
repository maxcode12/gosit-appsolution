import "server-only";

import { apiFetch } from "@/lib/api/client";
import type {
  CaregiverDetail,
  CaregiverSearchFilters,
  CaregiverSearchResponse,
  CareServiceCatalogue,
} from "@/features/search/types";

/** Builds the query string, dropping anything unset so the API sees only real filters. */
function toQueryString(filters: CaregiverSearchFilters): string {
  const params = new URLSearchParams();

  for (const [key, value] of Object.entries(filters)) {
    if (value === undefined || value === null || value === "") {
      continue;
    }

    if (Array.isArray(value)) {
      // Repeated keys, which is how ASP.NET Core binds a collection from a query string.
      for (const entry of value) {
        params.append(key, String(entry));
      }
      continue;
    }

    params.set(key, String(value));
  }

  return params.toString();
}

export async function searchCaregivers(
  filters: CaregiverSearchFilters = {},
): Promise<CaregiverSearchResponse> {
  const query = toQueryString(filters);

  return apiFetch<CaregiverSearchResponse>(
    `/api/v1/caregivers/search${query ? `?${query}` : ""}`,
  );
}

export async function getCaregiverDetail(caregiverProfileId: string): Promise<CaregiverDetail> {
  return apiFetch<CaregiverDetail>(`/api/v1/caregivers/${caregiverProfileId}`);
}

/**
 * The care-service taxonomy. Anonymous, identical for every visitor, and changes only
 * when the taxonomy does — so it is cached rather than refetched per request.
 */
export async function getCareServiceCatalogue(): Promise<CareServiceCatalogue> {
  return apiFetch<CareServiceCatalogue>("/api/v1/care-services", {
    anonymous: true,
    revalidate: 3600,
  });
}
