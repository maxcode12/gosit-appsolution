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
import { FlaggedMessageQueue } from "@/features/admin/components/flagged-message-queue";
import { adminNav } from "@/features/admin/nav";

export const metadata: Metadata = {
  title: "Flagged messages",
  description: "Messages that look like an exchange of contact details.",
};

/**
 * Messages flagged as possible contact sharing.
 *
 * Contact details have been flagged since messaging shipped, and until this screen existed
 * the flag was read by nothing. The policy that raises it is deliberately loose on the
 * grounds that a false positive only costs a review — there was no review, so a false
 * positive cost nothing and a real one cost nothing either.
 *
 * This is the only staff screen that can show a private message, and it is built so that
 * working through the queue does not mean reading them. Bodies are not loaded with the
 * page; a staff member opens one at a time and the API records who opened what.
 */
export default async function AdminFlaggedMessagesPage() {
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
      currentPath="/admin/flagged-messages"
      heading="Flagged messages"
      description="Taking care off-platform removes the record, the verified identity and the payment protection. Most of these are nothing."
    >
      <div className="max-w-3xl">
        <FlaggedMessageQueue messages={flagged} />
      </div>
    </DashboardShell>
  );
}
