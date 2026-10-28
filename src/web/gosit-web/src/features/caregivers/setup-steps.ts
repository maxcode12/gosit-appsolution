import { CHECK_LABELS, REQUIRED_FOR_LISTING } from "@/features/caregivers/types";
import type { CaregiverProfile, PayoutAccount, VerificationSummary } from "@/features/caregivers/types";

export type StepState = "done" | "review" | "todo";

/**
 * Why a step is on the list: `listing` steps gate appearing in search, `paid` steps gate
 * receiving money, `recommended` steps help but do not block anything today.
 */
export type StepKind = "listing" | "paid" | "recommended";

export interface SetupStep {
  id: string;
  title: string;
  detail: string;
  kind: StepKind;
  state: StepState;
  href: string;
  action: string;
}

/**
 * What a caregiver still has to do after the short sign-up form, in the order that gets them
 * listed soonest.
 *
 * Sign-up asks for a name, an email and a password and nothing else; everything GoSit needs
 * to know before it lists someone happens here instead, where it can be saved, left and
 * returned to. Each state is derived from data the API already sends. Whether the caregiver
 * is listable stays the server's decision (`profile.isListable`) — this only explains it.
 */
export function buildSetupSteps(
  profile: CaregiverProfile | null,
  verifications: VerificationSummary | null,
  payout: PayoutAccount | null,
): SetupStep[] {
  const about: SetupStep = {
    id: "about",
    title: "Your profile",
    detail: "Where you work, your rate and a short introduction for families.",
    kind: "listing",
    state: profile?.address && profile.hourlyRate > 0 ? "done" : "todo",
    href: "/caregiver/account#about",
    action: "Add profile",
  };

  if (!profile) {
    return [about, ...lockedSteps()];
  }

  const submitted = new Set(verifications?.cases.map((c) => c.checkType) ?? []);
  const required = verifications?.requiredChecks ?? REQUIRED_FOR_LISTING;
  const missingChecks = required.filter((check) => !submitted.has(check));

  const checks: SetupStep = {
    id: "checks",
    title: "Identity and background checks",
    detail:
      `${required.map((check) => CHECK_LABELS[check]).join(" and ")}. ` +
      "Done through our screening process; you will be asked for a photo of your ID.",
    kind: "listing",
    state:
      profile.verificationStatus === "Verified"
        ? "done"
        : missingChecks.length === 0
          ? "review"
          : "todo",
    href: "/caregiver/account#checks",
    action: "Start checks",
  };

  const anyPending = (items: { status: string }[]) => items.some((i) => i.status === "Pending");

  const certifications: SetupStep = {
    id: "certifications",
    title: "Certifications",
    detail: "First Aid, CPR and others. Each one opens up more of the services you can offer.",
    kind: "recommended",
    state: profile.certifications.some((c) => c.isValid)
      ? "done"
      : anyPending(profile.certifications)
        ? "review"
        : "todo",
    href: "/caregiver/account#certifications",
    action: "Add certificates",
  };

  const insurance: SetupStep = {
    id: "insurance",
    title: "Your insurance",
    detail:
      "You carry your own cover as an independent caregiver. Upload your policy and our team will verify it.",
    kind: "recommended",
    state: profile.insurancePolicies.some((p) => p.isValid)
      ? "done"
      : anyPending(profile.insurancePolicies)
        ? "review"
        : "todo",
    href: "/caregiver/account#insurance",
    action: "Add insurance",
  };

  const agreement: SetupStep = {
    id: "agreement",
    title: "Caregiver Service Agreement",
    detail: "Read and sign. Required before families can find you.",
    kind: "listing",
    state: profile.hasAcceptedCurrentAgreement ? "done" : "todo",
    href: "/caregiver/account#agreement",
    action: profile.agreementAcceptedVersion ? "Sign again" : "Read and sign",
  };

  const payouts: SetupStep = {
    id: "payouts",
    title: "Get paid",
    detail:
      payout && !payout.billingConfigured
        ? "Bank details are collected securely by our payments partner. Not open yet — we will tell you when it is."
        : "Add your bank details with our payments partner, who handles them directly. GoSit never sees your account number.",
    kind: "paid",
    state: payout?.payoutsEnabled ? "done" : payout?.detailsSubmitted ? "review" : "todo",
    href: "/caregiver/earnings",
    action: "Set up payouts",
  };

  return [
    { ...about, state: about.state },
    {
      id: "services",
      title: "What you offer",
      detail: "The kinds of care families can book you for.",
      kind: "listing",
      state: profile.serviceCodes.length > 0 ? "done" : "todo",
      href: "/caregiver/account#services",
      action: "Choose services",
    },
    {
      id: "availability",
      title: "When you are free",
      detail: "Your usual week. You can still decline anything.",
      kind: "listing",
      state: profile.availability.length > 0 ? "done" : "todo",
      href: "/caregiver/account#availability",
      action: "Set hours",
    },
    checks,
    certifications,
    insurance,
    agreement,
    payouts,
  ];
}

/** Before a profile exists the later steps cannot be saved, so they are listed but not offered. */
function lockedSteps(): SetupStep[] {
  const locked = (id: string, title: string, kind: StepKind): SetupStep => ({
    id,
    title,
    detail: "Available once your profile is saved.",
    kind,
    state: "todo",
    href: "/caregiver/account#about",
    action: "Save profile first",
  });

  return [
    locked("services", "What you offer", "listing"),
    locked("availability", "When you are free", "listing"),
    locked("checks", "Identity and background checks", "listing"),
    locked("certifications", "Certifications", "recommended"),
    locked("insurance", "Your insurance", "recommended"),
    locked("agreement", "Caregiver Service Agreement", "listing"),
    locked("payouts", "Get paid", "paid"),
  ];
}

export function setupProgress(steps: SetupStep[]): { done: number; total: number } {
  return { done: steps.filter((s) => s.state === "done").length, total: steps.length };
}
