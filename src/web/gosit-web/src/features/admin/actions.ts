"use server";

import { revalidatePath } from "next/cache";
import { ApiError, SessionExpiredError, apiFetch, apiSend } from "@/lib/api/client";

/**
 * Staff decisions.
 *
 * Every one of these is consequential and irreversible from the subject's side — a
 * rejected check, a suspended account. Reasons travel as FormData rather than as scalar
 * arguments, for the same reason as elsewhere: Next's development server echoes scalar
 * Server Action arguments to the console, and a staff note about why someone was removed
 * is not console material.
 *
 * Note what is absent: nothing here approves a caregiver wholesale. Verification status
 * is derived by the API from the individual checks, so there is no "make verified"
 * button to be clicked by mistake.
 */

export interface AdminFormState {
  error?: string;
  done?: boolean;
}

function toState(error: unknown, fallback: string): AdminFormState {
  if (error instanceof SessionExpiredError) {
    return { error: "Your session has expired. Log in again to continue." };
  }

  if (error instanceof ApiError) {
    return { error: error.problem?.title ?? fallback };
  }

  throw error;
}

function revalidateQueues(): void {
  revalidatePath("/admin");
  revalidatePath("/admin/verifications");
  revalidatePath("/admin/certifications");
  revalidatePath("/admin/insurance");
  revalidatePath("/admin/incidents");
}

export async function approveVerificationAction(caseId: string): Promise<AdminFormState> {
  try {
    await apiFetch(`/api/v1/admin/verifications/${caseId}/approve`, {
      method: "POST",
      body: { validUntil: null, notes: null },
    });
  } catch (error) {
    return toState(error, "We could not approve this check.");
  }

  revalidateQueues();

  return { done: true };
}

export async function rejectVerificationAction(
  caseId: string,
  _previous: AdminFormState,
  formData: FormData,
): Promise<AdminFormState> {
  const reason = String(formData.get("reason") ?? "").trim();

  if (!reason) {
    return { error: "Say why, so the caregiver knows what to fix." };
  }

  try {
    await apiFetch(`/api/v1/admin/verifications/${caseId}/reject`, {
      method: "POST",
      body: { reason },
    });
  } catch (error) {
    return toState(error, "We could not reject this check.");
  }

  revalidateQueues();

  return { done: true };
}

export async function approveCertificationAction(
  caregiverCertificationId: string,
): Promise<AdminFormState> {
  try {
    await apiSend(
      `/api/v1/admin/certifications/${caregiverCertificationId}/approve`,
      { method: "POST" },
    );
  } catch (error) {
    return toState(error, "We could not approve this certificate.");
  }

  revalidateQueues();

  return { done: true };
}

export async function rejectCertificationAction(
  caregiverCertificationId: string,
  _previous: AdminFormState,
  formData: FormData,
): Promise<AdminFormState> {
  const reason = String(formData.get("reason") ?? "").trim();

  if (!reason) {
    return { error: "Say why, so the caregiver knows what to fix." };
  }

  try {
    await apiSend(
      `/api/v1/admin/certifications/${caregiverCertificationId}/reject`,
      { method: "POST", body: { reason } },
    );
  } catch (error) {
    return toState(error, "We could not reject this certificate.");
  }

  revalidateQueues();

  return { done: true };
}

/**
 * Clears one insurance policy. GoSit verifies a caregiver's own coverage here, never
 * issuing or administering it — see docs/BUSINESS_MODEL.md.
 */
export async function approveInsuranceAction(
  caregiverInsurancePolicyId: string,
): Promise<AdminFormState> {
  try {
    await apiSend(`/api/v1/admin/insurance/${caregiverInsurancePolicyId}/approve`, {
      method: "POST",
    });
  } catch (error) {
    return toState(error, "We could not approve this policy.");
  }

  revalidateQueues();

  return { done: true };
}

export async function rejectInsuranceAction(
  caregiverInsurancePolicyId: string,
  _previous: AdminFormState,
  formData: FormData,
): Promise<AdminFormState> {
  const reason = String(formData.get("reason") ?? "").trim();

  if (!reason) {
    return { error: "Say why, so the caregiver knows what to fix." };
  }

  try {
    await apiSend(`/api/v1/admin/insurance/${caregiverInsurancePolicyId}/reject`, {
      method: "POST",
      body: { reason },
    });
  } catch (error) {
    return toState(error, "We could not reject this policy.");
  }

  revalidateQueues();

  return { done: true };
}

export async function closeIncidentAction(
  reportId: string,
  dismiss: boolean,
  _previous: AdminFormState,
  formData: FormData,
): Promise<AdminFormState> {
  const notes = String(formData.get("notes") ?? "").trim();

  if (!notes) {
    return { error: "Record what was done, so the decision can be traced later." };
  }

  try {
    await apiSend(`/api/v1/admin/incidents/${reportId}/close`, {
      method: "POST",
      body: { notes, dismiss },
    });
  } catch (error) {
    return toState(error, "We could not close this report.");
  }

  revalidateQueues();

  return { done: true };
}

/**
 * Suspends an account. This signs them out everywhere by revoking their tokens, and
 * takes a caregiver out of search immediately.
 */
export async function suspendAccountAction(
  userId: string,
  _previous: AdminFormState,
  formData: FormData,
): Promise<AdminFormState> {
  const reason = String(formData.get("reason") ?? "").trim();

  if (!reason) {
    return { error: "A suspension has to have a recorded reason." };
  }

  try {
    await apiSend(`/api/v1/admin/users/${userId}/suspend`, {
      method: "POST",
      body: { reason },
    });
  } catch (error) {
    return toState(error, "We could not suspend this account.");
  }

  revalidateQueues();

  return { done: true };
}

export async function reinstateAccountAction(userId: string): Promise<AdminFormState> {
  try {
    await apiSend(`/api/v1/admin/users/${userId}/reinstate`, { method: "POST" });
  } catch (error) {
    return toState(error, "We could not reinstate this account.");
  }

  revalidateQueues();

  return { done: true };
}

/**
 * Creating a promo code.
 *
 * FormData for the same reason as every other staff action here, and because the whole
 * form arrives at once — a code is a set of terms that only make sense together.
 *
 * Every rule that decides whether the terms are coherent lives on the server. Repeating
 * them here would give faster feedback and a second place for them to drift; the API's
 * own message is shown instead, which is written for the person reading it.
 */
export async function createPromoCodeAction(
  _previous: AdminFormState,
  formData: FormData,
): Promise<AdminFormState> {
  const text = (key: string) => {
    const value = String(formData.get(key) ?? "").trim();
    return value.length > 0 ? value : null;
  };

  const number = (key: string) => {
    const value = text(key);
    return value === null ? null : Number(value);
  };

  // A date input gives a local calendar day. Sent as-is, "ends on the 30th" would end at
  // midnight *starting* the 30th and quietly lose the last day of every campaign.
  const endOfDay = (key: string) => {
    const value = text(key);
    return value === null ? null : `${value}T23:59:59.999Z`;
  };

  const startOfDay = (key: string) => {
    const value = text(key);
    return value === null ? null : `${value}T00:00:00.000Z`;
  };

  try {
    await apiFetch("/api/v1/admin/promo-codes", {
      method: "POST",
      body: {
        code: text("code"),
        description: text("description"),
        // The select carries the enum's own numbers, so the two cannot drift apart.
        discountType: number("discountType"),
        value: number("value"),
        validFromUtc: startOfDay("validFrom"),
        validUntilUtc: endOfDay("validUntil"),
        maxRedemptions: number("maxRedemptions"),
        audience: text("audience"),
      },
    });
  } catch (error) {
    return toState(error, "We could not create that promo code.");
  }

  revalidatePath("/admin/promo-codes");

  return { done: true };
}

/** Switches a code off. There is no edit and no delete — see the API feature for why. */
export async function deactivatePromoCodeAction(
  _previous: AdminFormState,
  formData: FormData,
): Promise<AdminFormState> {
  const promoCodeId = String(formData.get("promoCodeId") ?? "");

  try {
    await apiFetch(`/api/v1/admin/promo-codes/${promoCodeId}/deactivate`, { method: "POST" });
  } catch (error) {
    return toState(error, "We could not switch that promo code off.");
  }

  revalidatePath("/admin/promo-codes");

  return { done: true };
}

/**
 * Settles a dispute.
 *
 * The amount travels as FormData like every other staff decision here, and for the same
 * reason: Next's development server echoes scalar Server Action arguments to the console,
 * and a note about what went wrong in a family's home is not console material.
 */
export async function resolveDisputeAction(
  careBookingId: string,
  _previous: AdminFormState,
  formData: FormData,
): Promise<AdminFormState> {
  const refundAmount = Number(formData.get("refundAmount") ?? 0);
  const notes = String(formData.get("notes") ?? "").trim();

  if (!Number.isFinite(refundAmount) || refundAmount < 0) {
    return { error: "Enter how much goes back to the family, or zero." };
  }

  if (notes.length === 0) {
    return { error: "Say why. Both people are told the outcome." };
  }

  try {
    await apiSend(`/api/v1/admin/disputes/${careBookingId}/resolve`, {
      method: "POST",
      body: { refundAmount, notes },
    });
  } catch (error) {
    return toState(error, "We could not settle that dispute.");
  }

  revalidatePath("/admin");
  revalidatePath("/admin/disputes");

  return { done: true };
}

/**
 * Reads one flagged message.
 *
 * Kept out of the queue's own load deliberately: the API audits every call against the
 * staff member, so this fires when someone chooses to read one rather than for every row
 * on the screen. A list that fetched all the bodies up front would make "who read whose
 * private message" mean nothing.
 *
 * The message id travels as a scalar because it is an identifier, not content — the
 * caution at the top of this file is about staff notes, and the body only ever moves in
 * the other direction.
 */
export async function readFlaggedMessageAction(
  messageId: string,
): Promise<{ body?: string; error?: string }> {
  try {
    const detail = await apiFetch<{ body: string }>(
      `/api/v1/admin/flagged-messages/${messageId}`,
    );

    return { body: detail.body };
  } catch (error) {
    if (error instanceof SessionExpiredError) {
      throw error;
    }

    return { error: "We could not open that message." };
  }
}

/**
 * Settles a flag: nothing wrong, or escalated to an incident about the sender.
 *
 * The notes travel as FormData for the reason given at the top of this file — an
 * escalation note is a staff member's account of why someone's conduct warranted a
 * report, and it is not console material.
 */
export async function moderateMessageAction(
  messageId: string,
  _previous: AdminFormState,
  formData: FormData,
): Promise<AdminFormState> {
  const outcome = String(formData.get("outcome") ?? "");
  const notes = String(formData.get("notes") ?? "").trim();

  if (outcome !== "NoConcern" && outcome !== "Escalated") {
    return { error: "Choose whether this needs action." };
  }

  if (outcome === "Escalated" && notes.length === 0) {
    return { error: "Say why. This becomes a report about a real person." };
  }

  try {
    await apiSend(`/api/v1/admin/flagged-messages/${messageId}/moderate`, {
      method: "POST",
      body: { outcome, notes: notes || null },
    });
  } catch (error) {
    return toState(error, "We could not settle that flag.");
  }

  revalidatePath("/admin");
  revalidatePath("/admin/flagged-messages");

  return { done: true };
}

/**
 * Keeps an interview's recordings past their deletion date.
 *
 * The reason travels as FormData for the reason at the top of this file: it names a
 * dispute or an incident about real people.
 */
export async function holdInterviewRecordingsAction(
  interviewId: string,
  _previous: AdminFormState,
  formData: FormData,
): Promise<AdminFormState> {
  const reason = String(formData.get("reason") ?? "").trim();

  if (reason.length === 0) {
    return { error: "Say why these need to be kept — a dispute, an incident, a request." };
  }

  try {
    await apiSend(`/api/v1/admin/interviews/${interviewId}/hold`, {
      method: "POST",
      body: { reason },
    });
  } catch (error) {
    return toState(error, "We could not put these recordings on hold.");
  }

  revalidatePath("/admin/interviews");

  return { done: true };
}

/** Lifts a hold. Recordings already past their date are deleted on the next sweep. */
export async function releaseInterviewRecordingsAction(interviewId: string): Promise<AdminFormState> {
  try {
    await apiSend(`/api/v1/admin/interviews/${interviewId}/release`, { method: "POST" });
  } catch (error) {
    return toState(error, "We could not release these recordings.");
  }

  revalidatePath("/admin/interviews");

  return { done: true };
}

/**
 * Takes a reported review down, or lets it stand.
 *
 * Hiding removes it from the caregiver's profile and from their rating. It never edits or
 * deletes the review: the words are evidence about their author as much as their subject,
 * and a decision that erased what it was made about could not be reviewed afterwards.
 *
 * The reason travels as FormData for the reason at the top of this file — it is a staff
 * member's account of why someone's words were removed.
 */
export async function moderateReviewAction(
  reviewId: string,
  hide: boolean,
  _previous: AdminFormState,
  formData: FormData,
): Promise<AdminFormState> {
  const reason = String(formData.get("reason") ?? "").trim();

  if (hide && reason.length === 0) {
    return { error: "Say why. Removing someone's words needs a recorded reason." };
  }

  try {
    await apiSend(`/api/v1/admin/reported-reviews/${reviewId}/moderate`, {
      method: "POST",
      body: { hide, reason: reason || null },
    });
  } catch (error) {
    return toState(error, "We could not settle that report.");
  }

  revalidatePath("/admin");
  revalidatePath("/admin/reported-reviews");

  return { done: true };
}

/**
 * Closes a contact-form message: answered, or not a real enquiry. Both stay on record, so
 * a run of spam is visible rather than quietly deleted.
 */
export async function closeEnquiryAction(
  enquiryId: string,
  outcome: "handled" | "spam",
): Promise<AdminFormState> {
  try {
    await apiSend(`/api/v1/admin/enquiries/${enquiryId}/${outcome}`, { method: "POST" });
  } catch (error) {
    return toState(error, "We could not update this message.");
  }

  revalidatePath("/admin/enquiries");

  return { done: true };
}
