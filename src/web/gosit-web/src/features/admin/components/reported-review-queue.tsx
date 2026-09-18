"use client";

import { useActionState, useState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { StarIcon } from "@/components/ui/icons";
import { textareaClassName } from "@/components/ui/text-input";
import { moderateReviewAction, type AdminFormState } from "@/features/admin/actions";
import { waitedFor } from "@/features/admin/components/subject-line";
import type { ReportedReview } from "@/features/admin/types";

/**
 * Reviews their subject has asked staff to look at.
 *
 * Each row is words one real person wrote about another, sitting on a public profile for
 * as long as it waits here — and for a caregiver that profile is how they get work. The
 * review and the complaint are shown together because the decision is whether these
 * particular words should stand, and neither reads fairly without the other.
 *
 * Letting it stand is the plain button, because that is the expected answer: a one-star
 * the subject disagrees with is not abuse, and a queue that treats every complaint as a
 * takedown request would quietly make reviews unpublishable by whoever complains loudest.
 */
export function ReportedReviewQueue({ reviews }: { reviews: ReportedReview[] }) {
  if (reviews.length === 0) {
    return (
      <EmptyState
        icon={<StarIcon />}
        title="No reports waiting"
        description="When someone asks us to look at a review written about them, it appears here. Until it is settled the review stays on their profile."
      />
    );
  }

  return (
    <ul className="flex flex-col gap-4">
      {reviews.map((review) => (
        <li key={review.reviewId} className="surface-card p-5">
          <ReportedReviewRow review={review} />
        </li>
      ))}
    </ul>
  );
}

function ReportedReviewRow({ review }: { review: ReportedReview }) {
  /*
   * The decision is held here rather than submitted straight from each button, because
   * hiding needs a reason and letting it stand does not — the form has to know which one
   * is being asked for before it can insist on anything.
   */
  const [decision, setDecision] = useState<"hide" | "stand" | null>(null);

  return (
    <>
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <p className="font-control text-sm font-semibold text-text-primary">
            About {review.subjectName}
            <span className="font-normal text-text-secondary">
              {" "}
              ({review.subjectIsCaregiver ? "caregiver" : "family"})
            </span>
          </p>
          <p className="mt-0.5 text-sm text-text-secondary">
            Written by {review.authorName}
          </p>
        </div>

        <p className="text-xs text-text-secondary">
          Reported {waitedFor(review.reportedAtUtc)}
        </p>
      </div>

      <div className="mt-4 rounded-control bg-surface-muted px-4 py-3">
        <p className="font-control text-sm font-semibold text-text-primary">
          {review.rating} out of 5
        </p>
        {review.comment ? (
          <p className="mt-1 text-sm text-text-primary">{review.comment}</p>
        ) : (
          <p className="mt-1 text-sm text-text-secondary">No comment was written.</p>
        )}
      </div>

      <div className="mt-3">
        <p className="font-control text-sm font-medium text-text-primary">
          What {review.subjectName} says
        </p>
        <blockquote className="mt-1 border-l-2 border-border-strong pl-4 text-sm text-text-primary">
          {review.reportReason ?? "No reason was given."}
        </blockquote>
      </div>

      {decision === null ? (
        <div className="mt-4 flex flex-wrap gap-2 border-t border-border pt-4">
          <Button type="button" size="sm" onClick={() => setDecision("stand")}>
            Let it stand
          </Button>
          <Button
            type="button"
            variant="subtle"
            size="sm"
            onClick={() => setDecision("hide")}
          >
            Take it down
          </Button>
        </div>
      ) : (
        <DecisionForm
          review={review}
          hide={decision === "hide"}
          onCancel={() => setDecision(null)}
        />
      )}
    </>
  );
}

function DecisionForm({
  review,
  hide,
  onCancel,
}: {
  review: ReportedReview;
  hide: boolean;
  onCancel: () => void;
}) {
  const [state, formAction] = useActionState<AdminFormState, FormData>(
    moderateReviewAction.bind(null, review.reviewId, hide),
    {},
  );

  return (
    <form action={formAction} className="mt-4 border-t border-border pt-4">
      {hide ? (
        <>
          <label
            htmlFor={`reason-${review.reviewId}`}
            className="font-control text-sm font-medium text-text-primary"
          >
            Why is it coming down?
          </label>
          <p className="text-xs text-text-secondary">
            Recorded against your account. The review is kept as written — it is evidence
            about {review.authorName} as much as about {review.subjectName}.
          </p>
          <textarea
            id={`reason-${review.reviewId}`}
            name="reason"
            rows={2}
            required
            maxLength={2000}
            placeholder="The comment names a child."
            className={`mt-1.5 ${textareaClassName}`}
          />
        </>
      ) : (
        <p className="text-sm text-text-primary">
          The review stays on {review.subjectName}&rsquo;s profile and keeps counting
          towards their rating.
        </p>
      )}

      {state.error ? (
        <p role="alert" className="mt-2 text-sm text-danger-accent">
          {state.error}
        </p>
      ) : null}

      <div className="mt-3 flex gap-2">
        <ConfirmButton hide={hide} />
        <Button type="button" variant="ghost" size="sm" onClick={onCancel}>
          Back
        </Button>
      </div>
    </form>
  );
}

function ConfirmButton({ hide }: { hide: boolean }) {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" size="sm" disabled={pending}>
      {pending
        ? hide
          ? "Taking it down…"
          : "Settling…"
        : hide
          ? "Take it down"
          : "Confirm it stands"}
    </Button>
  );
}
