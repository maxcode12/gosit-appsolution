"use client";

import { useActionState, useState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { textareaClassName } from "@/components/ui/text-input";
import { reportReviewAction, type ReportReviewState } from "@/features/reviews/actions";

/**
 * Asking staff to look at a review written about you.
 *
 * Deliberately understated: a small text link, closed by default, below the review. A
 * prominent "report" button beside every review would invite reporting any rating the
 * subject dislikes, and a queue full of "three stars is unfair" is a queue where the
 * review naming a child waits behind forty others.
 *
 * The copy says plainly that a review is not removed for being unfavourable, so someone
 * about to spend ten minutes writing that objection can stop before they start.
 */
export function ReportReview({ reviewId }: { reviewId: string }) {
  const [isOpen, setIsOpen] = useState(false);

  const [state, formAction] = useActionState<ReportReviewState, FormData>(
    reportReviewAction.bind(null, reviewId),
    {},
  );

  if (state.reported) {
    return (
      <p className="mt-3 text-sm text-text-secondary">
        Reported. Someone will look at it — the review stays up until they do.
      </p>
    );
  }

  if (!isOpen) {
    return (
      <button
        type="button"
        onClick={() => setIsOpen(true)}
        className="mt-3 font-control text-sm text-text-secondary underline-offset-4 hover:text-text-primary hover:underline"
      >
        Report this review
      </button>
    );
  }

  return (
    <form action={formAction} className="mt-3 border-t border-border pt-3">
      <label
        htmlFor={`report-${reviewId}`}
        className="font-control text-sm font-medium text-text-primary"
      >
        What is wrong with it?
      </label>
      <p className="text-xs text-text-secondary">
        A review is not removed for being unfavourable. Tell us if it is abusive, names a
        child, gives out an address, or is about a booking that never happened.
      </p>

      <textarea
        id={`report-${reviewId}`}
        name="reason"
        rows={3}
        required
        maxLength={2000}
        className={`mt-1.5 ${textareaClassName}`}
      />

      {state.error ? (
        <p role="alert" className="mt-2 text-sm text-danger-accent">
          {state.error}
        </p>
      ) : null}

      <div className="mt-3 flex gap-2">
        <SendButton />
        <Button type="button" variant="ghost" size="sm" onClick={() => setIsOpen(false)}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

function SendButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" size="sm" disabled={pending}>
      {pending ? "Sending…" : "Send report"}
    </Button>
  );
}
