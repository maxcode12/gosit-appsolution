import type { Metadata } from "next";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { caregiverNav } from "@/features/caregivers/nav";
import { getMySubscription, listPlans } from "@/features/subscriptions/api";
import { PlanPicker } from "@/features/subscriptions/components/plan-picker";

export const metadata: Metadata = {
  title: "Your plan",
  description: "What your GoSit plan includes.",
};

export default async function CaregiverPlanPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const [plans, current, params] = await Promise.all([
    listPlans(),
    getMySubscription(),
    searchParams,
  ]);

  // Set by the provider redirect. Not trusted for anything — it only decides which
  // message to show, and the message itself reads real state from the API.
  const raw = Array.isArray(params.checkout) ? params.checkout[0] : params.checkout;
  const checkout = raw === "complete" || raw === "cancelled" ? raw : undefined;

  return (
    <DashboardShell
      items={caregiverNav}
      currentPath="/caregiver/account"
      heading="Your plan"
      description="Working on GoSit is free. Plans add extras on top."
    >
      <div className="max-w-3xl">
        <PlanPicker plans={plans} current={current} checkout={checkout} />
      </div>
    </DashboardShell>
  );
}
