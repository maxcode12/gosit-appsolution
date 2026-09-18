import "server-only";

import { apiFetch } from "@/lib/api/client";
import type { Interview } from "@/features/interviews/types";

/**
 * Reads for the interview screens. Both routes are scoped to the signed-in account by the
 * API; an interview the caller is not part of answers 404, so ids cannot be probed.
 */

export async function listInterviews(withProfileId?: string): Promise<Interview[]> {
  const query = withProfileId ? `?withProfileId=${encodeURIComponent(withProfileId)}` : "";

  return apiFetch<Interview[]>(`/api/v1/interviews${query}`);
}

export async function getInterview(interviewId: string): Promise<Interview> {
  return apiFetch<Interview>(`/api/v1/interviews/${interviewId}`);
}
