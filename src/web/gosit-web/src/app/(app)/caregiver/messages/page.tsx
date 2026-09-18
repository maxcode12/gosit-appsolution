import type { Metadata } from "next";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { EmptyState } from "@/components/ui/empty-state";
import { listConversations } from "@/features/messaging/api";
import { ConversationRow } from "@/features/messaging/components/conversation-row";
import { caregiverNav } from "@/features/caregivers/nav";

export const metadata: Metadata = {
  title: "Messages",
  description: "Your conversations with families.",
};

/**
 * The caregiver inbox — wireframe `140:103`.
 *
 * The same list and rows as the requester's, under the caregiver rail. The API is
 * symmetric — one conversations endpoint serving both sides — so duplicating the
 * component would only create two things to keep in step.
 *
 * The empty state is the one real difference. A caregiver cannot start a conversation;
 * `POST /conversations` is requester-only, so telling them to "message someone" would be
 * advice they cannot act on.
 */
export default async function CaregiverMessagesPage() {
  const conversations = await listConversations();

  const unread = conversations.reduce((total, c) => total + c.unreadCount, 0);

  return (
    <DashboardShell
      items={caregiverNav}
      currentPath="/caregiver/messages"
      heading="Messages"
      description={
        unread > 0
          ? `${unread} unread ${unread === 1 ? "message" : "messages"}`
          : undefined
      }
    >
      {conversations.length === 0 ? (
        <EmptyState
          title="No messages yet"
          description="Families message you here before booking, usually to ask about your availability or experience. Replying quickly is the single best thing you can do to get booked."
        />
      ) : (
        <ul className="flex max-w-3xl flex-col gap-3">
          {conversations.map((conversation) => (
            <li key={conversation.conversationId}>
              <ConversationRow
                conversation={conversation}
                href={`/caregiver/messages/${conversation.conversationId}`}
              />
            </li>
          ))}
        </ul>
      )}
    </DashboardShell>
  );
}
