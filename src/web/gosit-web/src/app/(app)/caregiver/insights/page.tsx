import type { Metadata } from "next";
import Link from "next/link";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { buttonClassName } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { getInsights, getMyCaregiverProfile } from "@/features/caregivers/api";
import { InsightsLocked, InsightsPanel } from "@/features/caregivers/components/insights-panel";
import { caregiverNav } from "@/features/caregivers/nav";
import { getMySubscription } from "@/features/subscriptions/api";
import { SubscriptionFeature } from "@/features/subscriptions/types";

export const metadata: Metadata = {
  title: "How you are doing",
  description: "How often you are seen, how you reply, and the families who come back.",
};

/**
 * The caregiver's own figures — the screen behind CAREGIVER_INSIGHTS.
 *
 * Entitlement is resolved here before the endpoint is called, so a caregiver on the free
 * plan sees what the plan offers rather than an error. The API enforces the same rule
 * independently; this only decides what to render.
 *
 * Figma node `148:3` (Caregiver Stats) could not be inspected — the Figma MCP seat is at
 * its tool-call limit — so the layout follows the caregiver screens already in the repo:
 * a stat grid over sections ordered by what a caregiver can act on.
 */
export default async function CaregiverInsightsPage() {
  const [profile, subscription] = await Promise.all([
    getMyCaregiverProfile(),
    getMySubscription(),
  ]);

  const shell = (children: React.ReactNode) => (
    <DashboardShell
      items={caregiverNav}
      currentPath="/caregiver/insights"
      heading="How you are doing"
      description="How often you are seen, how you reply, and the families who come back."
    >
      {children}
    </DashboardShell>
  );

  if (!profile) {
    return shell(
      <div className="max-w-2xl">
        <EmptyState
          title="Set up your profile first"
          description="There is nothing to measure until families can find you and ask you to work."
          action={
            <Link href="/caregiver/account" className={buttonClassName()}>
              Set up your profile
            </Link>
          }
        />
      </div>,
    );
  }

  if (!subscription.features.includes(SubscriptionFeature.CaregiverInsights)) {
    return shell(<InsightsLocked planHref="/caregiver/plan" />);
  }

  const insights = await getInsights();

  if (!insights) {
    return shell(
      <div className="max-w-2xl">
        <EmptyState
          title="Nothing to show yet"
          description="Once families start booking you, your figures will appear here."
        />
      </div>,
    );
  }

  return shell(
    <div className="@container">
      <InsightsPanel insights={insights} />
    </div>,
  );
}
