/**
 * Mirrors the API's interview contracts.
 *
 * Note what an interview carries to a participant: who, when, and what they can do. It
 * says there is a recording — every interview has one — and gives no way to reach it.
 * Recordings are for Trust & Safety staff, and only they can watch them.
 */

export type InterviewStatus =
  | "Requested"
  | "Accepted"
  | "Declined"
  | "Cancelled"
  | "Completed"
  /** Nobody answered before the time came. */
  | "Expired"
  /** Accepted, and nobody joined. */
  | "Missed";

export interface Interview {
  id: string;
  status: InterviewStatus;
  careRequesterProfileId: string;
  caregiverProfileId: string;
  /** The pair's conversation. Chat during the call is written there. */
  conversationId: string;
  otherParticipantFirstName: string;
  otherParticipantLastInitial: string;
  scheduledStartUtc: string;
  scheduledEndUtc: string;
  durationMinutes: number;
  joinOpensAtUtc: string;
  joinClosesAtUtc: string;
  /** What the API will let this participant do right now. Never inferred client-side. */
  canAccept: boolean;
  canDecline: boolean;
  canCancel: boolean;
  canJoin: boolean;
  requestedAtUtc: string;
}

/** What the browser needs to connect to the call. Fetched on join, never rendered into a page. */
export interface JoinTicket {
  interviewId: string;
  conversationId: string;
  serverUrl: string;
  token: string;
  expiresAtUtc: string;
}

export const DURATION_OPTIONS = [15, 30, 45, 60] as const;

export const STATUS_LABELS: Record<InterviewStatus, string> = {
  Requested: "Waiting for a reply",
  Accepted: "Confirmed",
  Declined: "Declined",
  Cancelled: "Cancelled",
  Completed: "Done",
  Expired: "No reply in time",
  Missed: "Nobody joined",
};

/** Whether an interview is still ahead of the viewer, for sorting and headings. */
export function isUpcoming(interview: Interview): boolean {
  return (
    (interview.status === "Requested" || interview.status === "Accepted") &&
    new Date(interview.joinClosesAtUtc).getTime() > Date.now()
  );
}

/** "Tue 12 Mar, 7:00–7:30 p.m." in the viewer's own zone. */
export function formatSlot(interview: Interview): string {
  const start = new Date(interview.scheduledStartUtc);
  const end = new Date(interview.scheduledEndUtc);

  const day = start.toLocaleDateString("en-CA", {
    weekday: "short",
    day: "numeric",
    month: "short",
  });

  const time = (date: Date) =>
    date.toLocaleTimeString("en-CA", { hour: "numeric", minute: "2-digit" });

  return `${day}, ${time(start)}–${time(end)}`;
}

export function participantName(interview: Interview): string {
  return interview.otherParticipantLastInitial
    ? `${interview.otherParticipantFirstName} ${interview.otherParticipantLastInitial}.`
    : interview.otherParticipantFirstName;
}
