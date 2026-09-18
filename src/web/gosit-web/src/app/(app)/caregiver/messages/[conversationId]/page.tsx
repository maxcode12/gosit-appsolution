import type { Metadata } from "next";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { listInterviews } from "@/features/interviews/api";
import { InterviewList } from "@/features/interviews/components/interview-card";
import { getConversation } from "@/features/messaging/api";
import { ConversationThread } from "@/features/messaging/components/conversation-thread";
import { participantName } from "@/features/messaging/types";
import { caregiverNav } from "@/features/caregivers/nav";
import { ApiError } from "@/lib/api/client";
import { notFound } from "next/navigation";

export const metadata: Metadata = {
  title: "Conversation",
};

/**
 * The caregiver's view of a thread. Same thread component, caregiver rail and routes.
 *
 * Interview requests from this family appear at the top, with accept and decline. A
 * caregiver cannot ask for one — like messaging, the family goes first.
 */
export default async function CaregiverConversationPage({
  params,
}: {
  params: Promise<{ conversationId: string }>;
}) {
  const { conversationId } = await params;

  let conversation;

  try {
    conversation = await getConversation(conversationId);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      notFound();
    }

    throw error;
  }

  const interviews = await listInterviews(conversation.otherParticipantProfileId);

  return (
    <DashboardShell
      items={caregiverNav}
      currentPath="/caregiver/messages"
      heading={participantName(conversation)}
      description="Private conversation"
    >
      <ConversationThread
        conversation={conversation}
        backHref="/caregiver/messages"
        interviews={interviews.length > 0 ? <InterviewList interviews={interviews} /> : undefined}
      />
    </DashboardShell>
  );
}
