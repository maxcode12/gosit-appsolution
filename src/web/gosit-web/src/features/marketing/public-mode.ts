/**
 * Whether this build is the public brochure site (plain static files, no accounts) rather
 * than the full web app. Only `scripts/build-marketing-site.mjs` sets the flag.
 */
export const MARKETING_ONLY = process.env.NEXT_PUBLIC_MARKETING_ONLY === "true";

export const PUBLIC_API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5199";

export type Audience = "family" | "caregiver";

/** Where families and caregivers sign up and sign in. The brochure site is a different host. */
const APP_ORIGIN = process.env.NEXT_PUBLIC_APP_URL ?? "https://app.gosits.com";

/** Where the sign-in page is: on this site in the full app, on the app host from the brochure. */
export function loginHref(): string {
  return MARKETING_ONLY ? `${APP_ORIGIN}/login` : "/login";
}

/**
 * Where "Get started" goes: the real sign-up form, pre-set to the right kind of account.
 * Every button on the site goes through here. The early-access page at /get-started stays
 * for anyone who would rather leave their details than register.
 */
export function getStartedHref(audience?: Audience): string {
  const query = audience ? `?type=${audience}` : "";

  return `${MARKETING_ONLY ? APP_ORIGIN : ""}/signup${query}`;
}
