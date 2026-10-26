import type { Metadata } from "next";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { listEnquiries } from "@/features/admin/api";
import { EnquiryQueue } from "@/features/admin/components/enquiry-queue";
import { adminNav } from "@/features/admin/nav";

export const metadata: Metadata = {
  title: "Enquiries",
  description: "Messages sent through the contact form on the public website.",
};

/**
 * Messages from the public contact form.
 *
 * Sent by people who do not have an account, so there is nothing to link them to and no
 * in-app way to reply: staff email them. Only new ones are listed; handled and spam
 * messages stay on record but leave the queue.
 */
export default async function AdminEnquiriesPage() {
  const enquiries = await listEnquiries();

  return (
    <DashboardShell
      items={adminNav({ enquiries: enquiries.length })}
      currentPath="/admin/enquiries"
      heading="Enquiries"
      description="Messages from the website contact form. Reply by email, then mark them handled."
    >
      <div className="max-w-3xl">
        <EnquiryQueue enquiries={enquiries} />
      </div>
    </DashboardShell>
  );
}
