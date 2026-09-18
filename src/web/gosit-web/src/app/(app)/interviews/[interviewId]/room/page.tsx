import type { Metadata } from "next";
import { notFound, redirect } from "next/navigation";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { careRequesterNav } from "@/features/search/nav";
import { caregiverNav } from "@/features/caregivers/nav";
import { getInterview } from "@/features/interviews/api";
import { InterviewRoom } from "@/features/interviews/components/interview-room";
import { participantName, type Interview } from "@/features/interviews/types";
import { getConversation } from "@/features/messaging/api";
import { ApiError } from "@/lib/api/client";
import { getSessionRole } from "@/lib/auth/session";

export const metadata: Metadata = {
  title: "Video interview",
};

/**
 * The call itself.
 *
 * Loads the interview and the latest chat, and hands both to the room. No token is
 * fetched here: that happens when the person agrees to the recording and clicks join.
 */
export default async function InterviewRoomPage({
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

  // Too early, too late, or not accepted: the interview page explains which.
  if (!interview.canJoin) {
    redirect(`/interviews/${interview.id}`);
  }

  const [conversation, role] = await Promise.all([
    getConversation(interview.conversationId),
    getSessionRole(),
  ]);

  const isCaregiver = role === "Caregiver";

  return (
    <DashboardShell
      items={isCaregiver ? caregiverNav : careRequesterNav}
      currentPath={isCaregiver ? "/caregiver/messages" : "/messages"}
      heading={`Interview with ${participantName(interview)}`}
      description="Recorded. Only Trust & Safety staff can watch it."
    >
      <div className="relative">
        <InterviewRoom
          interview={interview}
          otherName={interview.otherParticipantFirstName}
          initialMessages={conversation.messages}
          backHref={`/interviews/${interview.id}`}
        />
      </div>
    </DashboardShell>
  );
}
