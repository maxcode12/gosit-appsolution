import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

/**
 * Keeps each side of the marketplace on its own screens.
 *
 * This runs before anything renders, which is the whole point. The same check as a
 * layout redirect would do, except a layout renders *in parallel* with the page beneath
 * it — so the page's data fetches still fire and throw on the wrong role's endpoints,
 * racing the redirect that was already on its way. Doing it here means the request never
 * reaches the page at all.
 *
 * Navigation, not authorisation. The API enforces roles on every request; a forged role
 * cookie buys nothing but the wrong navigation rail. Nothing here is trusted for access
 * control, which is why it is safe to decide from a cookie without calling the API.
 */

/** Which area each path prefix belongs to. */
const AREAS = {
  Caregiver: ["/caregiver"],
  Admin: ["/admin"],
  CareRequester: [
    "/dashboard",
    "/saved",
    "/bookings",
    "/messages",
    "/account",
    // A caregiver's public profile is a family-facing screen whose path merely starts
    // with the same letters as the caregiver area. Segment matching keeps them apart.
    "/caregivers",
  ],
} as const;

type Role = keyof typeof AREAS;

const ROLES = Object.keys(AREAS) as Role[];

function areaOf(pathname: string): Role | undefined {
  return ROLES.find((role) =>
    AREAS[role].some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`)),
  );
}

function homeFor(role: Role): string {
  return AREAS[role][0];
}

/**
 * A link Next.js is fetching ahead of time because it is on screen, not a person
 * arriving. Refreshing on one of these would rotate the refresh token for a page nobody
 * asked for, and several links in view would rotate it several times over.
 */
function isPrefetch(request: NextRequest): boolean {
  return (
    request.headers.get("next-router-prefetch") !== null
    || request.headers.get("purpose") === "prefetch"
  );
}

function routeByRole(request: NextRequest, to: (path: string) => URL) {
  const role = request.cookies.get("gosit_role")?.value;

  // Signed out, or a role this does not route on: let the page decide.
  if (!ROLES.includes(role as Role)) {
    return NextResponse.next();
  }

  const area = areaOf(request.nextUrl.pathname);

  if (area && area !== role) {
    return NextResponse.redirect(to(homeFor(role as Role)));
  }

  // The access cookie carries the token's own expiry, so the browser drops it the moment
  // the token dies — fifteen minutes in. The refresh cookie outlives it, and until now
  // nothing ever spent it: every signed-in screen simply started throwing
  // SessionExpiredError, because a Server Component cannot write the cookies a renewal
  // produces.
  //
  // It has to be caught here. The signed-in layout does check for a token, but a layout
  // renders *in parallel* with the page beneath it, so the page's own fetches fire and
  // throw while that redirect is still on its way — the same race this file exists to
  // avoid for role routing.
  if (!request.cookies.has("gosit_at") && !isPrefetch(request)) {
    const destination = request.cookies.has("gosit_rt")
      ? `/session/refresh?next=${encodeURIComponent(
          request.nextUrl.pathname + request.nextUrl.search,
        )}`
      : "/login";

    return NextResponse.redirect(to(destination));
  }

  return NextResponse.next();
}

/**
 * One server, two front doors.
 *
 * app.gosits.com is where families and caregivers sign in; admin.gosits.com is where staff
 * work. Both are this same server, told apart by the Host header, so the two sites share
 * one deployment but never share a screen: staff screens are not reachable on the families'
 * address and the reverse. Locally neither variable is set and none of this applies.
 *
 * Again navigation, not authorisation — the API refuses a family member's token on every
 * admin endpoint whatever address they typed.
 */
const APP_HOST = process.env.APP_HOSTNAME?.toLowerCase();
const ADMIN_HOST = process.env.ADMIN_HOSTNAME?.toLowerCase();
const MARKETING_ORIGIN = process.env.MARKETING_ORIGIN ?? "https://www.gosits.com";

type Door = "app" | "admin";

const SIGNED_OUT_PAGES = ["/login", "/forgot-password", "/reset-password", "/verify-email"];
const SHARED_PATHS = ["/session", "/documents", "/recordings"];
const MARKETING_PATHS = [
  "/about", "/contact", "/faq", "/for-caregivers", "/for-families",
  "/get-started", "/how-it-works", "/safety", "/services",
];

function underPath(pathname: string, prefixes: readonly string[]): boolean {
  return prefixes.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`));
}

function requestHost(request: NextRequest): string {
  const raw = request.headers.get("x-forwarded-host") ?? request.headers.get("host") ?? "";
  return raw.split(",")[0].split(":")[0].trim().toLowerCase();
}

function doorOf(request: NextRequest): Door | undefined {
  const host = requestHost(request);

  if (ADMIN_HOST && host === ADMIN_HOST) {
    return "admin";
  }

  return APP_HOST && host === APP_HOST ? "app" : undefined;
}

/** Where a request for `path` on this host should really go, or undefined to carry on. */
function hostRedirect(door: Door, request: NextRequest): string | undefined {
  const { pathname, search } = request.nextUrl;
  const role = request.cookies.get("gosit_role")?.value;

  if (door === "admin") {
    if (pathname === "/") {
      return "/admin";
    }

    // Anyone signed in as something other than staff belongs on the other address.
    if (role && role !== "Admin" && !underPath(pathname, SHARED_PATHS)) {
      return `https://${APP_HOST}${pathname === "/login" ? "/" : ""}`;
    }

    return underPath(pathname, ["/admin", ...SIGNED_OUT_PAGES, ...SHARED_PATHS])
      ? undefined
      : "/login";
  }

  if (underPath(pathname, ["/admin"])) {
    return `https://${ADMIN_HOST}${pathname}${search}`;
  }

  if (underPath(pathname, MARKETING_PATHS) || pathname === "/sitemap.xml") {
    return `${MARKETING_ORIGIN}${pathname}`;
  }

  if (pathname === "/") {
    return ROLES.includes(role as Role) ? homeFor(role as Role) : "/login";
  }

  return undefined;
}

export function proxy(request: NextRequest) {
  const door = doorOf(request);

  // Redirect targets must carry the address the visitor typed. Behind the platform's front
  // door the server itself is listening on a private address and port.
  const to = (path: string) =>
    door
      ? new URL(path, `https://${requestHost(request)}`)
      : new URL(path, request.url);

  if (door) {
    const target = hostRedirect(door, request);

    if (target) {
      return NextResponse.redirect(
        target.startsWith("https://") ? target : to(target),
      );
    }
  }

  const response = areaOf(request.nextUrl.pathname)
    ? routeByRole(request, to)
    : NextResponse.next();

  if (door) {
    // Sign-in screens and staff tools have no business in a search index.
    response.headers.set("X-Robots-Tag", "noindex, nofollow");
  }

  return response;
}

/**
 * Add a new top-level route for any side to `AREAS` above (the matcher already sees every page).
 * The route-group layouts repeat the check as a backstop for exactly that oversight —
 * they cannot prevent the wasted fetch, but they do prevent the wrong section showing.
 */
export const config = {
  // Everything except the framework's own files and static assets: the host rules above
  // have to see every page. The role routing inside only acts on the areas it lists.
  matcher: ["/((?!_next/|images/|.*\\..*).*)"],
};
