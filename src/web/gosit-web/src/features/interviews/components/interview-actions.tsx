"use client";

import Link from "next/link";
import { useState, useTransition } from "react";
import { Button, buttonClassName } from "@/components/ui/button";
import {
  acceptInterviewAction,
  cancelInterviewAction,
  declineInterviewAction,
} from "@/features/interviews/actions";
import type { Interview } from "@/features/interviews/types";

/**
 * What a participant can do with an interview right now.
 *
 * Every button is gated by a flag the API set for this viewer. The client never works out
 * for itself whether someone may accept — that is the server's call, and it has the clock.
 */
export function InterviewActions({ interview }: { interview: Interview }) {
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  function run(action: (id: string) => Promise<{ error?: string }>) {
    startTransition(async () => {
      const result = await action(interview.id);
      setError(result.error ?? null);
    });
  }

  const anything = interview.canJoin || interview.canAccept || interview.canDecline || interview.canCancel;

  if (!anything) {
    return null;
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap gap-2">
        {interview.canJoin ? (
          <Link href={`/interviews/${interview.id}/room`} className={buttonClassName("primary", "", "sm")}>
            Join the call
          </Link>
        ) : null}

        {interview.canAccept ? (
          <Button size="sm" disabled={isPending} onClick={() => run(acceptInterviewAction)}>
            Accept
          </Button>
        ) : null}

        {interview.canDecline ? (
          <Button size="sm" variant="subtle" disabled={isPending} onClick={() => run(declineInterviewAction)}>
            Decline
          </Button>
        ) : null}

        {interview.canCancel ? (
          <Button size="sm" variant="ghost" disabled={isPending} onClick={() => run(cancelInterviewAction)}>
            Cancel
          </Button>
        ) : null}
      </div>

      {error ? (
        <p role="alert" className="text-sm text-danger-accent">
          {error}
        </p>
      ) : null}
    </div>
  );
}
