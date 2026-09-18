import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { env } from "@/lib/env";
import {
  clearSession,
  createSession,
  getRefreshToken,
  type SessionTokens,
} from "@/lib/auth/session";

/**
 * Exchanges the refresh token for a new access token, then continues to wherever the
 * person was going.
 *
 * This exists because of a constraint rather than a preference: cookies cannot be
 * written while a Server Component renders, so nothing on a page can renew a session.
 * A Route Handler can, and `proxy.ts` sends expired sessions here before any page
 * renders. Without it the access cookie simply expires after fifteen minutes and every
 * signed-in screen fails — which is exactly what it used to do.
 *
 * The redirect target is required to be a path on this site. `next` arrives in a URL,
 * so it is attacker-controllable, and an open redirect out of an authentication step is
 * how a convincing credential-phishing link gets built.
 */

/** Only a path on this origin. Rejects absolute URLs and protocol-relative `//host`. */
function safeNext(value: string | null): string {
  if (!value || !value.startsWith("/") || value.startsWith("//")) {
    return "/";
  }

  return value;
}

async function signOut(request: NextRequest): Promise<NextResponse> {
  await clearSession();

  return NextResponse.redirect(new URL("/login", request.url));
}

export async function GET(request: NextRequest): Promise<NextResponse> {
  const next = safeNext(request.nextUrl.searchParams.get("next"));
  const refreshToken = await getRefreshToken();

  if (!refreshToken) {
    return signOut(request);
  }

  let response: Response;

  try {
    response = await fetch(`${env.apiBaseUrl}/api/v1/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
      cache: "no-store",
    });
  } catch {
    // The API is unreachable. Signing out is wrong — the session may be perfectly
    // valid — but there is nothing to render either, so send them to log in rather
    // than loop back into a page that cannot load. The cookies are left alone.
    return NextResponse.redirect(new URL("/login", request.url));
  }

  if (!response.ok) {
    // Refused, expired, or replayed. The API has already revoked what it needed to.
    return signOut(request);
  }

  await createSession((await response.json()) as SessionTokens);

  return NextResponse.redirect(new URL(next, request.url));
}
