import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { listInterviews } from "@/features/interviews/api";
import { InterviewList } from "@/features/interviews/components/interview-card";
import { RequestInterview } from "@/features/interviews/components/request-interview";
import { getConversation } from "@/features/messaging/api";
import { ConversationThread } from "@/features/messaging/components/conversation-thread";
import { participantName } from "@/features/messaging/types";
import { careRequesterNav } from "@/features/search/nav";
import { ApiError } from "@/lib/api/client";

/**
 * A static title. Thread titles reach browser history and tab strips, and who someone is
 * talking to is not something to leave there.
 */
export const metadata: Metadata = {
  title: "Conversation",
};

/**
 * One conversation — wireframe `139:56`.
 *
 * Opening this marks the thread read, server-side, as a side effect of the GET. That is
 * the API's design and the right one: there is no second call to forget.
 *
 * Video interviews with this caregiver sit at the top of the thread. The conversation is
 * where "can we talk first?" gets asked, so it is where the answer is offered.
 */
export default async function ConversationPage({
  params,
}: {
  params: Promise<{ conversationId: string }>;
}) {
  const { conversationId } = await params;

  let conversation;

  try {
    conversation = await getConversation(conversationId);
  } catch (error) {
    // 404 covers both "no such thread" and "not yours" — deliberately indistinguishable.
    if (error instanceof ApiError && error.status === 404) {
      notFound();
    }

    throw error;
  }

  const interviews = await listInterviews(conversation.otherParticipantProfileId);

  return (
    <DashboardShell
      items={careRequesterNav}
      currentPath="/messages"
      heading={participantName(conversation)}
      description="Private conversation"
    >
      <ConversationThread
        conversation={conversation}
        backHref="/messages"
        interviews={
          <>
            <InterviewList interviews={interviews} />
            <RequestInterview
              caregiverProfileId={conversation.otherParticipantProfileId}
              caregiverName={conversation.otherParticipantFirstName}
              // A conversation exists, so the profile it needed does too.
              hasProfile
            />
          </>
        }
      />
    </DashboardShell>
  );
}
