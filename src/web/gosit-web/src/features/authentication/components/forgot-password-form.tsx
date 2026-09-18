"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { TextInput } from "@/components/ui/text-input";
import {
  requestPasswordResetAction,
  type RecoveryFormState,
} from "@/features/authentication/recovery-actions";

/**
 * Asks for a reset link.
 *
 * The confirmation is worded to be true whether or not that address has an account —
 * "if there is an account", not "we've sent you an email". Saying the latter would undo
 * the API's care about not revealing who has an account, and would also be a lie to
 * anyone who mistyped their address and is now waiting for an email that will not come.
 */
export function ForgotPasswordForm() {
  const [state, formAction] = useActionState<RecoveryFormState, FormData>(
    requestPasswordResetAction,
    {},
  );

  if (state.done) {
    return (
      <div className="text-center">
        <p className="text-text-primary">
          If there is a GoSit account for that address, a reset link is on its way.
        </p>
        <p className="mt-3 text-sm leading-relaxed text-text-secondary">
          It is good for one hour. Check your spam folder if it has not arrived in a few
          minutes.
        </p>
        <Link
          href="/login"
          className="mt-6 inline-block font-control text-sm font-semibold text-brand-primary hover:underline"
        >
          Back to log in
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
          {state.error}
        </p>
      ) : null}

      <p className="text-sm leading-relaxed text-text-secondary">
        Enter the address you signed up with and we will send you a link to choose a new
        password.
      </p>

      <TextInput
        label="Email address"
        name="email"
        type="email"
        autoComplete="email"
        placeholder="you@example.com"
        required
        autoFocus
        error={state.fieldErrors?.email}
      />

      <SubmitButton />

      <p className="text-center text-sm text-text-secondary">
        Remembered it?{" "}
        <Link href="/login" className="font-semibold text-brand-primary hover:underline">
          Log in
        </Link>
      </p>
    </form>
  );
}

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" size="lg" disabled={pending} className="w-full">
      {pending ? "Sending…" : "Send me a reset link"}
    </Button>
  );
}
