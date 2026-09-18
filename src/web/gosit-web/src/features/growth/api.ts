import "server-only";

import { apiFetch } from "@/lib/api/client";
import type { ReferralSummary } from "@/features/growth/types";

/**
 * The caller's referral code and the invitations against it.
 *
 * The code is assigned lazily by the API the first time it is asked for, so simply
 * loading this page is what gives an account its code.
 */
export function getMyReferrals(): Promise<ReferralSummary> {
  return apiFetch<ReferralSummary>("/api/v1/me/referrals");
}
