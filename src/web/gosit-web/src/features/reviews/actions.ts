"use server";

import { revalidatePath } from "next/cache";
import { ApiError, SessionExpiredError, apiSend } from "@/lib/api/client";

/**
 * Asking staff to look at a review written about you.
 *
 * Only the subject of a review can report it, which the API enforces — the control is
 * shown only on your own reviews screen, and a stranger calling the route gets a 404.
 *
 * The reason travels as FormData rather than as a scalar argument: Next's development
 * server echoes scalar Server Action arguments to the console, and someone explaining
 * that a review names their child is not console material.
 */

export interface ReportReviewState {
  error?: string;
  reported?: boolean;
}

export async function reportReviewAction(
  reviewId: string,
  _previous: ReportReviewState,
  formData: FormData,
): Promise<ReportReviewState> {
  const reason = String(formData.get("reason") ?? "").trim();

  if (reason.length === 0) {
    return { error: "Tell us what is wrong with it." };
  }

  try {
    await apiSend(`/api/v1/reviews/${reviewId}/report`, {
      method: "POST",
      body: { reason },
    });
  } catch (error) {
    if (error instanceof SessionExpiredError) {
      return { error: "Your session has expired. Log in again to continue." };
    }

    if (error instanceof ApiError) {
      return { error: error.problem?.title ?? "We could not send that report." };
    }

    return { error: "We could not send that report." };
  }

  revalidatePath("/caregiver/reviews");

  return { reported: true };
}
