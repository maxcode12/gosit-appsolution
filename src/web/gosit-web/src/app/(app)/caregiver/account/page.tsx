import type { Metadata } from "next";
import Link from "next/link";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { getMyCaregiverProfile, getMyVerifications } from "@/features/caregivers/api";
import { AcceptingWorkToggle } from "@/features/caregivers/components/accepting-work-toggle";
import { AvailabilityForm } from "@/features/caregivers/components/availability-form";
import { CaregiverProfileForm } from "@/features/caregivers/components/caregiver-profile-form";
import { ListingStatus } from "@/features/caregivers/components/listing-status";
import { ServicesForm } from "@/features/caregivers/components/services-form";
import { caregiverNav } from "@/features/caregivers/nav";
import { getCareServiceCatalogue } from "@/features/search/api";

export const metadata: Metadata = {
  title: "Account",
  description: "Your profile, services, availability and checks.",
};

/**
 * Everything a caregiver sets up about themselves — wireframe `148:87`.
 *
 * One long page rather than a wizard. Setup is not linear: someone waiting on a
 * vulnerable sector check should still be able to write their bio and set their hours,
 * and a wizard would make them walk past the step they cannot finish every time.
 *
 * Ordered by what gets them listed soonest, with the listing status pinned at the top
 * so the reason for all of it stays visible.
 */
export default async function CaregiverAccountPage() {
  const [profile, verifications, catalogue] = await Promise.all([
    getMyCaregiverProfile(),
    getMyVerifications(),
    getCareServiceCatalogue(),
  ]);

  return (
    <DashboardShell
      items={caregiverNav}
      currentPath="/caregiver/account"
      heading="Account"
      description="Your profile, what you offer, and your checks."
    >
      <div className="flex max-w-2xl flex-col gap-8">
        <nav aria-label="Account sections" className="grid gap-3 sm:grid-cols-2">
          <Link href="/caregiver/plan" className="surface-card-interactive block p-4">
            <p className="font-heading font-semibold text-text-primary">Your plan</p>
            <p className="mt-1 text-sm text-text-secondary">
              What GoSit includes for you.
            </p>
          </Link>
          <Link href="/caregiver/referrals" className="surface-card-interactive block p-4">
            <p className="font-heading font-semibold text-text-primary">
              Invite caregivers
            </p>
            <p className="mt-1 text-sm text-text-secondary">
              Share your code with someone else who would be good at this.
            </p>
          </Link>
        </nav>

        {profile ? (
          <>
            <ListingStatus profile={profile} verifications={verifications} />
            <AcceptingWorkToggle profile={profile} />
          </>
        ) : null}

        <Section
          id="about"
          heading="About you"
          description="What families see, and what you charge."
        >
          <CaregiverProfileForm profile={profile} />
        </Section>

        {profile ? (
          <>
            <Section
              id="services"
              heading="What you offer"
              description="The kinds of care families can book you for."
            >
              <ServicesForm
                categories={catalogue.categories}
                selected={profile.serviceCodes}
                heldCertifications={profile.certifications
                  .filter((certification) => certification.isValid)
                  .map((certification) => certification.code)}
              />
            </Section>

            <Section
              id="availability"
              heading="When you are free"
              description="Your usual week. You can still decline anything."
            >
              <AvailabilityForm availability={profile.availability} />
            </Section>

            <Section
              id="documents"
              heading="Documents"
              description="Your checks, insurance, service agreement and certificates."
            >
              <Link href="/caregiver/documents" className="surface-card-interactive block p-4">
                <p className="font-heading font-semibold text-text-primary">Manage your documents</p>
                <p className="mt-1 text-sm text-text-secondary">
                  See what is required, what is in review, and renew anything that is about to
                  expire.
                </p>
              </Link>
            </Section>
          </>
        ) : (
          <p className="text-sm text-text-secondary">
            Save your profile above, then you can add your services, hours,
            certifications and checks.
          </p>
        )}
      </div>
    </DashboardShell>
  );
}

function Section({
  id,
  heading,
  description,
  children,
}: {
  id: string;
  heading: string;
  description: string;
  children: React.ReactNode;
}) {
  return (
    <section id={id} className="scroll-mt-24 border-t border-border pt-8 first:border-0 first:pt-0">
      <h2 className="font-heading text-lg font-medium text-text-primary">{heading}</h2>
      <p className="mt-1 text-sm text-text-secondary">{description}</p>
      <div className="mt-4">{children}</div>
    </section>
  );
}
