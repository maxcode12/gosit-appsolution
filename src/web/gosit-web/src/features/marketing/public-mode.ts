/**
 * Whether this build is the public brochure site (plain static files, no accounts) rather
 * than the full web app. Only `scripts/build-marketing-site.mjs` sets the flag.
 */
export const MARKETING_ONLY = process.env.NEXT_PUBLIC_MARKETING_ONLY === "true";

export const PUBLIC_API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5199";

export type Audience = "family" | "caregiver";

/**
 * Where "Get started" goes.
 *
 * In the full app that is the real sign-up form, pre-set to the right kind of account. On the
 * brochure build there is no sign-up to send anyone to (the API keeps registration closed
 * until launch), so it goes to a page that takes their details for the early list instead.
 * Every button on the site goes through here, so opening sign-up later is a build flag, not
 * a hunt through the pages.
 */
export function getStartedHref(audience?: Audience): string {
  const query = audience ? `?${MARKETING_ONLY ? "as" : "type"}=${audience}` : "";

  return `${MARKETING_ONLY ? "/get-started" : "/signup"}${query}`;
}
