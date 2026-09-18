import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { getCareRecipient } from "@/features/account/api";
import { CareNeedsForm } from "@/features/account/components/care-needs-form";
import { careRequesterNav } from "@/features/search/nav";

/**
 * Care needs for one person.
 *
 * A static title on purpose: a page title reaches browser history, tab strips and shared
 * screenshots, and none of those are places for a care recipient's name.
 */
export const metadata: Metadata = {
  title: "Care needs",
};

export default async function CareNeedsPage({
  params,
}: {
  params: Promise<{ careRecipientId: string }>;
}) {
  const { careRecipientId } = await params;

  const recipient = await getCareRecipient(careRecipientId);

  if (!recipient) {
    notFound();
  }

  return (
    <DashboardShell
      items={careRequesterNav}
      currentPath="/account"
      heading={`${recipient.firstName}'s care needs`}
      description={`${recipient.age} · ${recipient.recipientType}`}
    >
      <div className="max-w-2xl">
        <Link
          href="/account"
          className="font-control text-sm text-text-secondary hover:text-text-primary"
        >
          ← Account
        </Link>

        <p className="mt-4 rounded-card bg-surface-muted px-5 py-4 text-sm text-text-primary">
          Write what someone would need to know to look after {recipient.firstName} well.
          None of this is public. A caregiver sees it only after you book them, and only
          for that booking.
        </p>

        <div className="mt-6">
          <CareNeedsForm
            careRecipientId={recipient.id}
            firstName={recipient.firstName}
            careNeeds={recipient.careNeeds}
          />
        </div>
      </div>
    </DashboardShell>
  );
}
