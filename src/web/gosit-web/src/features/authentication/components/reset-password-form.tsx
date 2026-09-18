"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { TextInput } from "@/components/ui/text-input";
import {
  resetPasswordAction,
  type RecoveryFormState,
} from "@/features/authentication/recovery-actions";

/**
 * Chooses a new password against a reset token.
 *
 * The token comes from the URL and is bound to the action server-side rather than put in
 * a hidden field. It never needs to travel back through the form, and a bearer credential
 * that can take over an account has no business sitting in the DOM where an extension or
 * an injected script could read it.
 */
export function ResetPasswordForm({ token }: { token: string }) {
  const [state, formAction] = useActionState<RecoveryFormState, FormData>(
    resetPasswordAction.bind(null, token),
    {},
  );

  if (state.done) {
    return (
      <div className="text-center">
        <p className="text-text-primary">Your password has been changed.</p>
        <p className="mt-3 text-sm leading-relaxed text-text-secondary">
          You have been signed out everywhere else, so anyone who was in your account is
          not any more.
        </p>
        <Link
          href="/login"
          className="mt-6 inline-block rounded-control bg-brand-primary px-6 py-3 font-control text-sm font-semibold text-text-on-brand"
        >
          Log in
        </Link>
      </div>
    );
  }

  return (
    <form action={formAction} className="flex flex-col gap-5" noValidate>
      {state.error ? (
        <p
          role="alert"
          className="rounded-control border border-danger-accent/20 bg-danger-soft px-4 py-3 text-sm text-danger-accent"
        >
          {state.error}{" "}
          <Link href="/forgot-password" className="font-semibold underline">
            Ask for a new link
          </Link>
          .
        </p>
      ) : null}

      <TextInput
        label="New password"
        name="newPassword"
        type="password"
        autoComplete="new-password"
        placeholder="••••••••••••"
        required
        autoFocus
        hint="At least 12 characters."
        error={state.fieldErrors?.newPassword}
      />

      <TextInput
        label="Confirm new password"
        name="confirmPassword"
        type="password"
        autoComplete="new-password"
        placeholder="••••••••••••"
        required
        error={state.fieldErrors?.confirmPassword}
      />

      <SubmitButton />
    </form>
  );
}

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" size="lg" disabled={pending} className="w-full">
      {pending ? "Saving…" : "Set my new password"}
    </Button>
  );
}
