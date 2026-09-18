"use client";

import { EmptyState } from "@/components/ui/empty-state";
import { ShieldIcon } from "@/components/ui/icons";
import {
  approveVerificationAction,
  rejectVerificationAction,
} from "@/features/admin/actions";
import { DecisionPanel } from "@/features/admin/components/decision-panel";
import { SubjectLine, waitedFor } from "@/features/admin/components/subject-line";
import { CHECK_LABELS, REQUIRED_FOR_LISTING } from "@/features/admin/types";
import type { PendingVerification } from "@/features/admin/types";

/**
 * Background checks waiting on a decision.
 *
 * The single most consequential screen in the product: approving here is what lets a
 * stranger into a family's home. So each row says plainly whether this particular check
 * is one of the two that gate listing, and approving one check is never presented as
 * clearing the caregiver — the API derives that from all of them.
 */
export function VerificationQueue({ cases }: { cases: PendingVerification[] }) {
  if (cases.length === 0) {
    return (
      <EmptyState
        icon={<ShieldIcon />}
        title="No checks waiting"
        description="When a caregiver starts a background check it appears here for review."
      />
    );
  }

  return (
    <ul className="flex flex-col gap-4">
      {cases.map((pending) => (
        <li key={pending.caseId} className="surface-card p-5">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <SubjectLine
              name={pending.caregiverName}
              email={pending.caregiverEmail}
              accountStatus={pending.caregiverAccountStatus}
            />

            <div className="text-right">
              <p className="font-control text-sm font-semibold text-text-primary">
                {CHECK_LABELS[pending.checkType] ?? pending.checkType}
              </p>
              <p className="mt-0.5 text-xs text-text-secondary">
                Submitted {waitedFor(pending.submittedAtUtc)}
              </p>
            </div>
          </div>

          <p className="mt-3 text-sm text-text-secondary">
            {REQUIRED_FOR_LISTING.includes(pending.checkType)
              ? "Required before this caregiver can appear in search. They are still hidden until every required check clears."
              : "Optional. Clearing it does not by itself make this caregiver visible to families."}
          </p>

          <DecisionPanel
            approve={() => approveVerificationAction(pending.caseId)}
            reject={rejectVerificationAction.bind(null, pending.caseId)}
            approveLabel="Approve check"
            rejectLabel="Reject"
            rejectPrompt="Why is this being rejected?"
            rejectPlaceholder="The document was expired. Ask for a current one."
          />
        </li>
      ))}
    </ul>
  );
}
