"use client";

import { useActionState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import {
  acceptServiceAgreementAction,
  type CaregiverFormState,
} from "@/features/caregivers/actions";
import type { CaregiverProfile } from "@/features/caregivers/types";

/**
 * Electronically signing the Caregiver Service Agreement.
 *
 * A typed full legal name, recorded against whichever version is current on the server —
 * never one the browser names, so this cannot be used to keep an old signature alive past
 * a real revision. The agreement itself is still a draft awaiting legal review (see
 * docs/BUSINESS_MODEL.md), so this summarizes it rather than presenting it as finished —
 * the full text is not published anywhere in the app yet.
 */
export function ServiceAgreementPanel({ profile }: { profile: CaregiverProfile }) {
  const [state, formAction] = useActionState<CaregiverFormState, FormData>(
    acceptServiceAgreementAction,
    {},
  );

  if (profile.hasAcceptedCurrentAgreement) {
    return (
      <div className="rounded-card border border-border bg-surface p-4">
        <div className="flex items-center gap-2">
          <span aria-hidden className="size-2.5 rounded-full bg-brand-primary" />
          <p className="font-heading text-sm font-medium text-text-primary">Signed</p>
        </div>
        <p className="mt-1 text-sm text-text-secondary">
          Signed {formatSignedDate(profile.agreementAcceptedAtUtc)}.
        </p>
      </div>
    );
  }

  const isRevision = profile.agreementAcceptedVersion !== null;

  return (
    <form action={formAction} className="flex flex-col gap-4 rounded-card border border-border bg-surface p-5">
      {isRevision ? (
        <p className="rounded-control bg-accent-soft px-4 py-3 text-sm text-text-primary">
          This agreement has changed since you last signed it. Sign again to stay listed.
        </p>
      ) : null}

      <div>
        <h3 className="font-heading text-base font-medium text-text-primary">
          Caregiver Service Agreement
        </h3>
        <p className="mt-1 text-sm text-text-secondary">
          You provide care as an independent caregiver — GoSit is the marketplace that
          connects you with families, not your employer. You maintain your own insurance
          where a service requires it, and you agree to GoSit&apos;s Code of Conduct. This is
          required before families can find you.
        </p>
      </div>

      {state.error ? (
        <p role="alert" className="rounded-control bg-red-50 px-4 py-3 text-sm text-red-800">
          {state.error}
        </p>
      ) : null}

      <div>
        <label htmlFor="signedName" className="font-control text-sm text-text-secondary">
          Type your full legal name to sign
        </label>
        <input
          id="signedName"
          name="signedName"
          type="text"
          required
          maxLength={200}
          autoComplete="name"
          placeholder="Ada Lovelace"
          className="mt-1 h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
        />
        {state.fieldErrors?.signedName ? (
          <p className="mt-1 text-sm text-red-700">{state.fieldErrors.signedName}</p>
        ) : null}
      </div>

      <div>
        <SignButton />
      </div>
    </form>
  );
}

function formatSignedDate(iso: string | null): string {
  if (!iso) {
    return "";
  }

  return new Date(iso).toLocaleDateString("en-CA", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

function SignButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" disabled={pending}>
      {pending ? "Signing…" : "Sign agreement"}
    </Button>
  );
}
