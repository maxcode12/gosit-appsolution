import type { Metadata } from "next";
import Link from "next/link";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { buttonClassName } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { getMyCaregiverProfile } from "@/features/caregivers/api";
import { caregiverNav } from "@/features/caregivers/nav";
import { getCaregiverReviews } from "@/features/reviews/api";
import { ReviewList } from "@/features/reviews/components/review-list";

export const metadata: Metadata = {
  title: "Reviews",
  description: "What families have said about your care.",
};

/**
 * A caregiver's own reviews.
 *
 * The same list families see on the profile, which is the point — there is no private
 * version and no way to hide one. What a caregiver gets that a family does not is the
 * explanation of why a review they are waiting on has not appeared yet.
 */
export default async function CaregiverReviewsPage() {
  const profile = await getMyCaregiverProfile();

  if (!profile) {
    return (
      <DashboardShell items={caregiverNav} currentPath="/caregiver/reviews" heading="Reviews">
        <div className="max-w-2xl">
          <EmptyState
            title="No profile yet"
            description="Set up your profile and complete your first booking. Reviews families write about you appear here."
            action={
              <Link href="/caregiver/account" className={buttonClassName()}>
                Set up your profile
              </Link>
            }
          />
        </div>
      </DashboardShell>
    );
  }

  const reviews = await getCaregiverReviews(profile.id, 50);

  return (
    <DashboardShell
      items={caregiverNav}
      currentPath="/caregiver/reviews"
      heading="Reviews"
      description="Exactly what families see on your profile."
    >
      <div className="flex max-w-3xl flex-col gap-6">
        <p className="rounded-card bg-surface-muted px-5 py-4 text-sm leading-relaxed text-text-primary">
          Reviews are written blind. After a booking finishes you and the family each
          have two weeks to write one, and neither appears until both are in or the
          window closes. Nobody can read yours and then decide what to write.
        </p>

        {/*
          Reportable here and nowhere else: these are the reader's own reviews. The same
          list on a caregiver's public profile is not, or anyone could queue up a
          competitor's reviews for staff to adjudicate.
        */}
        <ReviewList
          reviews={reviews}
          reportable
          emptyMessage="No published reviews yet. They appear here once a family has written one and the blind window has closed."
        />
      </div>
    </DashboardShell>
  );
}
