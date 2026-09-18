"use client";

import { useActionState, useState, useTransition } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { VideoIcon } from "@/components/ui/icons";
import { textareaClassName } from "@/components/ui/text-input";
import {
  holdInterviewRecordingsAction,
  releaseInterviewRecordingsAction,
  type AdminFormState,
} from "@/features/admin/actions";
import type { AdminInterview, AdminInterviewRecording } from "@/features/admin/types";

/**
 * Recorded interviews, for Trust & Safety.
 *
 * Built so that using it does not mean watching people. Rows show who met whom and when;
 * a recording plays only when someone chooses to open it, and the API records who opened
 * what. The screen says so, in the row, before anyone clicks.
 */
export function InterviewRecordings({ interviews }: { interviews: AdminInterview[] }) {
  if (interviews.length === 0) {
    return (
      <EmptyState
        icon={<VideoIcon />}
        title="No recorded interviews"
        description="Every video interview between a family and a caregiver is recorded and listed here for 90 days. Watching one is logged against your account."
      />
    );
  }

  return (
    <ul className="flex flex-col gap-4">
      {interviews.map((interview) => (
        <li key={interview.interviewId} className="surface-card p-5">
          <InterviewRow interview={interview} />
        </li>
      ))}
    </ul>
  );
}

function InterviewRow({ interview }: { interview: AdminInterview }) {
  const [holdState, holdAction] = useActionState<AdminFormState, FormData>(
    holdInterviewRecordingsAction.bind(null, interview.interviewId),
    {},
  );
  const [releaseError, setReleaseError] = useState<string | null>(null);
  const [isReleasing, startReleasing] = useTransition();
  const [showHoldForm, setShowHoldForm] = useState(false);

  const when = new Date(interview.scheduledStartUtc).toLocaleString("en-CA", {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });

  function release() {
    startReleasing(async () => {
      const result = await releaseInterviewRecordingsAction(interview.interviewId);
      setReleaseError(result.error ?? null);
    });
  }

  return (
    <>
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <p className="font-heading font-semibold text-text-primary">
            {interview.careRequesterName}{" "}
            <span className="font-normal text-text-secondary">(family)</span> and{" "}
            {interview.caregiverName}{" "}
            <span className="font-normal text-text-secondary">(caregiver)</span>
          </p>
          <p className="mt-0.5 text-sm text-text-secondary">
            {when} · {interview.durationMinutes} min · {interview.status}
          </p>
        </div>

        {interview.recordingsOnHold ? (
          <span className="rounded-pill bg-accent-soft px-2.5 py-1 font-control text-xs font-semibold text-text-primary">
            On hold
          </span>
        ) : null}
      </div>

      {interview.recordingsOnHold && interview.recordingHoldReason ? (
        <p className="mt-3 rounded-control bg-surface-muted px-4 py-3 text-sm text-text-primary">
          <span className="font-medium">Kept because:</span> {interview.recordingHoldReason}
        </p>
      ) : null}

      <ul className="mt-4 flex flex-col gap-3">
        {interview.recordings.map((recording) => (
          <li key={recording.recordingId}>
            <RecordingRow recording={recording} />
          </li>
        ))}
      </ul>

      <div className="mt-4 border-t border-border pt-4">
        {interview.recordingsOnHold ? (
          <>
            <Button type="button" variant="subtle" size="sm" onClick={release} disabled={isReleasing}>
              {isReleasing ? "Releasing…" : "Release the hold"}
            </Button>
            {releaseError ? (
              <p role="alert" className="mt-2 text-sm text-danger-accent">
                {releaseError}
              </p>
            ) : null}
          </>
        ) : showHoldForm ? (
          <form action={holdAction}>
            <label
              htmlFor={`reason-${interview.interviewId}`}
              className="font-control text-sm font-medium text-text-primary"
            >
              Why these recordings need to be kept
            </label>
            <textarea
              id={`reason-${interview.interviewId}`}
              name="reason"
              rows={2}
              maxLength={500}
              required
              placeholder="Dispute on booking of 14 March; family says the caregiver misrepresented their experience."
              className={`mt-1.5 ${textareaClassName}`}
            />

            {holdState.error ? (
              <p role="alert" className="mt-2 text-sm text-danger-accent">
                {holdState.error}
              </p>
            ) : null}

            <div className="mt-3 flex gap-2">
              <HoldButton />
              <Button type="button" variant="ghost" size="sm" onClick={() => setShowHoldForm(false)}>
                Cancel
              </Button>
            </div>
          </form>
        ) : (
          <Button type="button" variant="subtle" size="sm" onClick={() => setShowHoldForm(true)}>
            Keep past 90 days
          </Button>
        )}
      </div>
    </>
  );
}

function RecordingRow({ recording }: { recording: AdminInterviewRecording }) {
  const [isOpen, setIsOpen] = useState(false);

  const started = new Date(recording.startedAtUtc).toLocaleTimeString("en-CA", {
    hour: "numeric",
    minute: "2-digit",
  });

  const label: Record<AdminInterviewRecording["status"], string> = {
    Recording: "Still recording",
    Available: "Saved",
    Failed: "Recording failed",
    Deleted: "Deleted",
  };

  return (
    <div className="rounded-control bg-surface-muted px-4 py-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-text-primary">
          <span className="font-medium">{label[recording.status]}</span>
          <span className="text-text-secondary"> · started {started}</span>
          {recording.deleteAfterUtc && recording.status !== "Deleted" ? (
            <span className="text-text-secondary">
              {" "}
              · deleted after{" "}
              {new Date(recording.deleteAfterUtc).toLocaleDateString("en-CA", {
                day: "numeric",
                month: "short",
              })}
            </span>
          ) : null}
        </p>

        {recording.isWatchable && !isOpen ? (
          <Button type="button" variant="subtle" size="sm" onClick={() => setIsOpen(true)}>
            Watch
          </Button>
        ) : null}
      </div>

      {recording.isWatchable && !isOpen ? (
        <p className="mt-1 text-xs text-text-secondary">
          Watching is recorded against your account.
        </p>
      ) : null}

      {isOpen ? (
        // A plain video element over the streaming route. Seeking within a long recording
        // needs range requests, which the route does not yet support; it plays from the start.
        <video
          controls
          preload="metadata"
          src={`/recordings/interviews/${recording.recordingId}`}
          className="mt-3 w-full rounded-control bg-black"
        >
          Your browser cannot play this recording.
        </video>
      ) : null}
    </div>
  );
}

function HoldButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" size="sm" disabled={pending}>
      {pending ? "Saving…" : "Put on hold"}
    </Button>
  );
}
