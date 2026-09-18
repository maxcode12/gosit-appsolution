import type { AddressDto } from "@/features/bookings/address";

/**
 * The caregiver's own view of their data.
 *
 * Distinct from the public `CaregiverDetail` in the search feature: this one carries the
 * exact address and the review state of each certification, neither of which a family
 * ever sees.
 */

export type VerificationStatus = "Unverified" | "Pending" | "Verified" | "Rejected";

export type CertificationStatus = "Pending" | "Verified" | "Rejected";

export interface CaregiverCertification {
  id: string;
  code: string;
  name: string;
  issuedOn: string;
  expiresOn: string | null;
  status: CertificationStatus;
  /** Verified and not expired — the only state that confers eligibility. */
  isValid: boolean;
}

export type InsuranceCoverageType = "GeneralLiability" | "ProfessionalLiability" | "Automobile" | "Other";

export type InsuranceVerificationStatus = "Pending" | "Verified" | "Rejected";

export const INSURANCE_COVERAGE_LABELS: Record<InsuranceCoverageType, string> = {
  GeneralLiability: "Commercial general liability",
  ProfessionalLiability: "Professional liability",
  Automobile: "Automobile",
  Other: "Other",
};

export interface CaregiverInsurancePolicy {
  id: string;
  coverageType: InsuranceCoverageType;
  insurer: string;
  policyReference: string;
  liabilityLimit: number;
  effectiveOn: string;
  expiresOn: string;
  status: InsuranceVerificationStatus;
  /** Verified and not expired — the only state that confers eligibility, once eligibility uses it. */
  isValid: boolean;
}

export interface AvailabilityWindow {
  day: string;
  /** "08:00:00" as the API stores it. */
  startTime: string;
  endTime: string;
}

export interface CaregiverProfile {
  id: string;
  bio: string | null;
  yearsOfExperience: number;
  hourlyRate: number;
  /** Price per night for boarding and in-home sitting, or null for someone who offers neither. */
  nightlyRate: number | null;
  address: AddressDto | null;
  serviceRadiusKm: number;
  verificationStatus: VerificationStatus;
  acceptingWork: boolean;
  /** Every condition for appearing in search, resolved server-side. */
  isListable: boolean;
  availability: AvailabilityWindow[];
  certifications: CaregiverCertification[];
  insurancePolicies: CaregiverInsurancePolicy[];
  serviceCodes: string[];

  /** The Caregiver Service Agreement version this caregiver has signed, or null. */
  agreementAcceptedVersion: string | null;
  agreementAcceptedAtUtc: string | null;
  /** False either because nothing is signed, or because the agreement changed since. */
  hasAcceptedCurrentAgreement: boolean;
  /** The version a caregiver would be signing if they accept right now. */
  currentAgreementVersion: string;
}

export interface UpsertCaregiverProfileInput {
  bio: string | null;
  yearsOfExperience: number;
  hourlyRate: number;
  nightlyRate: number | null;
  address: AddressDto | null;
  serviceRadiusKm: number;
}

/** One booking's contribution to earnings, gross and fee shown alongside the net. */
export interface StatementLine {
  payoutId: string;
  careBookingId: string;
  gross: number;
  platformFee: number;
  net: number;
  commissionRate: number;
  status: string;
  createdAtUtc: string;
  paidAtUtc: string | null;
  /**
   * When this may be sent — the close of the family's dispute window.
   *
   * A caregiver waits it out before money moves, because before that a dispute arrived
   * after the payout had gone. A balance sitting still with no date reads as a bug to the
   * person who has already done the work.
   */
  availableAtUtc: string;
  /** Stopped by an open dispute: a decision to wait on, not a date. */
  isHeldForDispute: boolean;
}

/**
 * Where a caregiver stands with payouts.
 *
 * The three states are deliberately distinguishable — "not started", "submitted and
 * under review", and "ready" call for different things to be said, and a single boolean
 * would leave someone who has done everything wondering why they are still being told
 * to set up payouts.
 */
export interface PayoutAccount {
  /** Whether a payout provider is configured at all. */
  billingConfigured: boolean;
  hasAccount: boolean;
  detailsSubmitted: boolean;
  payoutsEnabled: boolean;
  /** Why the provider is holding the account, when it is. */
  disabledReason: string | null;
  /** Earned and owed, but not yet settled — includes transfers in flight. */
  pendingBalance: number;
  currency: string;
}

export interface Earnings {
  currency: string;
  /** Paid out and settled. */
  totalPaid: number;
  /** Earned and owed, but not yet transferred. */
  pendingBalance: number;
  lifetimeGross: number;
  lifetimePlatformFees: number;
  statement: StatementLine[];
}

export type VerificationCheckType =
  | "Identity"
  | "BackgroundCheck"
  | "VulnerableSector"
  | "References";

export interface VerificationCase {
  id: string;
  checkType: VerificationCheckType;
  status: string;
  validUntil: string | null;
  /** Present only on a rejection, so a caregiver knows what to fix. */
  decisionNotes: string | null;
  submittedAtUtc: string;
  reviewedAtUtc: string | null;
}

export interface VerificationSummary {
  verificationStatus: VerificationStatus;
  meetsListingRequirements: boolean;
  /**
   * The checks this caregiver needs, from the server: photo ID and a vulnerable sector
   * check to care for people, photo ID and a criminal record check for pet care only.
   */
  requiredChecks: VerificationCheckType[];
  clearedForPeopleCare: boolean;
  cases: VerificationCase[];
}

/**
 * The checks that must be valid before a caregiver can be listed, mirroring the domain's
 * `VerificationRequirements.RequiredForListing`. Duplicated here only to explain the
 * requirement in the UI — `meetsListingRequirements` from the API remains the authority.
 */
export const REQUIRED_FOR_LISTING: VerificationCheckType[] = ["Identity", "VulnerableSector"];

export const CHECK_LABELS: Record<VerificationCheckType, string> = {
  Identity: "Photo ID",
  BackgroundCheck: "Criminal record check",
  VulnerableSector: "Vulnerable sector check",
  References: "References",
};

/** Care needs as disclosed to a booked caregiver. */
export interface BookingCareNeeds {
  careRecipientId: string;
  firstName: string;
  age: number;
  recipientType: string;
  careNeeds: {
    mobilityNeeds: string | null;
    communicationStyle: string | null;
    sensoryNeeds: string | null;
    behaviourSupportNotes: string | null;
    dietaryRestrictions: string | null;
    allergies: string | null;
    toiletingAssistance: boolean;
    feedingAssistance: boolean;
    routinePreferences: string | null;
    triggers: string | null;
    calmingStrategies: string | null;
    emergencyInstructions: string | null;
    requiredSkills: string[];
    updatedAtUtc: string | null;
  };
}

/**
 * How a caregiver's own work is going.
 *
 * Rates are null when the denominator is zero — a share of nothing is unknown, not zero
 * percent, and rendering "0% accepted" to someone who has had no requests would be a
 * criticism of something that never happened.
 *
 * `searchPerformanceAvailable` is now true: search appearances and profile views are
 * recorded. The flag stays in the contract because a deployment that has not started
 * counting is indistinguishable from a caregiver nobody has seen, and the screen has a
 * different sentence for each.
 *
 * The search figures are counts and nothing else — no family is named or identifiable.
 * That is the design, not a limitation of it.
 */
export interface CaregiverInsights {
  windowDays: number;

  requestsReceived: number;
  accepted: number;
  declined: number;
  lapsedUnanswered: number;
  /** Still open and still answerable — not a missed reply. */
  awaitingYourReply: number;
  /** Times shown on a page of results someone saw. Counts, never who searched. */
  searchAppearances: number;
  profileViews: number;
  /** Null rather than zero when they have not been shown at all. */
  viewRate: number | null;
  cancelledByYou: number;

  acceptanceRate: number | null;
  responseRate: number | null;
  medianMinutesToAccept: number | null;

  completedInWindow: number;
  completedLifetime: number;
  repeatFamilies: number;

  averageRating: number | null;
  reviewCount: number;

  searchPerformanceAvailable: boolean;
}

/**
 * Someone the caregiver can ring when the family cannot be reached.
 *
 * Disclosed only on a booking that has been taken on, and every read is audited by the
 * API. The relationship is what decides who to try first.
 */
export interface EmergencyContact {
  id: string;
  fullName: string;
  relationship: string;
  phoneNumber: string;
}
