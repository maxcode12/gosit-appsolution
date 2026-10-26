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

          <ProviderReport status={pending.providerStatus} score={pending.providerScore} />

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

/** Certn's progress words, in plain English. Anything unrecognised is shown as Certn sent it. */
const PROVIDER_STATUS_LABELS: Record<string, string> = {
  CASE_ORDERED: "Ordered. Waiting for the caregiver to be emailed.",
  APPLICANT_INVITED: "The caregiver has been emailed and has not started.",
  APPLICANT_OPENED: "The caregiver opened the email but has not started.",
  APPLICANT_STARTED: "The caregiver has started their application.",
  APPLICANT_SUBMITTED: "The caregiver has submitted. Checks are running.",
  APPLICANT_EXPIRED: "The caregiver did not finish in time.",
  INVITE_UNDELIVERABLE: "The email to the caregiver bounced.",
  IN_PROGRESS: "Checks are running.",
  APPLICANT_ACTION_REQUIRED: "Waiting on the caregiver for more information.",
  CLIENT_ACTION_REQUIRED: "Certn needs something from GoSit. Check the Certn portal.",
  IN_DISPUTE: "The caregiver is disputing a result.",
  CANCELLED: "Cancelled.",
  COMPLETE: "Finished.",
};

const SCORE_LABELS: Record<string, { text: string; tone: string }> = {
  CLEAR: { text: "Clear. No issues found.", tone: "text-brand-primary" },
  REVIEW: { text: "Needs a closer look. Open the report in Certn.", tone: "text-danger-accent" },
  REJECT: { text: "Adverse findings. Open the report in Certn.", tone: "text-danger-accent" },
  NOT_APPLICABLE: { text: "No score applies to this check.", tone: "text-text-secondary" },
  RESTRICTED: { text: "Certn has restricted this result. Open it in Certn.", tone: "text-danger-accent" },
};

/**
 * What the screening provider has said about this check.
 *
 * Shown beside the decision because it is the evidence for it, and labelled as evidence
 * because a clear score is not an approval: the reviewer still decides, and the full report
 * stays with the provider rather than being copied into GoSit.
 */
function ProviderReport({ status, score }: { status?: string | null; score?: string | null }) {
  if (!status && !score) {
    return null;
  }

  const verdict = score ? SCORE_LABELS[score] : undefined;

  return (
    <div className="mt-3 rounded-control border border-border bg-surface-muted px-4 py-3 text-sm">
      <p className="font-control text-xs font-semibold uppercase tracking-wide text-text-secondary">
        Screening provider
      </p>
      {status ? (
        <p className="mt-1 text-text-primary">{PROVIDER_STATUS_LABELS[status] ?? status}</p>
      ) : null}
      {score ? (
        <p className={`mt-1 font-medium ${verdict?.tone ?? "text-text-primary"}`}>
          {verdict?.text ?? score}
        </p>
      ) : null}
      <p className="mt-2 text-xs text-text-secondary">
        This is evidence, not an approval. You make the decision.
      </p>
    </div>
  );
}
