"use server";

import { redirect } from "next/navigation";
import { env } from "@/lib/env";
import {
  clearSession,
  createSession,
  getRefreshToken,
  homePathFor,
  type SessionRole,
  type SessionTokens,
} from "@/lib/auth/session";

/**
 * Server actions for the auth screens. Credentials go straight from this server
 * process to the API and are never handed to the browser; only httpOnly session
 * cookies come back. Validation here mirrors the API's rules for fast feedback —
 * the API remains the authority.
 */

export interface AuthFormState {
  error?: string;
  fieldErrors?: Record<string, string>;
}

/** Kept in step with RegisterValidator.MinimumPasswordLength on the server. */
const MINIMUM_PASSWORD_LENGTH = 12;

interface ProblemDetails {
  title?: string;
  errors?: Record<string, string[]>;
}

async function readProblem(response: Response): Promise<ProblemDetails> {
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return {};
  }
}

function toFieldErrors(problem: ProblemDetails): Record<string, string> {
  const fieldErrors: Record<string, string> = {};

  for (const [key, messages] of Object.entries(problem.errors ?? {})) {
    // The API reports command property names ("Email"); the form uses camelCase.
    const field = key.charAt(0).toLowerCase() + key.slice(1);
    if (messages.length > 0) {
      fieldErrors[field] = messages[0];
    }
  }

  return fieldErrors;
}

/** Stores the session and reports where this account belongs. */
/**
 * Stores the session and reports where this account belongs.
 *
 * A postal code typed on the landing page is carried through to the search that follows.
 * Losing it would make the box on the front page decorative — someone types where they
 * live, signs up, and lands on an unfiltered list as though they had never said.
 *
 * Only for a care requester: a caregiver's home is their own dashboard, which has no
 * search to seed.
 */
async function startSession(
  response: Response,
  postalCode?: string,
): Promise<string> {
  const tokens = (await response.json()) as SessionTokens;
  await createSession(tokens);

  const role = tokens.role as SessionRole;
  const home = homePathFor(role);

  if (role !== "CareRequester" || !postalCode) {
    return home;
  }

  // A radius with it, so the first search is "near what I typed", not "everyone sharing
  // that postal area".
  return `${home}?postalCode=${encodeURIComponent(postalCode)}&radiusKm=25`;
}

/**
 * A Canadian postal code, or nothing.
 *
 * Validated rather than passed along: this value arrives from a URL, goes into a
 * redirect, and would otherwise be a way to put arbitrary text into one.
 */
function toPostalCode(value: FormDataEntryValue | null): string | undefined {
  const cleaned = String(value ?? "").trim().toUpperCase();

  // Six characters, or just the first three: the website's "caregivers near you" box works
  // at postal-area level, and someone who typed "M5V" there should arrive with it.
  return /^[A-Z]\d[A-Z]( ?\d[A-Z]\d)?$/.test(cleaned) ? cleaned : undefined;
}

export async function loginAction(
  _previous: AuthFormState,
  formData: FormData,
): Promise<AuthFormState> {
  const email = String(formData.get("email") ?? "").trim();
  const password = String(formData.get("password") ?? "");

  if (!email || !password) {
    return { error: "Enter your email address and password." };
  }

  const response = await fetch(`${env.apiBaseUrl}/api/v1/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
    cache: "no-store",
  });

  if (!response.ok) {
    const problem = await readProblem(response);

    if (response.status === 429) {
      return { error: "Too many attempts. Wait a minute and try again." };
    }

    return {
      error: problem.title ?? "The email address or password is incorrect.",
      fieldErrors: toFieldErrors(problem),
    };
  }

  redirect(await startSession(response));
}

export async function registerAction(
  _previous: AuthFormState,
  formData: FormData,
): Promise<AuthFormState> {
  const payload = {
    firstName: String(formData.get("firstName") ?? "").trim(),
    lastName: String(formData.get("lastName") ?? "").trim(),
    email: String(formData.get("email") ?? "").trim(),
    password: String(formData.get("password") ?? ""),
    role: String(formData.get("role") ?? "CareRequester"),
  };

  const fieldErrors: Record<string, string> = {};
  if (!payload.firstName) fieldErrors.firstName = "Enter your first name.";
  if (!payload.lastName) fieldErrors.lastName = "Enter your last name.";
  if (!payload.email) fieldErrors.email = "Enter your email address.";
  if (payload.password.length < MINIMUM_PASSWORD_LENGTH) {
    fieldErrors.password = `Use at least ${MINIMUM_PASSWORD_LENGTH} characters.`;
  }

  if (Object.keys(fieldErrors).length > 0) {
    return { fieldErrors };
  }

  const response = await fetch(`${env.apiBaseUrl}/api/v1/auth/register`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
    cache: "no-store",
  });

  if (!response.ok) {
    const problem = await readProblem(response);

    if (response.status === 409) {
      return { fieldErrors: { email: "An account with that email already exists." } };
    }

    if (response.status === 429) {
      return { error: "Too many attempts. Wait a minute and try again." };
    }

    return {
      error: problem.title ?? "We could not create your account.",
      fieldErrors: toFieldErrors(problem),
    };
  }

  const destination = await startSession(response, toPostalCode(formData.get("postalCode")));

  // A new caregiver has only given the basics; the setup checklist is where the rest happens.
  redirect(destination === homePathFor("Caregiver") ? "/caregiver/setup" : destination);
}

export async function logoutAction(): Promise<void> {
  const refreshToken = await getRefreshToken();

  if (refreshToken) {
    // Best effort: the session cookies are cleared regardless, so a failure here
    // cannot leave the browser holding a usable session.
    await fetch(`${env.apiBaseUrl}/api/v1/auth/logout`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
      cache: "no-store",
    }).catch(() => undefined);
  }

  await clearSession();
  redirect("/login");
}
