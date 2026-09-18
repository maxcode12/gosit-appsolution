import type { Metadata } from "next";
import Link from "next/link";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { buttonClassName } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { listConversations } from "@/features/messaging/api";
import { ConversationRow } from "@/features/messaging/components/conversation-row";
import { careRequesterNav } from "@/features/search/nav";

export const metadata: Metadata = {
  title: "Messages",
  description: "Your conversations with caregivers.",
};

/**
 * The inbox — wireframes `127:2` (empty) and `136:30` (populated).
 *
 * A single column rather than a two-pane split: threads here are short and infrequent,
 * and a list that becomes a detail view is the pattern people already know from every
 * phone. Nothing is gained by keeping the list on screen beside the conversation.
 */
export default async function MessagesPage() {
  const conversations = await listConversations();

  const unread = conversations.reduce((total, c) => total + c.unreadCount, 0);

  return (
    <DashboardShell
      items={careRequesterNav}
      currentPath="/messages"
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
          description="Found someone who looks right? Ask them anything before you book — how they handle bedtime, whether they drive, what a first visit looks like."
          action={
            <Link href="/dashboard" className={buttonClassName()}>
              Find care
            </Link>
          }
        />
      ) : (
        <ul className="flex max-w-3xl flex-col gap-3">
          {conversations.map((conversation) => (
            <li key={conversation.conversationId}>
              <ConversationRow conversation={conversation} />
            </li>
          ))}
        </ul>
      )}
    </DashboardShell>
  );
}
