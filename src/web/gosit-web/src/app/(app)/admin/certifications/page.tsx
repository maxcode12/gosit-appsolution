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
import { CertificationQueue } from "@/features/admin/components/certification-queue";
import { adminNav } from "@/features/admin/nav";

export const metadata: Metadata = {
  title: "Certificates",
  description: "Certificates waiting on a decision.",
};

export default async function AdminCertificationsPage() {
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
      currentPath="/admin/certifications"
      heading="Certificates"
      description="Until a certificate is approved it counts for nothing, and the services needing it stay unbookable."
    >
      <div className="max-w-3xl">
        <CertificationQueue certifications={certifications} />
      </div>
    </DashboardShell>
  );
}
