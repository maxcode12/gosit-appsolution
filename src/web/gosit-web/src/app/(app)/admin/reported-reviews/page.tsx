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
import { ReportedReviewQueue } from "@/features/admin/components/reported-review-queue";
import { adminNav } from "@/features/admin/nav";

export const metadata: Metadata = {
  title: "Reported reviews",
  description: "Reviews their subject has asked us to look at.",
};

/**
 * Reviews someone has asked staff to look at.
 *
 * Until this existed a published review was permanent. A caregiver's rating is how they
 * get work, and a retaliatory one-star after a dispute, a comment naming a child, or a
 * slur reached nobody who could act on it — reporting a *user* left the words in place.
 *
 * Taking one down removes it from the profile and from the rating, and never edits or
 * deletes it: the review is evidence about its author as much as its subject.
 */
export default async function AdminReportedReviewsPage() {
  const [verifications, certifications, insurance, incidents, disputes, flagged, reviews] =
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
        reportedReviews: reviews.length,
      })}
      currentPath="/admin/reported-reviews"
      heading="Reported reviews"
      description="Each one is on someone's public profile while it waits. Most will be a review the subject simply disagrees with."
    >
      <div className="max-w-3xl">
        <ReportedReviewQueue reviews={reviews} />
      </div>
    </DashboardShell>
  );
}
