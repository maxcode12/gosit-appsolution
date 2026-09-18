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
import { InsuranceQueue } from "@/features/admin/components/insurance-queue";
import { adminNav } from "@/features/admin/nav";

export const metadata: Metadata = {
  title: "Insurance",
  description: "Caregiver insurance policies waiting on a decision.",
};

/**
 * Caregiver insurance policies.
 *
 * GoSit verifies a caregiver's own coverage here; it never issues or holds it — see
 * docs/BUSINESS_MODEL.md and docs/ADR-004-marketplace-provider-model.md. Until a policy
 * is approved it counts for nothing, mirroring how a certificate works.
 */
export default async function AdminInsurancePage() {
  const [verifications, certifications, insurance, incidents, disputes, flagged, reportedReviews] =
    await Promise.all([
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
      currentPath="/admin/insurance"
      heading="Insurance"
      description="Caregivers maintain their own coverage. This is where GoSit verifies it — never where GoSit provides it."
    >
      <div className="max-w-3xl">
        <InsuranceQueue policies={insurance} />
      </div>
    </DashboardShell>
  );
}
