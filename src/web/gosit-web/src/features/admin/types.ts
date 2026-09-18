/**
 * Staff contracts.
 *
 * These carry more about a person than anything else in the product: a full name, an
 * email address, an account status. That is deliberate and admin-gated — someone
 * deciding whether to let a stranger into a family's home, or whether to remove an
 * account, cannot do it from a GUID. Nothing here may be rendered outside /admin.
 */

export type VerificationCheckType =
  | "Identity"
  | "BackgroundCheck"
  | "VulnerableSector"
  | "References";

export interface PendingVerification {
  caseId: string;
  caregiverProfileId: string;
  caregiverUserId: string;
  caregiverName: string;
  caregiverEmail: string;
  caregiverAccountStatus: string;
  checkType: VerificationCheckType;
  status: string;
  submittedAtUtc: string;
}

export interface PendingCertification {
  caregiverCertificationId: string;
  caregiverProfileId: string;
  caregiverUserId: string;
  caregiverName: string;
  caregiverEmail: string;
  code: string;
  name: string;
  issuedOn: string;
  expiresOn: string | null;
  /** Already lapsed — approving it would change nothing. */
  isExpired: boolean;
  /**
   * Whether the caregiver uploaded the certificate.
   *
   * False means the API will refuse to approve it: there is nothing to verify. The key
   * itself never reaches the browser — the file is fetched through a route that
   * authorises and audits the request.
   */
  hasDocument: boolean;
  submittedAtUtc: string;
}

export type InsuranceCoverageType = "GeneralLiability" | "ProfessionalLiability" | "Automobile" | "Other";

export const INSURANCE_COVERAGE_LABELS: Record<InsuranceCoverageType, string> = {
  GeneralLiability: "Commercial general liability",
  ProfessionalLiability: "Professional liability",
  Automobile: "Automobile",
  Other: "Other",
};

/**
 * A caregiver's insurance policy waiting on a decision.
 *
 * GoSit verifies a caregiver's own coverage; it never issues or holds it — see
 * docs/BUSINESS_MODEL.md. Mirrors PendingCertification: the key to the uploaded document
 * never reaches the browser, only whether one exists.
 */
export interface PendingInsurance {
  caregiverInsurancePolicyId: string;
  caregiverProfileId: string;
  caregiverUserId: string;
  caregiverName: string;
  caregiverEmail: string;
  coverageType: InsuranceCoverageType;
  insurer: string;
  policyReference: string;
  liabilityLimit: number;
  effectiveOn: string;
  expiresOn: string;
  isExpired: boolean;
  hasDocument: boolean;
  submittedAtUtc: string;
}

export interface AdminIncident {
  id: string;
  reportedByUserId: string;
  reportedByName: string;
  subjectUserId: string;
  subjectName: string;
  subjectEmail: string;
  subjectRole: string;
  subjectAccountStatus: string;
  careBookingId: string | null;
  category: string;
  severity: string;
  status: string;
  requiresUrgentReview: boolean;
  description: string;
  assignedToUserId: string | null;
  createdAtUtc: string;
}

export const CHECK_LABELS: Record<VerificationCheckType, string> = {
  Identity: "Photo ID",
  BackgroundCheck: "Criminal record check",
  VulnerableSector: "Vulnerable sector check",
  References: "References",
};

/**
 * The checks a caregiver must pass before they can be listed. Approving anything else
 * is useful but does not clear them to work, and the queue says so.
 */
export const REQUIRED_FOR_LISTING: VerificationCheckType[] = ["Identity", "VulnerableSector"];

/** Severities in the order staff should work through them. */
export const SEVERITY_ORDER = ["Critical", "High", "Medium", "Low"] as const;

/**
 * A promo code as staff see it.
 *
 * `status` rather than `isActive` alone: a code stops working for four different
 * reasons, and a bare flag reads "active" on one that expired last week or spent its
 * budget yesterday.
 */
export type PromoCodeStatus =
  | "Live"
  | "Scheduled"
  | "Expired"
  | "FullyRedeemed"
  | "Deactivated";

export interface AdminPromoCode {
  id: string;
  code: string;
  description: string;
  discountType: "Percentage" | "FixedAmount";
  value: number;
  /** Null means either side of the marketplace can use it. */
  audience: "CareRequester" | "Caregiver" | null;
  validFromUtc: string;
  validUntilUtc: string | null;
  maxRedemptions: number | null;
  redemptionCount: number;
  isActive: boolean;
  status: PromoCodeStatus;
}

export const PROMO_STATUS_LABELS: Record<PromoCodeStatus, string> = {
  Live: "Live",
  Scheduled: "Not started",
  Expired: "Expired",
  FullyRedeemed: "Fully redeemed",
  Deactivated: "Switched off",
};

/**
 * What each status means for the campaign, in the terms someone managing it thinks in.
 * "Fully redeemed" is the one worth spelling out — it is a budget that ran out, not a
 * mistake, and it is the most common reason a code someone is handing out stops working.
 */
export const PROMO_STATUS_HINTS: Record<PromoCodeStatus, string> = {
  Live: "Being accepted right now.",
  Scheduled: "Will start accepting on its start date.",
  Expired: "Past its end date.",
  FullyRedeemed: "The redemption cap has been reached.",
  Deactivated: "Switched off by staff.",
};

/**
 * A disputed booking waiting on a staff decision.
 *
 * Every one of these is holding a caregiver's payout. Until this queue existed a dispute
 * appeared on no screen at all, while the money it held had no way out.
 */
export interface OpenDispute {
  careBookingId: string;
  careRequesterName: string;
  caregiverName: string;
  careServiceName: string;
  startAtUtc: string;
  completedAtUtc: string | null;
  disputedAtUtc: string;
  /** What the disputing party said, in their words. */
  reason: string | null;
  /** What the family paid, and so the most that can go back to them. */
  amountPaid: number;
  /** What the caregiver is owed if the dispute is dismissed. */
  payoutAmount: number;
  /**
   * False when the transfer already left — a payout sent before the dispute was raised.
   * A refund is still possible, but it comes out of GoSit rather than the caregiver,
   * which staff should know before deciding rather than afterwards.
   */
  payoutIsRecoverable: boolean;
}

/**
 * A message flagged as possible contact sharing, waiting on a staff decision.
 *
 * There is deliberately no body here. The queue tells staff that something was flagged,
 * between whom and when; reading what was written is a separate request that the API
 * audits against the staff member. A preview field would quietly turn this screen into a
 * feed of strangers' private conversations.
 */
export interface FlaggedMessage {
  messageId: string;
  conversationId: string;
  senderName: string;
  recipientName: string;
  /** Which side wrote it, so staff can see the direction without opening it. */
  sentByCaregiver: boolean;
  sentAtUtc: string;
}

/** One flagged message, with what was written. Fetched only when staff choose to read it. */
export interface FlaggedMessageDetail extends FlaggedMessage {
  body: string;
}

export interface AdminInterviewRecording {
  recordingId: string;
  status: "Recording" | "Available" | "Failed" | "Deleted";
  /** Finished, saved, and not yet deleted. The only state with anything to watch. */
  isWatchable: boolean;
  startedAtUtc: string;
  endedAtUtc: string | null;
  deleteAfterUtc: string | null;
  deletedAtUtc: string | null;
}

/**
 * A recorded interview, for Trust & Safety.
 *
 * Who and when, and what is still kept. No video and no link to one: watching is a
 * separate request the API audits against the staff member, and the screen says so before
 * anyone clicks. A hold keeps the recordings past their deletion date for a dispute or
 * incident.
 */
export interface AdminInterview {
  interviewId: string;
  careRequesterName: string;
  caregiverName: string;
  scheduledStartUtc: string;
  durationMinutes: number;
  status: string;
  recordingsOnHold: boolean;
  recordingHoldReason: string | null;
  recordingHeldAtUtc: string | null;
  recordings: AdminInterviewRecording[];
}

/**
 * A review whose subject has asked staff to look at it.
 *
 * The review's own words are here, and so is the complaint about them: the decision is
 * whether those words should stand, and there is nothing to withhold that the subject has
 * not already been shown on their own profile.
 */
export interface ReportedReview {
  reviewId: string;
  subjectProfileId: string;
  subjectIsCaregiver: boolean;
  subjectName: string;
  authorName: string;
  rating: number;
  comment: string | null;
  reportReason: string | null;
  reportedAtUtc: string;
  createdAtUtc: string;
}
