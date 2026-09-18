/**
 * Mirrors the API's messaging contracts.
 *
 * Note what a participant is here: a first name and a last initial. The API deliberately
 * does not disclose email addresses or phone numbers through messaging — the thread is
 * the channel — so there is nothing else to render and nothing to accidentally leak.
 */

export interface ConversationSummary {
  conversationId: string;
  otherParticipantProfileId: string;
  otherParticipantFirstName: string;
  otherParticipantLastInitial: string;
  /** A short excerpt of the newest message, or null on an empty thread. */
  lastMessagePreview: string | null;
  lastMessageAtUtc: string | null;
  unreadCount: number;
}

export interface Message {
  id: string;
  senderProfileId: string;
  /** The API decides authorship; the client never infers it from an id comparison. */
  sentByMe: boolean;
  body: string;
  sentAtUtc: string;
}

export interface ConversationDetail {
  conversationId: string;
  otherParticipantProfileId: string;
  otherParticipantFirstName: string;
  otherParticipantLastInitial: string;
  messages: Message[];
  /** True when older messages exist beyond the page that was fetched. */
  hasMore: boolean;
}

/** Formats a participant as "Priya R." — all the API gives, and all a thread needs. */
export function participantName(participant: {
  otherParticipantFirstName: string;
  otherParticipantLastInitial: string;
}): string {
  return participant.otherParticipantLastInitial
    ? `${participant.otherParticipantFirstName} ${participant.otherParticipantLastInitial}.`
    : participant.otherParticipantFirstName;
}
