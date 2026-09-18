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

export function proxy(request: NextRequest) {
  const role = request.cookies.get("gosit_role")?.value;

  // Signed out, or a role this does not route on: let the page decide.
  if (!ROLES.includes(role as Role)) {
    return NextResponse.next();
  }

  const area = areaOf(request.nextUrl.pathname);

  if (area && area !== role) {
    return NextResponse.redirect(new URL(homeFor(role as Role), request.url));
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

    return NextResponse.redirect(new URL(destination, request.url));
  }

  return NextResponse.next();
}

/**
 * Add a new top-level route for any side to both `AREAS` above and this matcher.
 * The route-group layouts repeat the check as a backstop for exactly that oversight —
 * they cannot prevent the wasted fetch, but they do prevent the wrong section showing.
 */
export const config = {
  matcher: [
    "/caregiver/:path*",
    "/admin/:path*",
    "/dashboard/:path*",
    "/saved/:path*",
    "/bookings/:path*",
    "/messages/:path*",
    "/account/:path*",
    "/caregivers/:path*",
  ],
};
