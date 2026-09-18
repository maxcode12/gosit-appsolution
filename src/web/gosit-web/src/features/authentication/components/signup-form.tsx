"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { TextInput } from "@/components/ui/text-input";
import { AccountTypeChoice } from "@/features/authentication/components/account-type-choice";
import { registerAction, type AuthFormState } from "@/features/authentication/actions";

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" size="lg" disabled={pending} className="mt-2 w-full">
      {pending ? "Creating your account…" : "Sign up for free"}
    </Button>
  );
}

/**
 * Signing up.
 *
 * `postalCode` rides along in a hidden field when someone arrived from the landing
 * page having already typed where they live. Carrying it means the search they land on
 * is the one they asked for; dropping it would make the box on the front page
 * decorative. The action validates it before putting it anywhere near a redirect.
 */
export function SignupForm({ postalCode }: { postalCode?: string }) {
  const [state, formAction] = useActionState<AuthFormState, FormData>(registerAction, {});

  return (
    <form action={formAction} className="flex flex-col gap-5" noValidate>
      {postalCode ? <input type="hidden" name="postalCode" value={postalCode} /> : null}
      {state.error ? (
        <p
          role="alert"
          className="rounded-control border border-danger-accent/20 bg-danger-soft px-4 py-3 text-sm text-danger-accent"
        >
          {state.error}
        </p>
      ) : null}

      <AccountTypeChoice />

      <div className="flex flex-col gap-5 sm:flex-row">
        <TextInput
          label="First name"
          name="firstName"
          autoComplete="given-name"
          placeholder="Ada"
          required
          error={state.fieldErrors?.firstName}
        />
        <TextInput
          label="Last name"
          name="lastName"
          autoComplete="family-name"
          placeholder="Lovelace"
          required
          error={state.fieldErrors?.lastName}
        />
      </div>

      <TextInput
        label="Email address"
        name="email"
        type="email"
        autoComplete="email"
        placeholder="you@example.com"
        required
        error={state.fieldErrors?.email}
      />

      <TextInput
        label="Password"
        name="password"
        type="password"
        autoComplete="new-password"
        placeholder="••••••••••••"
        required
        hint="At least 12 characters."
        error={state.fieldErrors?.password}
      />

      <SubmitButton />

      <p className="text-center text-sm text-text-secondary">
        Already have an account?{" "}
        <Link href="/login" className="font-semibold text-brand-primary hover:underline">
          Log in
        </Link>
      </p>

      <p className="border-t border-border pt-5 text-center text-xs leading-relaxed text-text-muted">
        By registering you agree to the{" "}
        <Link href="/terms" className="underline hover:text-text-secondary">
          Terms &amp; Conditions
        </Link>{" "}
        and the{" "}
        <Link href="/privacy" className="underline hover:text-text-secondary">
          Privacy Policy
        </Link>
        .
      </p>
    </form>
  );
}
