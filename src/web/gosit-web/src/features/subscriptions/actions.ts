"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { ApiError, SessionExpiredError, apiFetch } from "@/lib/api/client";
import type { SubscribeResult } from "@/features/subscriptions/types";

/**
 * Changing plan.
 *
 * Two worlds, decided by the API rather than here:
 *
 * - **Billing configured.** Subscribing grants nothing. The API returns a hosted
 *   checkout URL and the browser is redirected to it; the plan becomes real only when
 *   Stripe confirms payment over a signed webhook. Nothing this client does can start
 *   a subscription.
 * - **No billing configured** — local development. The plan starts immediately and no
 *   payment is taken, which the screen states plainly rather than implying a charge.
 *
 * The response shape makes the difference impossible to miss: a checkout URL and a
 * started subscription are different fields, so neither can be mistaken for the other.
 */

export interface PlanFormState {
  error?: string;
  changed?: boolean;
  /**
   * Where to send the browser to pay. Present only when billing is configured, and it
   * means nothing has been granted yet.
   */
  checkoutUrl?: string;
}

function toState(error: unknown, fallback: string): PlanFormState {
  if (error instanceof SessionExpiredError) {
    return { error: "Your session has expired. Log in again to continue." };
  }

  if (error instanceof ApiError) {
    return { error: error.problem?.title ?? fallback };
  }

  throw error;
}

function revalidatePlans(): void {
  revalidatePath("/account/plan");
  revalidatePath("/caregiver/plan");
}

export async function changePlanAction(
  planCode: string,
  _previous: PlanFormState,
  formData: FormData,
): Promise<PlanFormState> {
  const promoCode = String(formData.get("promoCode") ?? "")
    .trim()
    .toUpperCase();

  let result: SubscribeResult;

  try {
    result = await apiFetch<SubscribeResult>("/api/v1/me/subscription", {
      method: "POST",
      body: { planCode, promoCode: promoCode || null },
    });
  } catch (error) {
    return toState(error, "We could not change your plan.");
  }

  // With billing configured nothing has been granted — the browser has to go and pay.
  // Redirecting from the action keeps the URL off the client entirely.
  if (result.checkoutUrl) {
    redirect(result.checkoutUrl);
  }

  revalidatePlans();

  return { changed: true };
}

/**
 * Opens the provider's billing portal, where someone can change their card, read
 * invoices and cancel. Redirects rather than returning the URL: it is a short-lived
 * single-use link and has no business sitting in client state.
 */
export async function openBillingPortalAction(): Promise<PlanFormState> {
  let portal: { url: string };

  try {
    portal = await apiFetch<{ url: string }>("/api/v1/me/billing-portal", {
      method: "POST",
    });
  } catch (error) {
    return toState(error, "We could not open your billing details.");
  }

  redirect(portal.url);
}

/** Stops the renewal. Whatever the plan entitles continues to the end of the period. */
export async function cancelPlanAction(): Promise<PlanFormState> {
  try {
    await apiFetch("/api/v1/me/subscription", { method: "DELETE" });
  } catch (error) {
    return toState(error, "We could not cancel your plan.");
  }

  revalidatePlans();

  return { changed: true };
}
