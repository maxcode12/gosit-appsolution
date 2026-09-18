import type { Metadata } from "next";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import {
  listFlaggedMessages,
  listOpenDisputes,
  listOpenIncidents,
  listPendingCertifications,
  listPendingInsurance,
  listPendingVerifications,
  listRecordedInterviews,
  listReportedReviews,
} from "@/features/admin/api";
import { InterviewRecordings } from "@/features/admin/components/interview-recordings";
import { adminNav } from "@/features/admin/nav";

export const metadata: Metadata = {
  title: "Interview recordings",
  description: "Recorded video interviews between families and caregivers.",
};

/**
 * Recorded interviews.
 *
 * Not a queue. Nothing here is waiting on staff; it is where staff go when a family or a
 * caregiver raises a concern about what was said. Recordings are deleted after 90 days
 * unless an interview is put on hold, and every view is logged.
 */
export default async function AdminInterviewsPage() {
  const [verifications, certifications, insurance, incidents, disputes, flagged, reportedReviews, interviews] =
    await Promise.all([
      listPendingVerifications(),
      listPendingCertifications(),
      listPendingInsurance(),
      listOpenIncidents(),
      listOpenDisputes(),
      listFlaggedMessages(),
      listReportedReviews(),
      listRecordedInterviews(),
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
      currentPath="/admin/interviews"
      heading="Interview recordings"
      description="Look one up when a concern is raised. Watching is logged; recordings go after 90 days unless held."
    >
      <div className="max-w-3xl">
        <InterviewRecordings interviews={interviews} />
      </div>
    </DashboardShell>
  );
}
