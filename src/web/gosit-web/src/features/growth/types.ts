/**
 * Referral contracts.
 *
 * Note what a referral does not carry: anything identifying the person who accepted it.
 * A referrer learns that an invitation converted, never who the account belongs to —
 * which is the right call, since "who did I invite that signed up" is a question with an
 * uncomfortable answer for the person being asked about.
 */

export type ReferralStatus = "Pending" | "Qualified" | "Rewarded" | "Expired";

export interface Referral {
  id: string;
  status: ReferralStatus;
  createdAtUtc: string;
  qualifiedAtUtc: string | null;
  rewardAmount: number | null;
}

export interface ReferralSummary {
  myReferralCode: string;
  pendingCount: number;
  qualifiedCount: number;
  rewardedCount: number;
  referrals: Referral[];
}

export const REFERRAL_STATUS_LABELS: Record<ReferralStatus, string> = {
  Pending: "Signed up",
  Qualified: "Completed a booking",
  Rewarded: "Reward granted",
  Expired: "Expired",
};

/**
 * What each status means for the referrer, in their terms.
 *
 * "Qualified" is the one that needs explaining: a referral only counts once the person
 * has actually completed a booking, not when they sign up. That is deliberate — paying
 * for signups is how a referral programme becomes a fraud programme — and someone
 * watching their invitations sit at "Signed up" deserves to know why.
 */
export const REFERRAL_STATUS_HINTS: Record<ReferralStatus, string> = {
  Pending: "They have joined but not completed a booking yet.",
  Qualified: "They have completed a booking. This one counts.",
  Rewarded: "Your reward for this referral has been granted.",
  Expired: "This invitation was not taken up in time.",
};
