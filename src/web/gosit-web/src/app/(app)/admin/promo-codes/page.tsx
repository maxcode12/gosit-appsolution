import type { Metadata } from "next";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import {
  listFlaggedMessages,
  listOpenDisputes,
  listOpenIncidents,
  listPendingCertifications,
  listPendingInsurance,
  listPendingVerifications,
  listPromoCodes,
  listReportedReviews,
} from "@/features/admin/api";
import { PromoCodeManager } from "@/features/admin/components/promo-code-manager";
import { adminNav } from "@/features/admin/nav";

export const metadata: Metadata = {
  title: "Promo codes",
  description: "Campaigns, what they are costing, and switching one off.",
};

/**
 * Staff management of promo codes.
 *
 * Until this existed a code had to be inserted into the database by hand, which meant
 * nobody without database access could run a campaign and nobody at all could see what
 * one had cost.
 *
 * Deliberately three things and no more: create, see what is being spent, switch off.
 * There is no edit — a code that has been handed out carries an offer already made to
 * everyone holding it, and quietly changing the terms afterwards penalises whoever was
 * slowest to redeem. Switching one off and issuing another is the same work and leaves
 * both campaigns legible.
 */
export default async function AdminPromoCodesPage() {
  const [verifications, certifications, insurance, incidents, disputes, promoCodes, flagged, reportedReviews] = await Promise.all([
    listPendingVerifications(),
    listPendingCertifications(),
    listPendingInsurance(),
    listOpenIncidents(),
    listOpenDisputes(),
    listPromoCodes(),
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
      currentPath="/admin/promo-codes"
      heading="Promo codes"
      description="A promo code gives money away. Everything here is recorded against your account."
    >
      <div className="max-w-3xl">
        <PromoCodeManager promoCodes={promoCodes} />
      </div>
    </DashboardShell>
  );
}
