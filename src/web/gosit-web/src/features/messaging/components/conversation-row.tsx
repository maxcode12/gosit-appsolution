import Link from "next/link";
import { participantName, type ConversationSummary } from "@/features/messaging/types";

/**
 * One conversation in the inbox.
 *
 * Unread threads carry weight rather than colour alone — a bolder name, a dot with a
 * text label behind it — so the distinction survives for someone who cannot see the
 * accent colour.
 */
export function ConversationRow({
  conversation,
  href,
}: {
  conversation: ConversationSummary;
  /** Defaults to the requester route; the caregiver side passes its own. */
  href?: string;
}) {
  const name = participantName(conversation);
  const isUnread = conversation.unreadCount > 0;

  return (
    <article className="relative flex items-start gap-4 rounded-card border border-border bg-surface p-4 transition-shadow focus-within:shadow-md hover:shadow-md">
      <Avatar name={conversation.otherParticipantFirstName} isUnread={isUnread} />

      <div className="min-w-0 flex-1">
        <div className="flex items-baseline justify-between gap-3">
          <h2
            className={`truncate font-heading text-base ${
              isUnread ? "font-semibold text-text-primary" : "font-medium text-text-primary"
            }`}
          >
            <Link
              href={href ?? `/messages/${conversation.conversationId}`}
              className="after:absolute after:inset-0 after:content-[''] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
            >
              {name}
            </Link>
          </h2>

          {conversation.lastMessageAtUtc ? (
            <time
              dateTime={conversation.lastMessageAtUtc}
              className="shrink-0 text-xs text-text-secondary"
            >
              {formatRelative(conversation.lastMessageAtUtc)}
            </time>
          ) : null}
        </div>

        <p
          className={`mt-1 truncate text-sm ${
            isUnread ? "text-text-primary" : "text-text-secondary"
          }`}
        >
          {conversation.lastMessagePreview ?? "No messages yet."}
        </p>
      </div>

      {isUnread ? (
        <span className="shrink-0 rounded-pill bg-brand-primary px-2 py-0.5 text-xs font-semibold text-text-on-brand">
          {conversation.unreadCount}
          <span className="sr-only"> unread</span>
        </span>
      ) : null}
    </article>
  );
}

/**
 * An initial in a circle.
 *
 * There are no profile photos in the API yet, and a generic silhouette for everyone
 * would tell a reader less than the letter does.
 */
function Avatar({ name, isUnread }: { name: string; isUnread: boolean }) {
  return (
    <span
      aria-hidden
      className={`grid size-11 shrink-0 place-items-center rounded-full font-heading text-lg ${
        isUnread
          ? "bg-brand-primary text-text-on-brand"
          : "bg-surface-muted text-text-secondary"
      }`}
    >
      {name.slice(0, 1).toUpperCase()}
    </span>
  );
}

/**
 * "3h", "Tue", "12 Mar" — an inbox wants recency at a glance, not a full timestamp.
 * The exact instant stays in the `dateTime` attribute for anyone who needs it.
 */
function formatRelative(iso: string): string {
  const then = new Date(iso);
  const minutes = (Date.now() - then.getTime()) / 60_000;

  if (minutes < 1) return "now";
  if (minutes < 60) return `${Math.floor(minutes)}m`;
  if (minutes < 60 * 24) return `${Math.floor(minutes / 60)}h`;
  if (minutes < 60 * 24 * 7) return then.toLocaleDateString("en-CA", { weekday: "short" });

  return then.toLocaleDateString("en-CA", { day: "numeric", month: "short" });
}
