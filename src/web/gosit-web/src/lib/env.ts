/**
 * Typed access to the environment variables the web client needs.
 * Secrets must never be exposed with the NEXT_PUBLIC_ prefix.
 */
export const env = {
  /**
   * Base URL the browser would use. Only for values that are safe to ship to the client.
   */
  publicApiBaseUrl:
    process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5199",

  /**
   * Base URL used from server components and server actions. In a deployed environment
   * this is the internal address of the API, which may differ from the public one.
   */
  apiBaseUrl:
    process.env.API_BASE_URL ??
    process.env.NEXT_PUBLIC_API_BASE_URL ??
    "http://localhost:5199",

  /**
   * Stripe's publishable key. Despite the name this is not a secret — it identifies the
   * account to Stripe.js and is meant to ship to the browser — but it is still read
   * through this file rather than inline, so every client-visible variable has one place
   * that says so.
   */
  stripePublishableKey: process.env.NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY ?? null,
} as const;
