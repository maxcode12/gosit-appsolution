"use client";

import { EmptyState } from "@/components/ui/empty-state";
import { DocumentIcon, ShieldIcon } from "@/components/ui/icons";
import {
  approveInsuranceAction,
  rejectInsuranceAction,
} from "@/features/admin/actions";
import { DecisionPanel } from "@/features/admin/components/decision-panel";
import { SubjectLine, waitedFor } from "@/features/admin/components/subject-line";
import { INSURANCE_COVERAGE_LABELS, type PendingInsurance } from "@/features/admin/types";

/**
 * Insurance policies waiting on a decision.
 *
 * GoSit verifies a caregiver's own coverage; it never issues or holds it — see
 * docs/BUSINESS_MODEL.md. Mirrors CertificationQueue: the evidence is what the decision
 * is made on, and approving without it is approving a caregiver's own claim about
 * themselves.
 */
export function InsuranceQueue({ policies }: { policies: PendingInsurance[] }) {
  if (policies.length === 0) {
    return (
      <EmptyState
        icon={<ShieldIcon />}
        title="No policies waiting"
        description="Proof of insurance appears here for review when a caregiver submits it."
      />
    );
  }

  return (
    <ul className="flex flex-col gap-4">
      {policies.map((policy) => (
        <li key={policy.caregiverInsurancePolicyId} className="surface-card p-5">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <SubjectLine name={policy.caregiverName} email={policy.caregiverEmail} />

            <div className="text-right">
              <p className="font-control text-sm font-semibold text-text-primary">
                {INSURANCE_COVERAGE_LABELS[policy.coverageType]}
              </p>
              <p className="mt-0.5 text-xs text-text-secondary">
                Submitted {waitedFor(policy.submittedAtUtc)}
              </p>
            </div>
          </div>

          <dl className="mt-4 flex flex-wrap gap-x-8 gap-y-2 rounded-control bg-surface-muted px-4 py-3 text-sm">
            <div className="flex gap-2">
              <dt className="text-text-secondary">Insurer</dt>
              <dd className="text-text-primary">{policy.insurer}</dd>
            </div>
            <div className="flex gap-2">
              <dt className="text-text-secondary">Policy</dt>
              <dd className="text-text-primary">{policy.policyReference}</dd>
            </div>
            <div className="flex gap-2">
              <dt className="text-text-secondary">Liability limit</dt>
              <dd className="text-text-primary">
                ${policy.liabilityLimit.toLocaleString("en-CA")}
              </dd>
            </div>
            <div className="flex gap-2">
              <dt className="text-text-secondary">Effective</dt>
              <dd className="text-text-primary">{formatDate(policy.effectiveOn)}</dd>
            </div>
            <div className="flex gap-2">
              <dt className="text-text-secondary">Expires</dt>
              <dd className="text-text-primary">{formatDate(policy.expiresOn)}</dd>
            </div>
          </dl>

          <div className="mt-3">
            {policy.hasDocument ? (
              <a
                href={documentHref(policy.caregiverInsurancePolicyId)}
                target="_blank"
                rel="noreferrer"
                className="inline-flex min-h-11 items-center gap-2 rounded-control border border-border-strong px-4 font-control text-sm font-semibold text-text-primary transition-colors hover:bg-surface-muted"
              >
                <DocumentIcon />
                Open the declaration page
              </a>
            ) : (
              <p className="rounded-control bg-accent-soft px-4 py-3 text-sm text-text-primary">
                No document was uploaded, so there is nothing to verify. Reject this and
                ask for it, or leave it until they upload one.
              </p>
            )}
          </div>

          {policy.isExpired ? (
            <p className="mt-3 rounded-control bg-accent-soft px-4 py-3 text-sm text-text-primary">
              This policy has already lapsed. Approving it will not make anything
              bookable — ask for a current one instead.
            </p>
          ) : null}

          <DecisionPanel
            approve={() => approveInsuranceAction(policy.caregiverInsurancePolicyId)}
            reject={rejectInsuranceAction.bind(null, policy.caregiverInsurancePolicyId)}
            approveBlockedReason={
              policy.hasDocument ? undefined : "Approving needs a document to look at."
            }
            approveLabel="Approve policy"
            rejectLabel="Reject"
            rejectPrompt="Why is this being rejected?"
            rejectPlaceholder="The declaration page does not show a coverage limit."
          />
        </li>
      ))}
    </ul>
  );
}

/**
 * A calendar date as "31 Dec 2029", read field by field.
 *
 * `effectiveOn`/`expiresOn` are bare dates — "2029-12-31", no time and no zone —
 * and `new Date("2029-12-31")` parses that as UTC midnight. Formatting it with a
 * local-zone formatter anywhere west of Greenwich prints the day before, which is
 * exactly the trap the caregiver-facing insurance and certification panels already
 * avoid by reading the fields directly rather than through the `Date` constructor.
 */
function formatDate(isoDate: string): string {
  const [year, month, day] = isoDate.slice(0, 10).split("-").map(Number);

  return new Date(year, month - 1, day).toLocaleDateString("en-CA", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

/**
 * Where the document is fetched from. A Next route rather than the API directly, for the
 * same reason as a certification: the admin's token lives in an httpOnly cookie the
 * browser cannot read.
 */
function documentHref(caregiverInsurancePolicyId: string): string {
  return `/documents/insurance/${caregiverInsurancePolicyId}`;
}
