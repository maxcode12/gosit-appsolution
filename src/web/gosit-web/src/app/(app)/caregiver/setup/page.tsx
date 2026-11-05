import type { Metadata } from "next";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { getMyCaregiverProfile, getMyVerifications, getPayoutAccount } from "@/features/caregivers/api";
import { SetupChecklist } from "@/features/caregivers/components/setup-checklist";
import { StartProfileForm } from "@/features/caregivers/components/start-profile-form";
import { caregiverNav } from "@/features/caregivers/nav";
import { buildSetupSteps } from "@/features/caregivers/setup-steps";

export const metadata: Metadata = {
  title: "Finish setting up",
  description: "What GoSit needs from you before families can find you and you can be paid.",
};

/**
 * Where a caregiver lands after the short sign-up form: everything still to do, in one place.
 * Sign-up deliberately collects almost nothing, so this is the real registration.
 */
export default async function CaregiverSetupPage() {
  const [profile, verifications, payout] = await Promise.all([
    getMyCaregiverProfile(),
    getMyVerifications(),
    getPayoutAccount(),
  ]);

  return (
    <DashboardShell
      items={caregiverNav}
      currentPath="/caregiver/setup"
      heading="Finish setting up"
      description="Your details, checks, insurance and payout account."
    >
      <div className="flex max-w-2xl flex-col gap-6">
        {profile ? null : <StartProfileForm />}
        <SetupChecklist
          steps={buildSetupSteps(profile, verifications, payout)}
          isListable={profile?.isListable ?? false}
        />
      </div>
    </DashboardShell>
  );
}
