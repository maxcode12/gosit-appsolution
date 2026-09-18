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
import { DisputeQueue } from "@/features/admin/components/dispute-queue";
import { adminNav } from "@/features/admin/nav";

export const metadata: Metadata = {
  title: "Disputes",
  description: "Disputed bookings waiting on a decision.",
};

/**
 * Disputed bookings.
 *
 * Every one here is holding a caregiver's payout — they are unpaid and the family is
 * unanswered for as long as it waits. Until this screen existed a dispute appeared
 * nowhere in the portal at all, and the money it held had no way out.
 */
export default async function AdminDisputesPage() {
  const [verifications, certifications, insurance, incidents, disputes, flagged, reportedReviews] = await Promise.all([
    listPendingVerifications(),
    listPendingCertifications(),
    listPendingInsurance(),
    listOpenIncidents(),
    listOpenDisputes(),
    listFlaggedMessages(),
    listReportedReviews(),
  ]);

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
      currentPath="/admin/disputes"
      heading="Disputes"
      description="Each one is holding a caregiver's pay. Deciding releases it, reduces it, or refunds the family."
    >
      <div className="max-w-3xl">
        <DisputeQueue disputes={disputes} />
      </div>
    </DashboardShell>
  );
}
