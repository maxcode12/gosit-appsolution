import type { Metadata } from "next";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { getMyReferrals } from "@/features/growth/api";
import { ReferralPanel } from "@/features/growth/components/referral-panel";
import { careRequesterNav } from "@/features/search/nav";

export const metadata: Metadata = {
  title: "Invite friends",
  description: "Your referral code and the invitations against it.",
};

export default async function CareRequesterReferralsPage() {
  const summary = await getMyReferrals();

  return (
    <DashboardShell
      items={careRequesterNav}
      currentPath="/account"
      heading="Invite friends"
      description="Know another family who needs care?"
    >
      <div className="max-w-3xl">
        <ReferralPanel summary={summary} />
      </div>
    </DashboardShell>
  );
}
