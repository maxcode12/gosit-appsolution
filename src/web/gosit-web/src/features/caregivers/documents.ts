import type {
  CaregiverProfile,
  CaregiverCertification,
  CaregiverInsurancePolicy,
  VerificationSummary,
} from "@/features/caregivers/types";
import { CHECK_LABELS, REQUIRED_FOR_LISTING } from "@/features/caregivers/types";

/**
 * Where a document stands, in the words a caregiver acts on:
 *  - `missing`   nothing on file yet
 *  - `review`    submitted, with our team
 *  - `valid`     approved and in date
 *  - `expiring`  approved, but ends within {@link EXPIRING_SOON_DAYS}
 *  - `expired`   was approved, is no longer in date
 *  - `rejected`  our team could not accept it
 */
export type DocumentState = "missing" | "review" | "valid" | "expiring" | "expired" | "rejected";

export interface DocumentItem {
  id: string;
  title: string;
  detail: string;
  required: boolean;
  state: DocumentState;
  /** When the approved document ends, for the ones that end. */
  expiresOn: string | null;
  /** Extra line under the title — a reviewer's note, or why a certificate is required. */
  note: string | null;
}

/** Matches the server's expiry notice, which starts 30 days ahead. */
export const EXPIRING_SOON_DAYS = 30;

/**
 * The wording of the action a caregiver takes, by document and state. "Renew" only once
 * something is on file, and the verbs differ because the work does: checks are started, the
 * agreement is signed, insurance and certificates are uploaded.
 */
export function documentAction(item: Pick<DocumentItem, "id" | "required" | "state">): string {
  const { id, state } = item;

  if (id === "agreement") {
    return state === "valid" ? "View" : state === "expired" ? "Sign again" : "Sign";
  }

  if (id === "checks") {
    return state === "valid" || state === "review" ? "View" : state === "rejected" ? "Start again" : "Start";
  }

  if (!item.required && state === "missing") {
    return "Add";
  }

  switch (state) {
    case "missing":
      return "Upload";
    case "rejected":
      return "Upload again";
    case "expiring":
    case "expired":
      return "Renew";
    default:
      return "Update";
  }
}

/** Whether this document is something the caregiver has to do something about. */
export function needsAttention(state: DocumentState): boolean {
  return state === "missing" || state === "rejected" || state === "expired" || state === "expiring";
}

/**
 * The required and optional documents, with a state for each, derived from what the API
 * already returns. Nothing here decides anything: whether a caregiver is listed or bookable
 * stays the server's call (`profile.isListable`). This only explains it.
 *
 * `requiredCertificationCodes` are the certificates the caregiver's chosen services ask for,
 * so a certificate shows as required only when something they offer needs it.
 */
export function buildDocuments(
  profile: CaregiverProfile,
  verifications: VerificationSummary | null,
  requiredCertificationCodes: string[],
  today: Date = new Date(),
): DocumentItem[] {
  const items: DocumentItem[] = [
    checksDocument(profile, verifications, today),
    insuranceDocument(profile.insurancePolicies, today),
    agreementDocument(profile),
  ];

  const held = profile.certifications;
  const requiredCodes = [...new Set(requiredCertificationCodes)].sort();

  for (const code of requiredCodes) {
    items.push(certificateDocument(code, held, today));
  }

  items.push(otherCertificatesDocument(held, requiredCodes));

  return items;
}

function checksDocument(
  profile: CaregiverProfile,
  verifications: VerificationSummary | null,
  today: Date,
): DocumentItem {
  const required = verifications?.requiredChecks ?? REQUIRED_FOR_LISTING;
  const cases = verifications?.cases ?? [];
  const requiredCases = cases.filter((c) => required.includes(c.checkType));
  const rejected = requiredCases.find((c) => c.status === "Rejected");

  const submitted = new Set(cases.map((c) => c.checkType));
  const missing = required.filter((check) => !submitted.has(check));

  // The nearest end date among the checks that have one. A check with no end date never lapses.
  const ends = requiredCases
    .map((c) => c.validUntil)
    .filter((d): d is string => Boolean(d))
    .sort();
  const expiresOn = ends[0] ?? null;

  let state: DocumentState;

  if (profile.verificationStatus === "Verified") {
    state = stateForExpiry(expiresOn, today);
  } else if (rejected) {
    state = "rejected";
  } else if (missing.length > 0) {
    state = "missing";
  } else {
    state = "review";
  }

  return {
    id: "checks",
    title: "Photo ID and background checks",
    detail: required.map((check) => CHECK_LABELS[check]).join(" and ") + ". Done through our screening process.",
    required: true,
    state,
    expiresOn,
    note: state === "rejected" ? (rejected?.decisionNotes ?? null) : null,
  };
}

const LIABILITY = new Set(["GeneralLiability", "ProfessionalLiability"]);

function insuranceDocument(policies: CaregiverInsurancePolicy[], today: Date): DocumentItem {
  // Only a liability policy counts as the required cover; automobile or other cover is extra.
  const liability = policies.filter((p) => LIABILITY.has(p.coverageType));
  const valid = liability.filter((p) => p.isValid).sort((a, b) => b.expiresOn.localeCompare(a.expiresOn));

  let state: DocumentState;
  let expiresOn: string | null = null;

  if (valid.length > 0) {
    expiresOn = valid[0].expiresOn;
    state = stateForExpiry(expiresOn, today);
  } else if (liability.some((p) => p.status === "Pending")) {
    state = "review";
  } else if (liability.some((p) => p.status === "Verified")) {
    state = "expired";
    expiresOn = liability.map((p) => p.expiresOn).sort().at(-1) ?? null;
  } else if (liability.some((p) => p.status === "Rejected")) {
    state = "rejected";
  } else {
    state = "missing";
  }

  return {
    id: "insurance",
    title: "Liability insurance",
    detail:
      "You carry your own cover as an independent caregiver. Upload your policy and our team will verify it. GoSit does not provide insurance.",
    required: true,
    state,
    expiresOn,
    note: null,
  };
}

function agreementDocument(profile: CaregiverProfile): DocumentItem {
  const signedOlder = !profile.hasAcceptedCurrentAgreement && profile.agreementAcceptedVersion !== null;

  return {
    id: "agreement",
    title: "Caregiver Service Agreement",
    detail: "Read and sign. Required before families can find you.",
    required: true,
    state: profile.hasAcceptedCurrentAgreement ? "valid" : signedOlder ? "expired" : "missing",
    expiresOn: null,
    note: signedOlder ? "This agreement has changed since you signed. Sign the new version." : null,
  };
}

function certificateDocument(
  code: string,
  held: CaregiverCertification[],
  today: Date,
): DocumentItem {
  const matching = held.filter((c) => c.code === code);
  const valid = matching
    .filter((c) => c.isValid)
    .sort((a, b) => (b.expiresOn ?? "9999").localeCompare(a.expiresOn ?? "9999"));

  let state: DocumentState;
  let expiresOn: string | null = null;

  if (valid.length > 0) {
    expiresOn = valid[0].expiresOn;
    state = stateForExpiry(expiresOn, today);
  } else if (matching.some((c) => c.status === "Pending")) {
    state = "review";
  } else if (matching.some((c) => c.status === "Verified")) {
    state = "expired";
    expiresOn = matching.map((c) => c.expiresOn ?? "").sort().at(-1) || null;
  } else if (matching.some((c) => c.status === "Rejected")) {
    state = "rejected";
  } else {
    state = "missing";
  }

  return {
    id: `certificate-${code}`,
    title: matching[0]?.name ?? humanise(code),
    detail: "Needed for a service you offer.",
    required: true,
    state,
    expiresOn,
    note: null,
  };
}

/**
 * The place to add or renew any certificate, and the summary of the ones no chosen service
 * asks for. Certificates a service does ask for appear as their own required rows above,
 * which point here.
 */
function otherCertificatesDocument(held: CaregiverCertification[], requiredCodes: string[]): DocumentItem {
  const others = held.filter((c) => !requiredCodes.includes(c.code));
  const valid = others.filter((c) => c.isValid);
  const waiting = others.filter((c) => c.status === "Pending").length;

  return {
    id: "certificates",
    title: "Certificates",
    detail: "First Aid, CPR and specialist training. Each one opens up more of the services you can offer.",
    required: false,
    state: valid.length > 0 ? "valid" : waiting > 0 ? "review" : "missing",
    expiresOn: null,
    note: others.length === 0 ? null : `${valid.length} approved${waiting > 0 ? `, ${waiting} with our team` : ""}.`,
  };
}

function stateForExpiry(expiresOn: string | null, today: Date): DocumentState {
  if (!expiresOn) return "valid";

  const days = daysUntil(expiresOn, today);

  if (days < 0) return "expired";
  if (days <= EXPIRING_SOON_DAYS) return "expiring";

  return "valid";
}

/** Whole days from today to the date, negative once it has passed. Compared as calendar days. */
export function daysUntil(isoDate: string, today: Date = new Date()): number {
  const [year, month, day] = isoDate.slice(0, 10).split("-").map(Number);
  const target = Date.UTC(year, month - 1, day);
  const start = Date.UTC(today.getFullYear(), today.getMonth(), today.getDate());

  return Math.round((target - start) / 86_400_000);
}

function humanise(code: string): string {
  const words = code.toLowerCase().replace(/_/g, " ");

  return words.charAt(0).toUpperCase() + words.slice(1);
}
