"use server";

import { env } from "@/lib/env";

/**
 * Account recovery.
 *
 * These call the API without a session — someone resetting a password is by definition
 * locked out — so they use fetch directly rather than the authenticated client.
 *
 * <b>The response never says whether an account exists.</b> The API is careful about
 * that, and it would be undone here by showing "no account with that address": the
 * confirmation is deliberately worded to be true either way.
 */

export interface RecoveryFormState {
  error?: string;
  fieldErrors?: Record<string, string>;
  done?: boolean;
}

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
    const field = key.charAt(0).toLowerCase() + key.slice(1);

    if (messages.length > 0) {
      fieldErrors[field] = messages[0];
    }
  }

  return fieldErrors;
}

async function post(path: string, body: unknown): Promise<Response> {
  return fetch(`${env.apiBaseUrl}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    cache: "no-store",
  });
}

export async function requestPasswordResetAction(
  _previous: RecoveryFormState,
  formData: FormData,
): Promise<RecoveryFormState> {
  const email = String(formData.get("email") ?? "").trim();

  if (!email) {
    return { fieldErrors: { email: "Enter your email address." } };
  }

  const response = await post("/api/v1/auth/password-reset", { email });

  if (response.status === 429) {
    return { error: "Too many attempts. Wait a minute and try again." };
  }

  if (!response.ok) {
    const problem = await readProblem(response);

    return {
      error: problem.title ?? "We could not send that email.",
      fieldErrors: toFieldErrors(problem),
    };
  }

  // Success whatever the address was. See the module remarks.
  return { done: true };
}

export async function resetPasswordAction(
  token: string,
  _previous: RecoveryFormState,
  formData: FormData,
): Promise<RecoveryFormState> {
  const newPassword = String(formData.get("newPassword") ?? "");
  const confirmPassword = String(formData.get("confirmPassword") ?? "");

  if (newPassword !== confirmPassword) {
    return { fieldErrors: { confirmPassword: "These do not match." } };
  }

  const response = await post("/api/v1/auth/password-reset/confirm", {
    token,
    newPassword,
  });

  if (response.status === 429) {
    return { error: "Too many attempts. Wait a minute and try again." };
  }

  if (!response.ok) {
    const problem = await readProblem(response);
    const fieldErrors = toFieldErrors(problem);

    return {
      // The API says the same thing for wrong, expired and already-used, and this
      // passes that through rather than guessing at something more specific.
      error: fieldErrors.token ?? problem.title ?? "We could not reset your password.",
      fieldErrors,
    };
  }

  return { done: true };
}

/**
 * Redeems a verification link.
 *
 * Called from the page rather than a form: someone arriving from their mailbox has
 * already expressed intent by clicking, and asking them to press a second button to
 * confirm that they meant it is a pointless step.
 */
export async function confirmEmailAction(token: string): Promise<boolean> {
  const response = await post("/api/v1/auth/email-verification/confirm", { token });

  return response.ok;
}

/** Sends a fresh verification link to the signed-in account. */
export async function resendVerificationAction(
  accessToken: string,
): Promise<RecoveryFormState> {
  const response = await fetch(`${env.apiBaseUrl}/api/v1/auth/email-verification`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}` },
    cache: "no-store",
  });

  if (!response.ok) {
    return { error: "We could not send a new link. Try again in a moment." };
  }

  return { done: true };
}
