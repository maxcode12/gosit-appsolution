/**
 * Review contracts.
 *
 * Reviews are written blind: neither side sees the other's until both have written, or
 * the two-week window closes. That is the whole design — it is what stops a review being
 * a negotiation — and the UI has to reflect it rather than quietly showing a draft as
 * though it were live.
 */

/** A published review as shown on a profile. */
export interface PublishedReview {
  id: string;
  rating: number;
  comment: string | null;
  authorFirstName: string;
  authorLastInitial: string;
  createdAtUtc: string;
}

export interface CaregiverReviews {
  averageRating: number | null;
  reviewCount: number;
  reviews: PublishedReview[];
}

/** Formats an author as "Daniel O." — all the contract carries, and all a reader needs. */
export function authorName(review: PublishedReview): string {
  return review.authorLastInitial
    ? `${review.authorFirstName} ${review.authorLastInitial}.`
    : review.authorFirstName;
}

/** Counts of each star rating, highest first, for the distribution bars. */
export function ratingBreakdown(
  reviews: PublishedReview[],
): { rating: number; count: number; share: number }[] {
  return [5, 4, 3, 2, 1].map((rating) => {
    const count = reviews.filter((review) => review.rating === rating).length;

    return {
      rating,
      count,
      share: reviews.length === 0 ? 0 : count / reviews.length,
    };
  });
}
