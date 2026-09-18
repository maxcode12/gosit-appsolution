import type { Metadata } from "next";
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
import { IncidentQueue } from "@/features/admin/components/incident-queue";
import { adminNav } from "@/features/admin/nav";
import { SEVERITY_ORDER } from "@/features/admin/types";

export const metadata: Metadata = {
  title: "Reports",
  description: "Safety concerns waiting on triage.",
};

export default async function AdminIncidentsPage() {
  const [verifications, certifications, insurance, incidents, disputes, flagged, reportedReviews] = await Promise.all([
    listPendingVerifications(),
    listPendingCertifications(),
    listPendingInsurance(),
    listOpenIncidents(),
    listOpenDisputes(),
    listFlaggedMessages(),
    listReportedReviews(),
  ]);

  // Urgent first, then by severity. The API already orders by its own rules; this makes
  // the ordering visible and stable regardless of how it changes.
  const ordered = [...incidents].sort((a, b) => {
    if (a.requiresUrgentReview !== b.requiresUrgentReview) {
      return a.requiresUrgentReview ? -1 : 1;
    }

    return severityRank(a.severity) - severityRank(b.severity);
  });

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
      currentPath="/admin/incidents"
      heading="Reports"
      description="Urgent first. Every decision is recorded against the account."
    >
      <div className="max-w-3xl">
        <IncidentQueue incidents={ordered} />
      </div>
    </DashboardShell>
  );
}

function severityRank(severity: string): number {
  const index = SEVERITY_ORDER.indexOf(severity as (typeof SEVERITY_ORDER)[number]);

  return index === -1 ? SEVERITY_ORDER.length : index;
}
