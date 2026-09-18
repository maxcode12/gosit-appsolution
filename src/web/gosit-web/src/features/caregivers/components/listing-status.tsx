import Link from "next/link";
import { buttonClassName } from "@/components/ui/button";
import { REQUIRED_FOR_LISTING, CHECK_LABELS } from "@/features/caregivers/types";
import type { CaregiverProfile, VerificationSummary } from "@/features/caregivers/types";

/**
 * Whether this caregiver appears in search, and if not, exactly what is missing.
 *
 * `isListable` is a single server-side boolean, which is the right thing to trust and
 * the wrong thing to show on its own — "you are not listed" without a reason leaves
 * someone stuck. So each condition is re-derived here purely to explain the answer the
 * server already gave.
 */
export function ListingStatus({
  profile,
  verifications,
}: {
  profile: CaregiverProfile;
  verifications: VerificationSummary | null;
}) {
  if (profile.isListable) {
    return (
      <section className="rounded-card border border-border bg-surface p-5">
        <div className="flex items-center gap-2">
          <span aria-hidden className="size-2.5 rounded-full bg-brand-primary" />
          <h2 className="font-heading text-base font-medium text-text-primary">
            You are visible to families
          </h2>
        </div>
        <p className="mt-1 text-sm text-text-secondary">
          Families searching near {profile.address?.city ?? "you"} can find you and send
          requests.
        </p>
      </section>
    );
  }

  const blockers = listingBlockers(profile, verifications);

  return (
    <section className="rounded-card border border-brand-accent/40 bg-brand-accent/5 p-5">
      <div className="flex items-center gap-2">
        <span aria-hidden className="size-2.5 rounded-full bg-brand-accent" />
        <h2 className="font-heading text-base font-medium text-text-primary">
          Not yet visible to families
        </h2>
      </div>

      <p className="mt-1 text-sm text-text-secondary">
        {blockers.length === 1
          ? "One thing left before you appear in search."
          : `${blockers.length} things left before you appear in search.`}
      </p>

      <ul className="mt-4 flex flex-col gap-2">
        {blockers.map((blocker) => (
          <li key={blocker} className="flex items-start gap-2 text-sm text-text-primary">
            <span aria-hidden className="mt-2 size-1.5 shrink-0 rounded-full bg-text-secondary" />
            {blocker}
          </li>
        ))}
      </ul>

      <Link href="/caregiver/account" className={buttonClassName("primary", "mt-4")}>
        Finish setting up
      </Link>
    </section>
  );
}

/**
 * Restates the domain's listing rule in the caregiver's own words.
 *
 * Kept deliberately in step with `CaregiverProfile.IsListable`: accepting work, verified,
 * an address, at least one availability window, a rate above zero, and a current
 * signature on the Caregiver Service Agreement.
 */
function listingBlockers(
  profile: CaregiverProfile,
  verifications: VerificationSummary | null,
): string[] {
  const blockers: string[] = [];

  if (!profile.address) {
    blockers.push("Add the address you work from, so families nearby can find you.");
  }

  if (profile.hourlyRate <= 0) {
    blockers.push("Set your hourly rate.");
  }

  if (profile.availability.length === 0) {
    blockers.push("Add the hours you are usually free.");
  }

  if (profile.verificationStatus !== "Verified") {
    const submitted = new Set(verifications?.cases.map((c) => c.checkType) ?? []);
    // Which checks depends on what they offer, so the server's list wins when present.
    const missing = (verifications?.requiredChecks ?? REQUIRED_FOR_LISTING).filter((check) => !submitted.has(check));

    blockers.push(
      missing.length > 0
        ? `Start your ${missing.map((check) => CHECK_LABELS[check].toLowerCase()).join(" and ")}.`
        : "Your checks are with our team. We will email you when they are done.",
    );
  }

  if (!profile.hasAcceptedCurrentAgreement) {
    blockers.push(
      profile.agreementAcceptedVersion
        ? "Sign the Caregiver Service Agreement again — it has changed since you last signed."
        : "Sign the Caregiver Service Agreement.",
    );
  }

  if (!profile.acceptingWork && blockers.length === 0) {
    blockers.push("Turn on accepting work when you are ready for requests.");
  }

  return blockers;
}
