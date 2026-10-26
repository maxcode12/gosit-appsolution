/**
 * Public, brochure-only build of the marketing site.
 *
 * Sign-up, log-in and search all need the API, and sign-up is closed. When this is on
 * (NEXT_PUBLIC_MARKETING_ONLY=true at build time), every route into an account is replaced
 * with the contact form instead of a page that cannot work. It is a build flag rather than
 * a code deletion so that turning the real app on is one setting, not a revert. See
 * scripts/build-marketing-site.mjs.
 */
export const MARKETING_ONLY = process.env.NEXT_PUBLIC_MARKETING_ONLY === "true";

/** Where the contact form lives on the page. */
export const CONTACT_ANCHOR = "#contact";

/**
 * The API the contact form posts to. The public site is static files, so the browser calls
 * the API directly; the API allows this website's address and no other.
 */
export const PUBLIC_API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5199";
