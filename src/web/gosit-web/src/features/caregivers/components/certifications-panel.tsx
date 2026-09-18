"use client";

import { useActionState, useState } from "react";
import { useFormStatus } from "react-dom";
import { humanizeCode } from "@/components/ui/badges";
import { Button } from "@/components/ui/button";
import {
  submitCertificationAction,
  type CaregiverFormState,
} from "@/features/caregivers/actions";
import type { CaregiverCertification } from "@/features/caregivers/types";

/**
 * Certifications, and adding a new one.
 *
 * As with checks, nothing here approves anything: a submitted certification confers no
 * eligibility until Trust and Safety verifies it, and the row says so. The distinction
 * matters — a caregiver who believes their First Aid is live will not understand why
 * families cannot book them for nanny care.
 */
export function CertificationsPanel({
  certifications,
  available,
}: {
  certifications: CaregiverCertification[];
  /** Codes drawn from the service catalogue's requirements. */
  available: string[];
}) {
  const [isAdding, setIsAdding] = useState(certifications.length === 0);

  const held = new Set(certifications.map((c) => c.code));
  const addable = available.filter((code) => !held.has(code));

  return (
    <div className="flex flex-col gap-4">
      {certifications.length === 0 ? (
        <p className="text-sm text-text-secondary">
          Nothing on file yet. Certifications unlock the services families can book you
          for.
        </p>
      ) : (
        <ul className="flex flex-col gap-3">
          {certifications.map((certification) => (
            <li
              key={certification.id}
              className="rounded-card border border-border bg-surface p-4"
            >
              {/*
                The pill stays on the header row beside the name. It used to sit in a
                flex row with the whole left column, so adding the consequence sentence
                below pushed it onto its own line at the bottom left, where it read as a
                stray label rather than this certificate's state.
              */}
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="text-text-primary">{certification.name}</p>
                  <p className="mt-0.5 text-sm text-text-secondary">
                    Issued {formatMonth(certification.issuedOn)}
                    {certification.expiresOn
                      ? ` · expires ${formatMonth(certification.expiresOn)}`
                      : ""}
                  </p>
                </div>

                <Status certification={certification} />
              </div>

              <Consequence certification={certification} />
            </li>
          ))}
        </ul>
      )}

      {isAdding && addable.length > 0 ? (
        <AddCertificationForm
          available={addable}
          onDone={() => setIsAdding(false)}
        />
      ) : addable.length > 0 ? (
        <div>
          <Button type="button" variant="ghost" onClick={() => setIsAdding(true)}>
            Add a certification
          </Button>
        </div>
      ) : (
        <p className="text-sm text-text-secondary">
          Everything we currently recognise is on file.
        </p>
      )}
    </div>
  );
}

/**
 * The three states that matter, and they are not the same question.
 *
 * `isValid` is the one that decides eligibility: verified *and* unexpired. A verified
 * but expired certification is shown as expired rather than cleared, because that is
 * what it means for the caregiver.
 */
const WARN_WITHIN_DAYS = 30;

/**
 * A calendar date as "Sep 2026", read field by field for the same reason as
 * <see cref="daysUntilExpiry"/>: `new Date("2026-09-01")` is UTC midnight, and formatting
 * it locally west of Greenwich prints the month before.
 */
function formatMonth(isoDate: string): string {
  const [year, month, day] = isoDate.slice(0, 10).split("-").map(Number);

  return new Date(year, month - 1, day).toLocaleDateString("en-CA", {
    month: "short",
    year: "numeric",
  });
}

/**
 * Whole days until a certificate lapses, or null when it never does.
 *
 * The expiry is a calendar date — "2026-09-10", no time and no zone — so it is read
 * field by field rather than through `new Date(...)`. That constructor treats a bare
 * date as UTC midnight, and reading it back with the local getters lands on the previous
 * day anywhere west of Greenwich: a certificate nine days out was counted as eight.
 *
 * Compared date to date rather than by elapsed milliseconds, so "expires today" does not
 * become "in 0 days" at one minute past midnight and "in 1 day" the evening before.
 */
function daysUntilExpiry(expiresOn: string | null): number | null {
  if (!expiresOn) {
    return null;
  }

  const [year, month, day] = expiresOn.slice(0, 10).split("-").map(Number);

  if (!year || !month || !day) {
    return null;
  }

  const today = new Date();

  const days = Math.round(
    (Date.UTC(year, month - 1, day)
      - Date.UTC(today.getFullYear(), today.getMonth(), today.getDate()))
      / 86_400_000,
  );

  return Number.isFinite(days) ? days : null;
}

/**
 * What a lapse costs, spelled out on the row.
 *
 * The pill says the state; this says why it matters. Which services are affected is not
 * on this contract, so it is put in the caregiver's terms rather than guessed at — the
 * expiry notification, which is computed server-side, names them.
 */
function Consequence({ certification }: { certification: CaregiverCertification }) {
  const days = daysUntilExpiry(certification.expiresOn);

  if (certification.status === "Verified" && !certification.isValid) {
    return (
      <p className="mt-1 text-sm text-danger-accent">
        Families can no longer book you for the services that require this. Upload a
        current certificate to start appearing in search again.
      </p>
    );
  }

  if (certification.isValid && days !== null && days <= WARN_WITHIN_DAYS) {
    return (
      <p className="mt-1 text-sm text-text-secondary">
        Renew it before then, or families will stop being able to book you for the
        services that require it.
      </p>
    );
  }

  return null;
}

function Status({ certification }: { certification: CaregiverCertification }) {
  if (certification.isValid) {
    const days = daysUntilExpiry(certification.expiresOn);

    /*
     * A certificate a few weeks from lapsing is still valid, and saying only "Cleared"
     * is how someone finds out by their work stopping. The countdown is the whole point:
     * "expires soon" is a fact, "in 9 days" is a deadline.
     */
    if (days !== null && days <= WARN_WITHIN_DAYS) {
      return (
        <span className="inline-flex items-center rounded-pill bg-brand-accent/15 px-3 py-1 text-xs font-semibold text-text-primary">
          {days <= 0 ? "Expires today" : `Expires in ${days} day${days === 1 ? "" : "s"}`}
        </span>
      );
    }

    return (
      <span className="inline-flex items-center rounded-pill bg-brand-primary/10 px-3 py-1 text-xs font-semibold text-brand-primary">
        Cleared
      </span>
    );
  }

  if (certification.status === "Rejected") {
    return (
      <span className="inline-flex items-center rounded-pill bg-danger-accent/10 px-3 py-1 text-xs font-semibold text-danger-accent">
        Not accepted
      </span>
    );
  }

  if (certification.status === "Verified") {
    /*
     * Was muted grey, which read as a filing state. An expired certificate has removed
     * this caregiver from search for every service requiring it — that is not a quiet
     * status, it is the reason their work stopped.
     */
    return (
      <span className="inline-flex items-center rounded-pill bg-danger-accent/10 px-3 py-1 text-xs font-semibold text-danger-accent">
        Expired
      </span>
    );
  }

  return (
    <span className="inline-flex items-center rounded-pill bg-brand-accent/15 px-3 py-1 text-xs font-semibold text-text-primary">
      In review
    </span>
  );
}

function AddCertificationForm({
  available,
  onDone,
}: {
  available: string[];
  onDone: () => void;
}) {
  const [state, formAction] = useActionState<CaregiverFormState, FormData>(
    async (previous, formData) => {
      const result = await submitCertificationAction(previous, formData);

      if (result.saved) {
        onDone();
      }

      return result;
    },
    {},
  );

  const today = new Date().toISOString().slice(0, 10);

  return (
    <form
      action={formAction}
      className="flex flex-col gap-4 rounded-card border border-border bg-surface p-4"
      noValidate
    >
      <h3 className="font-heading text-base font-medium text-text-primary">
        Add a certification
      </h3>

      {state.error ? (
        <p role="alert" className="rounded-control bg-red-50 px-4 py-3 text-sm text-red-800">
          {state.error}
        </p>
      ) : null}

      <div className="grid gap-4 sm:grid-cols-3">
        <div>
          <label
            htmlFor="certificationCode"
            className="font-control text-sm text-text-secondary"
          >
            Which one
          </label>
          <select
            id="certificationCode"
            name="certificationCode"
            required
            className="mt-1 h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          >
            {available.map((code) => (
              <option key={code} value={code}>
                {humanizeCode(code)}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="issuedOn" className="font-control text-sm text-text-secondary">
            Issued
          </label>
          <input
            id="issuedOn"
            name="issuedOn"
            type="date"
            required
            max={today}
            className="mt-1 h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          />
          {state.fieldErrors?.issuedOn ? (
            <p className="mt-1 text-sm text-red-700">{state.fieldErrors.issuedOn}</p>
          ) : null}
        </div>

        <div>
          <label htmlFor="expiresOn" className="font-control text-sm text-text-secondary">
            Expires (optional)
          </label>
          <input
            id="expiresOn"
            name="expiresOn"
            type="date"
            className="mt-1 h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          />
        </div>
      </div>

      <p className="text-sm text-text-secondary">
        Our team checks every certificate before it counts. Document upload is coming;
        for now we will email you if we need to see it.
      </p>

      <div className="flex gap-2">
        <SubmitButton />
        <Button type="button" variant="ghost" onClick={onDone}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" disabled={pending}>
      {pending ? "Submitting…" : "Submit"}
    </Button>
  );
}
