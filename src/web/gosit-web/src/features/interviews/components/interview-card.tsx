import Link from "next/link";
import { InterviewActions } from "@/features/interviews/components/interview-actions";
import {
  STATUS_LABELS,
  formatSlot,
  isUpcoming,
  participantName,
  type Interview,
} from "@/features/interviews/types";

/** One interview: when, with whom, where it stands, and what can be done about it. */
export function InterviewCard({
  interview,
  showName = true,
}: {
  interview: Interview;
  /** Off when the surrounding page already says who the other person is. */
  showName?: boolean;
}) {
  const upcoming = isUpcoming(interview);

  return (
    <article className="surface-card flex flex-col gap-3 p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="font-heading text-base font-medium text-text-primary">
            <Link href={`/interviews/${interview.id}`} className="hover:underline">
              {showName ? `Video interview with ${participantName(interview)}` : "Video interview"}
            </Link>
          </h3>
          <p className="mt-0.5 text-sm text-text-secondary">
            {formatSlot(interview)} · {interview.durationMinutes} min
          </p>
        </div>

        <span
          className={`shrink-0 rounded-pill px-2.5 py-1 font-control text-xs font-semibold ${
            interview.status === "Accepted"
              ? "bg-success-soft text-success-accent"
              : upcoming
                ? "bg-accent-soft text-text-primary"
                : "bg-surface-muted text-text-secondary"
          }`}
        >
          {STATUS_LABELS[interview.status]}
        </span>
      </div>

      <InterviewActions interview={interview} />
    </article>
  );
}

/**
 * The interviews on a conversation, upcoming first.
 *
 * Past ones are kept short. They are context — "we spoke last month" — not a queue.
 */
export function InterviewList({ interviews }: { interviews: Interview[] }) {
  const upcoming = interviews.filter(isUpcoming);
  const past = interviews.filter((interview) => !isUpcoming(interview)).slice(0, 3);

  if (interviews.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-col gap-3">
      {upcoming.map((interview) => (
        <InterviewCard key={interview.id} interview={interview} showName={false} />
      ))}

      {past.length > 0 ? (
        <details className="text-sm">
          <summary className="cursor-pointer font-control text-text-secondary">
            Earlier interviews ({past.length})
          </summary>
          <ul className="mt-2 flex flex-col gap-1 text-text-secondary">
            {past.map((interview) => (
              <li key={interview.id}>
                <Link href={`/interviews/${interview.id}`} className="hover:underline">
                  {formatSlot(interview)}
                </Link>{" "}
                — {STATUS_LABELS[interview.status]}
              </li>
            ))}
          </ul>
        </details>
      ) : null}
    </div>
  );
}
