import type { Metadata } from "next";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { caregiverNav } from "@/features/caregivers/nav";
import { getMyReferrals } from "@/features/growth/api";
import { ReferralPanel } from "@/features/growth/components/referral-panel";

export const metadata: Metadata = {
  title: "Invite caregivers",
  description: "Your referral code and the invitations against it.",
};

export default async function CaregiverReferralsPage() {
  const summary = await getMyReferrals();

  return (
    <DashboardShell
      items={caregiverNav}
      currentPath="/caregiver/account"
      heading="Invite caregivers"
      description="Know someone else who would be good at this?"
    >
      <div className="max-w-3xl">
        <ReferralPanel summary={summary} />
      </div>
    </DashboardShell>
  );
}
