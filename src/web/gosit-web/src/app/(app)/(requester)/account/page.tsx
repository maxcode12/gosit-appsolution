import type { Metadata } from "next";
import Link from "next/link";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { getMyProfile } from "@/features/account/api";
import { CareRecipients } from "@/features/account/components/care-recipients";
import { EmergencyContacts } from "@/features/account/components/emergency-contacts";
import { Pets } from "@/features/account/components/pets";
import { ProfileForm } from "@/features/account/components/profile-form";
import { careRequesterNav } from "@/features/search/nav";

export const metadata: Metadata = {
  title: "Account",
  description: "Your details and the people you arrange care for.",
};

/**
 * The care requester's account.
 *
 * Two things live here because two things must be true before anyone can book: a profile
 * with an address, and at least one care recipient. The booking flow sends people here
 * when either is missing, so the page is written to be finishable in one sitting.
 */
export default async function AccountPage() {
  const profile = await getMyProfile();

  return (
    <DashboardShell
      items={careRequesterNav}
      currentPath="/account"
      heading="Account"
      description="Your details, and who you arrange care for."
    >
      <div className="flex max-w-2xl flex-col gap-8">
        <nav aria-label="Account sections" className="grid gap-3 sm:grid-cols-2">
          <AccountLink
            href="/account/plan"
            title="Your plan"
            body="What GoSit includes for you."
          />
          <AccountLink
            href="/account/referrals"
            title="Invite friends"
            body="Share your code with another family."
          />
        </nav>

        <section>
          <h2 className="font-heading text-lg font-medium text-text-primary">Your details</h2>
          <div className="mt-4">
            <ProfileForm profile={profile} />
          </div>
        </section>

        <section className="border-t border-border pt-8">
          <h2 className="font-heading text-lg font-medium text-text-primary">
            Who care is for
          </h2>
          <p className="mt-1 text-sm text-text-secondary">
            Add everyone you might book care for. What you record about them stays private
            until you book someone.
          </p>

          <div className="mt-4">
            {profile ? (
              <CareRecipients recipients={profile.careRecipients} />
            ) : (
              // A recipient belongs to a profile, so there is nothing to attach one to
              // yet. Saying so beats an add form that would fail on submit.
              <p className="text-sm text-text-secondary">
                Save your details above first — the people you add are kept with your
                profile.
              </p>
            )}
          </div>
        </section>

        <section className="border-t border-border pt-8">
          <h2 className="font-heading text-lg font-medium text-text-primary">Pets</h2>
          <p className="mt-1 text-sm text-text-secondary">
            For dog walks, drop-in visits and pet sitting. Care notes stay private until a
            sitter accepts your booking.
          </p>

          <div className="mt-4">
            {profile ? (
              <Pets pets={profile.pets} />
            ) : (
              <p className="text-sm text-text-secondary">
                Pets are kept with your profile too, so save your details first.
              </p>
            )}
          </div>
        </section>

        <section className="border-t border-border pt-8">
          <h2 className="font-heading text-lg font-medium text-text-primary">
            If we cannot reach you
          </h2>
          <p className="mt-1 text-sm text-text-secondary">
            Someone a caregiver can call during a booking if your phone goes unanswered.
            They see this only after accepting one of your bookings.
          </p>

          <div className="mt-4">
            {profile ? (
              <EmergencyContacts contacts={profile.emergencyContacts} />
            ) : (
              // Deliberately not the same sentence as the section above. Repeating an
              // instruction the reader has already been given twice on one screen reads
              // as a form error rather than as context.
              <p className="text-sm text-text-secondary">
                You can add contacts once your profile is saved.
              </p>
            )}
          </div>
        </section>
      </div>
    </DashboardShell>
  );
}

/** A card linking to one of the account's sub-screens. */
function AccountLink({
  href,
  title,
  body,
}: {
  href: string;
  title: string;
  body: string;
}) {
  return (
    <Link href={href} className="surface-card-interactive block p-4">
      <p className="font-heading font-semibold text-text-primary">{title}</p>
      <p className="mt-1 text-sm text-text-secondary">{body}</p>
    </Link>
  );
}
