import { Avatar } from "@/components/ui/badges";
import { StarIcon } from "@/components/ui/icons";
import { ReportReview } from "@/features/reviews/components/report-review";
import { authorName, ratingBreakdown } from "@/features/reviews/types";
import type { CaregiverReviews } from "@/features/reviews/types";

/**
 * Reviews about a caregiver.
 *
 * The summary leads with the distribution rather than only the average, because "4.6"
 * and "4.6 with two one-star reviews" are different things and a family deserves to see
 * which one they are looking at.
 *
 * A caregiver with no reviews is not shown as zero stars — a new caregiver has not been
 * rated badly, and rendering them as though they had is a false signal that would keep
 * good people from ever getting a first booking.
 */
export function ReviewList({
  reviews,
  emptyMessage,
  reportable = false,
}: {
  reviews: CaregiverReviews;
  emptyMessage: string;
  /**
   * Whether these are the reader's own reviews, and so reportable. Off by default: this
   * list is also the public one on a caregiver's profile, where offering to report
   * someone else's review would let anyone queue up a competitor's.
   */
  reportable?: boolean;
}) {
  if (reviews.reviewCount === 0 || reviews.reviews.length === 0) {
    return (
      <div className="rounded-card border border-dashed border-border-strong px-6 py-10 text-center">
        <span
          aria-hidden
          className="mx-auto mb-3 grid size-11 place-items-center rounded-full bg-surface-muted text-text-muted"
        >
          <StarIcon />
        </span>
        <p className="text-sm text-text-secondary">{emptyMessage}</p>
      </div>
    );
  }

  const breakdown = ratingBreakdown(reviews.reviews);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-6 rounded-card bg-surface-muted p-6 sm:flex-row sm:items-center">
        <div className="text-center sm:w-40 sm:shrink-0">
          <p className="font-heading text-5xl font-extrabold text-text-primary">
            {reviews.averageRating?.toFixed(1) ?? "—"}
          </p>
          {/*
            Stars and a count, not the average again — the big number above already
            says 5.0, and RatingStars would repeat it directly underneath.
          */}
          <div className="mt-2 flex justify-center">
            <Stars rating={Math.round(reviews.averageRating ?? 0)} />
          </div>
          <p className="mt-1.5 text-sm text-text-secondary">
            {reviews.reviewCount} {reviews.reviewCount === 1 ? "review" : "reviews"}
          </p>
        </div>

        <dl className="flex-1">
          {breakdown.map((row) => (
            <div key={row.rating} className="flex items-center gap-3">
              <dt className="w-12 shrink-0 text-sm text-text-secondary">
                {row.rating} star
              </dt>
              <dd className="flex flex-1 items-center gap-3">
                <span
                  aria-hidden
                  className="h-2 flex-1 overflow-hidden rounded-pill bg-border"
                >
                  <span
                    className="block h-full rounded-pill bg-brand-accent transition-[width] duration-500"
                    style={{ width: `${Math.round(row.share * 100)}%` }}
                  />
                </span>
                <span className="w-6 shrink-0 text-right text-sm text-text-secondary">
                  {row.count}
                </span>
              </dd>
            </div>
          ))}
        </dl>
      </div>

      <ul className="flex flex-col gap-4">
        {reviews.reviews.map((review) => (
          <li key={review.id} className="surface-card p-5">
            <div className="flex items-start gap-3">
              <Avatar name={review.authorFirstName} size="sm" />

              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-baseline justify-between gap-2">
                  <p className="font-heading font-semibold text-text-primary">
                    {authorName(review)}
                  </p>
                  <time
                    dateTime={review.createdAtUtc}
                    className="text-xs text-text-secondary"
                  >
                    {new Date(review.createdAtUtc).toLocaleDateString("en-CA", {
                      month: "long",
                      year: "numeric",
                    })}
                  </time>
                </div>

                <div className="mt-1">
                  <Stars rating={review.rating} />
                </div>

                {review.comment ? (
                  <p className="mt-3 whitespace-pre-wrap text-sm leading-relaxed text-text-primary">
                    {review.comment}
                  </p>
                ) : (
                  <p className="mt-3 text-sm italic text-text-secondary">
                    Rated without a comment.
                  </p>
                )}

                {reportable ? <ReportReview reviewId={review.id} /> : null}
              </div>
            </div>
          </li>
        ))}
      </ul>

      {reviews.reviewCount > reviews.reviews.length ? (
        <p className="text-center text-sm text-text-secondary">
          Showing the most recent {reviews.reviews.length} of {reviews.reviewCount}{" "}
          reviews.
        </p>
      ) : null}
    </div>
  );
}

function Stars({ rating }: { rating: number }) {
  return (
    <span
      className="flex text-brand-accent"
      aria-label={`Rated ${rating} out of 5`}
      role="img"
    >
      {[1, 2, 3, 4, 5].map((star) => (
        <StarIcon key={star} filled={star <= rating} />
      ))}
    </span>
  );
}
