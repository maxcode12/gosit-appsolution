import "server-only";

import { cookies } from "next/headers";

/**
 * Tokens live in httpOnly cookies, so page scripts cannot read them and an XSS bug
 * cannot exfiltrate a session. Nothing in this module may be imported by a Client
 * Component — the "server-only" import makes that a build error rather than a leak.
 */

const ACCESS_TOKEN_COOKIE = "gosit_at";
const REFRESH_TOKEN_COOKIE = "gosit_rt";

/**
 * Which side of the marketplace this account is on, kept only so pages can be routed to
 * the right home without a round trip. It is never an authorisation decision — the API
 * enforces roles on every request, and a tampered cookie buys nothing but the wrong
 * navigation rail.
 */
const ROLE_COOKIE = "gosit_role";

export interface SessionTokens {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  userId: string;
  role: string;
}

const baseCookieOptions = {
  httpOnly: true,
  sameSite: "lax",
  secure: process.env.NODE_ENV === "production",
  path: "/",
} as const;

export async function createSession(tokens: SessionTokens): Promise<void> {
  const cookieStore = await cookies();

  cookieStore.set(ACCESS_TOKEN_COOKIE, tokens.accessToken, {
    ...baseCookieOptions,
    expires: new Date(tokens.accessTokenExpiresAtUtc),
  });

  // Deliberately no expiry hint from the server: the refresh token's real lifetime is
  // enforced by the API, and a session cookie is discarded when the browser closes.
  cookieStore.set(REFRESH_TOKEN_COOKIE, tokens.refreshToken, baseCookieOptions);

  cookieStore.set(ROLE_COOKIE, tokens.role, baseCookieOptions);
}

export type SessionRole = "CareRequester" | "Caregiver" | "Admin";

/** The signed-in account's role, for routing only. Undefined when signed out. */
export async function getSessionRole(): Promise<SessionRole | undefined> {
  const value = (await cookies()).get(ROLE_COOKIE)?.value;

  return value === "CareRequester" || value === "Caregiver" || value === "Admin"
    ? value
    : undefined;
}

/** Where an account belongs after signing in. */
export function homePathFor(role: SessionRole | undefined): string {
  if (role === "Caregiver") return "/caregiver";
  if (role === "Admin") return "/admin";

  return "/dashboard";
}

export async function getAccessToken(): Promise<string | undefined> {
  return (await cookies()).get(ACCESS_TOKEN_COOKIE)?.value;
}

export async function getRefreshToken(): Promise<string | undefined> {
  return (await cookies()).get(REFRESH_TOKEN_COOKIE)?.value;
}

export async function clearSession(): Promise<void> {
  const cookieStore = await cookies();
  cookieStore.delete(ACCESS_TOKEN_COOKIE);
  cookieStore.delete(REFRESH_TOKEN_COOKIE);
  cookieStore.delete(ROLE_COOKIE);
}
