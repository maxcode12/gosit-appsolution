"use server";

import { revalidatePath } from "next/cache";
import { ApiError, SessionExpiredError, apiFetch } from "@/lib/api/client";

/**
 * Applying a referral code.
 *
 * Only ever done once, by the person who was invited. The API refuses a second attempt,
 * refuses your own code, and refuses a code once you have been on the platform long
 * enough to have used it — all of which surface here as the API's own message, because
 * each one is a different situation and a generic failure would explain none of them.
 */

export interface ReferralFormState {
  error?: string;
  applied?: boolean;
}

export async function applyReferralCodeAction(
  _previous: ReferralFormState,
  formData: FormData,
): Promise<ReferralFormState> {
  const code = String(formData.get("code") ?? "")
    .trim()
    .toUpperCase();

  if (!code) {
    return { error: "Enter the code you were given." };
  }

  try {
    await apiFetch("/api/v1/me/referrals", { method: "POST", body: { code } });
  } catch (error) {
    if (error instanceof SessionExpiredError) {
      return { error: "Your session has expired. Log in again to continue." };
    }

    if (error instanceof ApiError) {
      return { error: error.problem?.title ?? "That code could not be applied." };
    }

    throw error;
  }

  revalidatePath("/account/referrals");
  revalidatePath("/caregiver/referrals");

  return { applied: true };
}
