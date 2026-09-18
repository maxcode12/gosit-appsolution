"use client";

import { useState, useTransition } from "react";
import { Button } from "@/components/ui/button";
import { submitVerificationAction } from "@/features/caregivers/actions";
import {
  CHECK_LABELS,
  REQUIRED_FOR_LISTING,
  type VerificationCheckType,
  type VerificationSummary,
} from "@/features/caregivers/types";

const ALL_CHECKS: VerificationCheckType[] = [
  "Identity",
  "VulnerableSector",
  "BackgroundCheck",
  "References",
];

/**
 * Background checks.
 *
 * Nothing here approves anything — Trust and Safety decides, and the API would refuse a
 * caregiver trying to pass their own check. This screen only starts a check and reports
 * where it got to. A rejection shows the reviewer's notes, because being told "rejected"
 * with no reason gives someone no way forward.
 */
export function VerificationPanel({
  verifications,
}: {
  verifications: VerificationSummary | null;
}) {
  const cases = new Map(verifications?.cases.map((c) => [c.checkType, c]) ?? []);

  return (
    <div className="flex flex-col gap-4">
      {verifications?.meetsListingRequirements ? (
        <p className="rounded-control bg-brand-primary/10 px-4 py-3 text-sm text-brand-primary">
          Your required checks are done.
        </p>
      ) : (
        <p className="text-sm text-text-secondary">
          {REQUIRED_FOR_LISTING.map((check) => CHECK_LABELS[check]).join(" and ")} are
          required before families can find you. The others are optional and help you get
          booked.
        </p>
      )}

      <ul className="flex flex-col gap-3">
        {ALL_CHECKS.map((check) => (
          <li key={check}>
            <CheckRow
              check={check}
              existing={cases.get(check)}
              isRequired={REQUIRED_FOR_LISTING.includes(check)}
            />
          </li>
        ))}
      </ul>
    </div>
  );
}

function CheckRow({
  check,
  existing,
  isRequired,
}: {
  check: VerificationCheckType;
  existing: VerificationSummary["cases"][number] | undefined;
  isRequired: boolean;
}) {
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  return (
    <div className="rounded-card border border-border bg-surface p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-text-primary">
            {CHECK_LABELS[check]}
            {isRequired ? (
              <span className="ml-2 text-sm text-text-secondary">Required</span>
            ) : null}
          </p>

          {existing ? (
            <p className="mt-0.5 text-sm text-text-secondary">
              {statusLine(existing.status, existing.submittedAtUtc)}
            </p>
          ) : null}
        </div>

        {existing ? (
          <StatusBadge status={existing.status} />
        ) : (
          <Button
            type="button"
            variant="ghost"
            disabled={isPending}
            onClick={() =>
              startTransition(async () => {
                setError(null);

                const result = await submitVerificationAction(check);

                if (result.error) {
                  setError(result.error);
                }
              })
            }
          >
            {isPending ? "Starting…" : "Start"}
          </Button>
        )}
      </div>

      {existing?.decisionNotes ? (
        <p className="mt-3 border-t border-border pt-3 text-sm text-text-primary">
          {existing.decisionNotes}
        </p>
      ) : null}

      {error ? (
        <p role="alert" className="mt-2 text-sm text-red-700">
          {error}
        </p>
      ) : null}
    </div>
  );
}

function statusLine(status: string, submittedAtUtc: string): string {
  const submitted = new Date(submittedAtUtc).toLocaleDateString("en-CA", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });

  if (status === "Approved" || status === "Verified") return `Cleared ${submitted}`;
  if (status === "Rejected") return `Reviewed ${submitted}`;

  return `Started ${submitted} — with our team`;
}

function StatusBadge({ status }: { status: string }) {
  const isDone = status === "Approved" || status === "Verified";
  const isRejected = status === "Rejected";

  return (
    <span
      className={`inline-flex items-center rounded-pill px-3 py-1 text-xs font-semibold ${
        isDone
          ? "bg-brand-primary/10 text-brand-primary"
          : isRejected
            ? "bg-danger-accent/10 text-danger-accent"
            : "bg-brand-accent/15 text-text-primary"
      }`}
    >
      {isDone ? "Cleared" : isRejected ? "Not cleared" : "In review"}
    </span>
  );
}
