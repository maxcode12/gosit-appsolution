/**
 * Subscription contracts.
 *
 * `features` on the caller's own subscription is authoritative — a client gates on that
 * list, never on the plan code. Inferring entitlement from a name is how a renamed plan
 * silently grants the wrong thing.
 */

export interface Plan {
  code: string;
  name: string;
  audience: string;
  monthlyPrice: number;
  currency: string;
  isFree: boolean;
  features: string[];
}

export interface MySubscription {
  planCode: string;
  planName: string;
  status: string;
  features: string[];
  currentPeriodEndUtc: string | null;
  cancelAtPeriodEnd: boolean;
  amountCharged: number | null;
  discountApplied: number | null;
  /**
   * Whether real billing is configured. Read from the API rather than assumed — with no
   * provider a plan grants its features without any payment being taken.
   */
  billingEnabled: boolean;
  /** True when this account has a provider customer whose portal can be opened. */
  hasBillingPortal: boolean;
}

/**
 * What subscribing produced.
 *
 * Exactly one of these is set. A checkout URL means "go here and pay" and nothing has
 * been granted; a subscription means the plan started. Treating the first as the second
 * is the mistake the two-shape response exists to prevent.
 */
export interface SubscribeResult {
  checkoutUrl: string | null;
  subscription: MySubscription | null;
}

/**
 * How each feature code reads to a person.
 *
 * Every one of these is currently declared by a plan and enforced nowhere — no code
 * outside the subscriptions feature consults the entitlement service. The UI therefore
 * describes them as what the plan *includes* rather than what it unlocks, and the plan
 * screen says plainly that they are not active yet. Listing them as live benefits would
 * be selling six things that do not exist.
 */
export const FEATURE_LABELS: Record<string, string> = {
  UNLIMITED_FAVOURITES: "Save as many caregivers as you like",
  ADVANCED_SEARCH_FILTERS: "Filter by certification, language and experience",
  PRIORITY_BOOKING_REQUESTS: "Your requests are shown to caregivers first",
  PROFILE_PROMOTION: "Your profile ranks higher in search",
  CAREGIVER_INSIGHTS: "See how your profile is performing",
  REDUCED_COMMISSION: "A lower platform fee on every booking",
};

export function featureLabel(code: string): string {
  return (
    FEATURE_LABELS[code]
    ?? code
      .toLowerCase()
      .split("_")
      .map((word, index) => (index === 0 ? word.charAt(0).toUpperCase() + word.slice(1) : word))
      .join(" ")
  );
}

export function formatPrice(plan: Plan): string {
  if (plan.isFree || plan.monthlyPrice <= 0) {
    return "Free";
  }

  return plan.monthlyPrice.toLocaleString("en-CA", {
    style: "currency",
    currency: plan.currency || "CAD",
  });
}

/**
 * Feature codes, mirroring `SubscriptionFeature` on the server.
 *
 * Gating UI on a code from here rather than on a plan name means a renamed or
 * repackaged plan changes one seed row and nothing else. The server resolves and
 * enforces entitlement on every request regardless — this only decides what to show.
 */
export const SubscriptionFeature = {
  UnlimitedFavourites: "UNLIMITED_FAVOURITES",
  AdvancedSearchFilters: "ADVANCED_SEARCH_FILTERS",
  PriorityBookingRequests: "PRIORITY_BOOKING_REQUESTS",
  ProfilePromotion: "PROFILE_PROMOTION",
  CaregiverInsights: "CAREGIVER_INSIGHTS",
  ReducedCommission: "REDUCED_COMMISSION",
} as const;
