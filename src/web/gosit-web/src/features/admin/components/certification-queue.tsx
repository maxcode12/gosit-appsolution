"use client";

import { humanizeCode } from "@/components/ui/badges";
import { EmptyState } from "@/components/ui/empty-state";
import { DocumentIcon, StarIcon } from "@/components/ui/icons";
import {
  approveCertificationAction,
  rejectCertificationAction,
} from "@/features/admin/actions";
import { DecisionPanel } from "@/features/admin/components/decision-panel";
import { SubjectLine, waitedFor } from "@/features/admin/components/subject-line";
import type { PendingCertification } from "@/features/admin/types";

/**
 * Certificates waiting on a decision.
 *
 * Each one unlocks specific services — a First Aid certificate is what makes nanny care
 * bookable — so a certificate sitting here is a caregiver losing work they are qualified
 * for. Oldest first, and the wait is shown, because that is the number that should make
 * someone uncomfortable.
 */
export function CertificationQueue({
  certifications,
}: {
  certifications: PendingCertification[];
}) {
  if (certifications.length === 0) {
    return (
      <EmptyState
        icon={<StarIcon />}
        title="No certificates waiting"
        description="First Aid, CPR and specialist training appear here for review when a caregiver submits them."
      />
    );
  }

  return (
    <ul className="flex flex-col gap-4">
      {certifications.map((certification) => (
        <li key={certification.caregiverCertificationId} className="surface-card p-5">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <SubjectLine
              name={certification.caregiverName}
              email={certification.caregiverEmail}
            />

            <div className="text-right">
              <p className="font-control text-sm font-semibold text-text-primary">
                {certification.name || humanizeCode(certification.code)}
              </p>
              <p className="mt-0.5 text-xs text-text-secondary">
                Submitted {waitedFor(certification.submittedAtUtc)}
              </p>
            </div>
          </div>

          <dl className="mt-4 flex flex-wrap gap-x-8 gap-y-2 rounded-control bg-surface-muted px-4 py-3 text-sm">
            <div className="flex gap-2">
              <dt className="text-text-secondary">Issued</dt>
              <dd className="text-text-primary">{formatDate(certification.issuedOn)}</dd>
            </div>
            <div className="flex gap-2">
              <dt className="text-text-secondary">Expires</dt>
              <dd className="text-text-primary">
                {certification.expiresOn ? formatDate(certification.expiresOn) : "No expiry"}
              </dd>
            </div>
          </dl>

          {/*
            The evidence, or the absence of it.

            Placed above the buttons because it is what the decision is made on. Until
            uploads existed this queue showed a code and two dates the caregiver had
            typed, and staff approved from that — which is what a Verified badge rested on.
          */}
          <div className="mt-3">
            {certification.hasDocument ? (
              <a
                href={documentHref(certification.caregiverCertificationId)}
                // Opens outside this page. The API serves it as an attachment, so an
                // uploaded file never renders in the origin an admin is signed in to.
                target="_blank"
                rel="noreferrer"
                className="inline-flex min-h-11 items-center gap-2 rounded-control border border-border-strong px-4 font-control text-sm font-semibold text-text-primary transition-colors hover:bg-surface-muted"
              >
                <DocumentIcon />
                Open the certificate
              </a>
            ) : (
              <p className="rounded-control bg-accent-soft px-4 py-3 text-sm text-text-primary">
                No certificate was uploaded, so there is nothing to verify. Reject this and
                ask for the document, or leave it until they upload one.
              </p>
            )}
          </div>

          {certification.isExpired ? (
            <p className="mt-3 rounded-control bg-accent-soft px-4 py-3 text-sm text-text-primary">
              This certificate has already lapsed. Approving it will not make anything
              bookable — ask for a current one instead.
            </p>
          ) : null}

          <DecisionPanel
            approve={() =>
              approveCertificationAction(certification.caregiverCertificationId)
            }
            reject={rejectCertificationAction.bind(
              null,
              certification.caregiverCertificationId,
            )}
            approveBlockedReason={
              certification.hasDocument
                ? undefined
                : "Approving needs a certificate to look at."
            }
            approveLabel="Approve certificate"
            rejectLabel="Reject"
            rejectPrompt="Why is this being rejected?"
            rejectPlaceholder="The scan is cut off. Ask for the full certificate."
          />
        </li>
      ))}
    </ul>
  );
}

/**
 * A calendar date as "31 Dec 2029", read field by field.
 *
 * `issuedOn`/`expiresOn` are bare dates — "2029-12-31", no time and no zone — and
 * `new Date("2029-12-31")` parses that as UTC midnight. Formatting it with a
 * local-zone formatter anywhere west of Greenwich prints the day before, which is
 * exactly the trap the caregiver-facing certifications and insurance panels already
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
 * Where the certificate is fetched from.
 *
 * A Next route rather than the API directly: the admin's token lives in an httpOnly
 * cookie the browser cannot read, so a link straight to the API would arrive
 * unauthenticated. The route forwards the request with the session, and the API still
 * authorises and audits it.
 */
function documentHref(caregiverCertificationId: string): string {
  return `/documents/certifications/${caregiverCertificationId}`;
}