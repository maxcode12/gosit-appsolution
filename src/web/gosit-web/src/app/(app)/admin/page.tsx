import type { Metadata } from "next";
import Link from "next/link";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import {
  listFlaggedMessages,
  listOpenDisputes,
  listOpenIncidents,
  listPendingCertifications,
  listPendingInsurance,
  listPendingVerifications,
  listReportedReviews,
} from "@/features/admin/api";
import { adminNav } from "@/features/admin/nav";
import { SEVERITY_ORDER } from "@/features/admin/types";

export const metadata: Metadata = {
  title: "Staff overview",
  description: "Checks, certificates and reports waiting on a decision.",
};

/**
 * The staff overview.
 *
 * Ordered by consequence, not by volume. An urgent safety report outranks forty
 * certificates however long they have waited, so it is called out first and separately
 * rather than being one number among four.
 */
export default async function AdminOverviewPage() {
  const [verifications, certifications, insurance, incidents, disputes, flagged, reportedReviews] = await Promise.all([
    listPendingVerifications(),
    listPendingCertifications(),
    listPendingInsurance(),
    listOpenIncidents(),
    listOpenDisputes(),
    listFlaggedMessages(),
    listReportedReviews(),
  ]);

  const urgent = incidents.filter((incident) => incident.requiresUrgentReview);

  // The oldest thing anyone is waiting on, which is the number that should prompt action.
  const oldestWait = [...verifications, ...certifications]
    .map((item) => item.submittedAtUtc)
    .sort()
    .at(0);

  return (
    <DashboardShell
      items={adminNav({
        verifications: verifications.length,
        certifications: certifications.length,
        insurance: insurance.length,
        incidents: incidents.length,
        disputes: disputes.length,
        flaggedMessages: flagged.length,
        reportedReviews: reportedReviews.length,
      })}
      currentPath="/admin"
      heading="Staff overview"
      description="Everything waiting on a decision."
    >
      <div className="flex max-w-4xl flex-col gap-8">
        {urgent.length > 0 ? (
          <section className="rounded-card border border-danger-accent/30 bg-danger-soft p-5">
            <h2 className="font-heading text-base font-bold text-danger-accent">
              {urgent.length} urgent{" "}
              {urgent.length === 1 ? "report needs" : "reports need"} review
            </h2>
            <p className="mt-1 text-sm text-text-primary">
              Marked urgent by the person who reported them. Everything else can wait.
            </p>
            <Link
              href="/admin/incidents"
              className="mt-4 inline-flex font-control text-sm font-semibold text-danger-accent hover:underline"
            >
              Go to reports →
            </Link>
          </section>
        ) : null}

        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <QueueCard
            href="/admin/verifications"
            label="Background checks"
            count={verifications.length}
            blurb="Caregivers cannot be listed until these clear."
          />
          <QueueCard
            href="/admin/certifications"
            label="Certificates"
            count={certifications.length}
            blurb="Each one unlocks the services a caregiver can be booked for."
          />
          <QueueCard
            href="/admin/insurance"
            label="Insurance"
            count={insurance.length}
            blurb="GoSit verifies a caregiver's own coverage — it never provides it."
          />
          <QueueCard
            href="/admin/incidents"
            label="Open reports"
            count={incidents.length}
            blurb="Safety concerns raised by families and caregivers."
          />
        </div>

        {oldestWait ? (
          <p className="text-sm text-text-secondary">
            The longest anyone has been waiting on a decision is{" "}
            <strong className="font-semibold text-text-primary">
              {daysWaiting(oldestWait)}
            </strong>
            . Every day here is a day someone cannot work.
          </p>
        ) : (
          <p className="text-sm text-text-secondary">
            Nothing is waiting on a decision right now.
          </p>
        )}

        {incidents.length > 0 ? <SeverityBreakdown incidents={incidents} /> : null}
      </div>
    </DashboardShell>
  );
}

function QueueCard({
  href,
  label,
  count,
  blurb,
}: {
  href: string;
  label: string;
  count: number;
  blurb: string;
}) {
  return (
    <Link href={href} className="surface-card-interactive block p-5">
      <p className="font-control text-sm font-medium text-text-secondary">{label}</p>
      <p
        className={`mt-1 font-heading text-4xl font-extrabold ${
          count > 0 ? "text-text-primary" : "text-text-muted"
        }`}
      >
        {count}
      </p>
      <p className="mt-2 text-sm leading-relaxed text-text-secondary">{blurb}</p>
    </Link>
  );
}

function SeverityBreakdown({
  incidents,
}: {
  incidents: { severity: string }[];
}) {
  const counts = SEVERITY_ORDER.map((severity) => ({
    severity,
    count: incidents.filter((incident) => incident.severity === severity).length,
  })).filter((entry) => entry.count > 0);

  if (counts.length === 0) {
    return null;
  }

  return (
    <section>
      <h2 className="font-heading text-lg font-semibold text-text-primary">
        Reports by severity
      </h2>

      <dl className="mt-4 flex flex-wrap gap-3">
        {counts.map((entry) => (
          <div
            key={entry.severity}
            className="rounded-control bg-surface px-4 py-3 shadow-xs"
          >
            <dt className="text-sm text-text-secondary">{entry.severity}</dt>
            <dd className="mt-0.5 font-heading text-xl font-bold text-text-primary">
              {entry.count}
            </dd>
          </div>
        ))}
      </dl>
    </section>
  );
}

function daysWaiting(iso: string): string {
  const days = Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000);

  if (days < 1) return "less than a day";

  return `${days} ${days === 1 ? "day" : "days"}`;
}
