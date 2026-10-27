"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { TextInput } from "@/components/ui/text-input";
import { loginAction, type AuthFormState } from "@/features/authentication/actions";

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" size="lg" disabled={pending} className="mt-2 w-full">
      {pending ? "Logging in…" : "Log in"}
    </Button>
  );
}

export function LoginForm() {
  const [state, formAction] = useActionState<AuthFormState, FormData>(loginAction, {});

  return (
    <form action={formAction} className="flex flex-col gap-4" noValidate>
      {state.error ? (
        <p
          role="alert"
          className="rounded-control border border-danger-accent/20 bg-danger-soft px-4 py-3 text-sm text-danger-accent"
        >
          {state.error}
        </p>
      ) : null}

      {/*
        Labels are shown rather than replaced by placeholders. A placeholder that stands
        in for a label disappears the moment someone starts typing, which is exactly when
        they might want to check what the field was for.
      */}
      <TextInput
        label="Email address"
        name="email"
        type="email"
        autoComplete="email"
        placeholder="you@example.com"
        required
        error={state.fieldErrors?.email}
      />

      <div>
        <TextInput
          label="Password"
          name="password"
          type="password"
          autoComplete="current-password"
          placeholder="••••••••••••"
          required
          error={state.fieldErrors?.password}
        />
        <div className="mt-2 text-right">
          <Link
            href="/forgot-password"
            className="font-control text-sm font-medium text-brand-primary hover:underline"
          >
            Forgot password?
          </Link>
        </div>
      </div>

      <SubmitButton />

      <p className="text-center text-sm text-text-secondary">
        New to GoSit?{" "}
        <Link href="/signup" className="font-semibold text-brand-primary hover:underline">
          Create an account
        </Link>
      </p>
    </form>
  );
}
