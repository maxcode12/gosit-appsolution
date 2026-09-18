import Link from "next/link";
import type { ReactNode } from "react";
import { Composer } from "@/features/messaging/components/composer";
import { participantName, type ConversationDetail, type Message } from "@/features/messaging/types";

/**
 * A thread and its composer.
 *
 * Shared by both sides of the marketplace: the API's conversation endpoints are
 * symmetric, so the only thing that differs is where "back" goes. Two copies of this
 * would be two things to keep in step for no gain.
 */
export function ConversationThread({
  conversation,
  backHref,
  interviews,
}: {
  conversation: ConversationDetail;
  backHref: string;
  /** The pair's video interviews, and for a family the way to ask for one. */
  interviews?: ReactNode;
}) {
  const name = participantName(conversation);

  return (
    <div className="flex max-w-3xl flex-col overflow-hidden rounded-card border border-border bg-surface-muted">
      <div className="border-b border-border bg-surface px-4 py-3">
        <Link
          href={backHref}
          className="font-control text-sm text-text-secondary hover:text-text-primary"
        >
          ← All messages
        </Link>
      </div>

      {interviews ? (
        <div className="flex flex-col gap-3 border-b border-border bg-surface p-4">{interviews}</div>
      ) : null}

      <div className="flex min-h-100 flex-col gap-3 p-4">
        {conversation.hasMore ? (
          <p className="text-center text-xs text-text-secondary">
            Older messages are not shown.
          </p>
        ) : null}

        {conversation.messages.length === 0 ? (
          <p className="my-auto text-center text-sm text-text-secondary">
            No messages yet. Say hello.
          </p>
        ) : (
          <ol className="flex flex-col gap-3">
            {conversation.messages.map((message, index) => (
              <MessageBubble
                key={message.id}
                message={message}
                senderName={name}
                // A name only where authorship changes; repeating it on every bubble in
                // a run is noise.
                showSender={conversation.messages[index - 1]?.sentByMe !== message.sentByMe}
              />
            ))}
          </ol>
        )}
      </div>

      <Composer conversationId={conversation.conversationId} recipientName={name} />
    </div>
  );
}

function MessageBubble({
  message,
  senderName,
  showSender,
}: {
  message: Message;
  senderName: string;
  showSender: boolean;
}) {
  // Authorship comes from the API's own `sentByMe`, never from comparing ids client-side.
  const isMine = message.sentByMe;

  return (
    <li className={`flex flex-col ${isMine ? "items-end" : "items-start"}`}>
      {showSender ? (
        <span className="mb-1 px-1 text-xs text-text-secondary">
          {isMine ? "You" : senderName}
        </span>
      ) : null}

      <div
        className={`max-w-[85%] whitespace-pre-wrap rounded-card px-4 py-2.5 text-sm ${
          isMine
            ? "bg-brand-primary text-text-on-brand"
            : "border border-border bg-surface text-text-primary"
        }`}
      >
        {message.body}
      </div>

      <time dateTime={message.sentAtUtc} className="mt-1 px-1 text-xs text-text-secondary">
        {new Date(message.sentAtUtc).toLocaleString("en-CA", {
          day: "numeric",
          month: "short",
          hour: "numeric",
          minute: "2-digit",
        })}
      </time>
    </li>
  );
}
