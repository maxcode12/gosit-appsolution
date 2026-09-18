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
import { VerificationQueue } from "@/features/admin/components/verification-queue";
import { adminNav } from "@/features/admin/nav";

export const metadata: Metadata = {
  title: "Background checks",
  description: "Checks waiting on a decision.",
};

export default async function AdminVerificationsPage() {
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
      currentPath="/admin/verifications"
      heading="Background checks"
      description="Approving a check here is what lets someone into a family's home."
    >
      <div className="max-w-3xl">
        <VerificationQueue cases={verifications} />
      </div>
    </DashboardShell>
  );
}
