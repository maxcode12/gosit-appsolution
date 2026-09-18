import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { careRequesterNav } from "@/features/search/nav";
import { caregiverNav } from "@/features/caregivers/nav";
import { getInterview } from "@/features/interviews/api";
import { InterviewActions } from "@/features/interviews/components/interview-actions";
import {
  STATUS_LABELS,
  formatSlot,
  participantName,
  type Interview,
} from "@/features/interviews/types";
import { ApiError } from "@/lib/api/client";
import { getSessionRole } from "@/lib/auth/session";

/** Static, like conversation titles: who someone is meeting does not belong in a tab strip. */
export const metadata: Metadata = {
  title: "Video interview",
};

/**
 * One interview, for whichever side is looking.
 *
 * Outside both route groups on purpose: the email about an interview links here, and
 * the same link has to work for the family and the caregiver. The rail is chosen from the
 * session's role; what the page can do is chosen by the API.
 */
export default async function InterviewPage({
  params,
}: {
  params: Promise<{ interviewId: string }>;
}) {
  const { interviewId } = await params;

  let interview: Interview;

  try {
    interview = await getInterview(interviewId);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      notFound();
    }

    throw error;
  }

  const isCaregiver = (await getSessionRole()) === "Caregiver";
  const messagesHref = isCaregiver
    ? `/caregiver/messages/${interview.conversationId}`
    : `/messages/${interview.conversationId}`;

  return (
    <DashboardShell
      items={isCaregiver ? caregiverNav : careRequesterNav}
      currentPath={isCaregiver ? "/caregiver/messages" : "/messages"}
      heading={`Video interview with ${participantName(interview)}`}
      description={formatSlot(interview)}
    >
      <div className="flex max-w-2xl flex-col gap-6">
        <section className="surface-card p-6">
          <dl className="grid gap-4 sm:grid-cols-3">
            <div>
              <dt className="text-sm text-text-secondary">Status</dt>
              <dd className="mt-0.5 font-heading text-lg font-medium text-text-primary">
                {STATUS_LABELS[interview.status]}
              </dd>
            </div>
            <div>
              <dt className="text-sm text-text-secondary">Length</dt>
              <dd className="mt-0.5 font-heading text-lg font-medium text-text-primary">
                {interview.durationMinutes} minutes
              </dd>
            </div>
            <div>
              <dt className="text-sm text-text-secondary">Doors open</dt>
              <dd className="mt-0.5 font-heading text-lg font-medium text-text-primary">
                {new Date(interview.joinOpensAtUtc).toLocaleTimeString("en-CA", {
                  hour: "numeric",
                  minute: "2-digit",
                })}
              </dd>
            </div>
          </dl>

          <div className="mt-6">
            <InterviewActions interview={interview} />
          </div>

          {interview.status === "Requested" && !interview.canAccept ? (
            <p className="mt-4 text-sm text-text-secondary">
              Waiting for {interview.otherParticipantFirstName} to accept. You will be told
              when they reply.
            </p>
          ) : null}

          {interview.status === "Accepted" && !interview.canJoin ? (
            <p className="mt-4 text-sm text-text-secondary">
              You can join from 10 minutes before the start. Come back then.
            </p>
          ) : null}
        </section>

        <section className="surface-card p-6 text-sm text-text-secondary">
          <h2 className="font-heading text-base font-semibold text-text-primary">
            How interviews work
          </h2>
          <ul className="mt-3 list-disc space-y-1.5 pl-5">
            <li>The call happens here, on GoSit. Nothing is arranged elsewhere.</li>
            <li>
              It is recorded, and both of you agree to that when you join. Only GoSit&apos;s
              Trust &amp; Safety staff can watch a recording; every view is logged; it is
              deleted after 90 days.
            </li>
            <li>You can mute, turn your camera off, and leave at any time.</li>
            <li>Anything typed in the chat is saved to your conversation.</li>
          </ul>

          <p className="mt-4">
            <Link href={messagesHref} className="font-control font-semibold text-brand-primary hover:underline">
              Open your conversation with {interview.otherParticipantFirstName} →
            </Link>
          </p>
        </section>
      </div>
    </DashboardShell>
  );
}
