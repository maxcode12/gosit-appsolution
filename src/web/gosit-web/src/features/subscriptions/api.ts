import "server-only";

import { apiFetch } from "@/lib/api/client";
import type { MySubscription, Plan } from "@/features/subscriptions/types";

/**
 * Plans and the caller's own subscription.
 *
 * The plan list is already filtered to the caller's side of the marketplace by the API,
 * so a family never sees a caregiver plan and vice versa.
 */
export function listPlans(): Promise<Plan[]> {
  return apiFetch<Plan[]>("/api/v1/subscription-plans");
}

export function getMySubscription(): Promise<MySubscription> {
  return apiFetch<MySubscription>("/api/v1/me/subscription");
}
