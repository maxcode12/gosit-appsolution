import "server-only";

import { ApiError, apiFetch } from "@/lib/api/client";
import type {
  AdminIncident,
  AdminInterview,
  FlaggedMessage,
  AdminPromoCode,
  OpenDispute,
  PendingCertification,
  PendingInsurance,
  PendingVerification,
  ReportedReview,
} from "@/features/admin/types";

/**
 * Reads for the staff queues.
 *
 * Every route is behind the API's AdminOnly policy. A non-admin gets 403, which these
 * treat as "nothing here" so a stray visit renders an empty portal rather than throwing
 * mid-render while the role guard redirects.
 */
async function queue<T>(path: string): Promise<T[]> {
  try {
    return await apiFetch<T[]>(path);
  } catch (error) {
    if (error instanceof ApiError && error.status === 403) {
      return [];
    }

    throw error;
  }
}

export function listPendingVerifications(): Promise<PendingVerification[]> {
  return queue<PendingVerification>("/api/v1/admin/verifications");
}

export function listPendingCertifications(): Promise<PendingCertification[]> {
  return queue<PendingCertification>("/api/v1/admin/certifications");
}

export function listPendingInsurance(): Promise<PendingInsurance[]> {
  return queue<PendingInsurance>("/api/v1/admin/insurance");
}

export function listOpenIncidents(): Promise<AdminIncident[]> {
  return queue<AdminIncident>("/api/v1/admin/incidents");
}

export function listPromoCodes(): Promise<AdminPromoCode[]> {
  return queue<AdminPromoCode>("/api/v1/admin/promo-codes");
}

export function listOpenDisputes(): Promise<OpenDispute[]> {
  return queue<OpenDispute>("/api/v1/admin/disputes");
}

export function listFlaggedMessages(): Promise<FlaggedMessage[]> {
  return queue<FlaggedMessage>("/api/v1/admin/flagged-messages");
}

export function listReportedReviews(): Promise<ReportedReview[]> {
  return queue<ReportedReview>("/api/v1/admin/reported-reviews");
}

/** Interviews with a recording, newest first. Not a queue: nothing here waits on staff. */
export function listRecordedInterviews(): Promise<AdminInterview[]> {
  return queue<AdminInterview>("/api/v1/admin/interviews");
}
