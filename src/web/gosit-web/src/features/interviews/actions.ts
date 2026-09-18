"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { ApiError, SessionExpiredError, apiFetch } from "@/lib/api/client";
import type { Interview, JoinTicket } from "@/features/interviews/types";
import type { ConversationDetail, Message } from "@/features/messaging/types";

/**
 * Interview mutations.
 *
 * Times travel as FormData and are converted to UTC here, as bookings are, so the
 * browser's zone stays out of the API contract. Chat bodies travel as FormData too, for
 * the reason messaging gives: Next's development server echoes scalar Server Action
 * arguments to the console.
 */

export interface InterviewFormState {
  error?: string;
  done?: boolean;
}

function toState(error: unknown, fallback: string): InterviewFormState {
  if (error instanceof SessionExpiredError) {
    return { error: "Your session has expired. Log in again to continue." };
  }

  if (error instanceof ApiError) {
    return { error: error.problem?.title ?? fallback };
  }

  throw error;
}

function revalidateInterviewPaths(interviewId?: string): void {
  revalidatePath("/messages");
  revalidatePath("/caregiver/messages");

  if (interviewId) {
    revalidatePath(`/interviews/${interviewId}`);
  }
}

export async function requestInterviewAction(
  caregiverProfileId: string,
  _previous: InterviewFormState,
  formData: FormData,
): Promise<InterviewFormState> {
  const date = String(formData.get("date") ?? "");
  const time = String(formData.get("time") ?? "");
  const durationMinutes = Number(formData.get("durationMinutes") ?? 30);

  if (!date || !time) {
    return { error: "Choose a date and time." };
  }

  const start = new Date(`${date}T${time}`);

  if (Number.isNaN(start.getTime())) {
    return { error: "That date and time could not be read." };
  }

  let interview: Interview;

  try {
    interview = await apiFetch<Interview>("/api/v1/interviews", {
      method: "POST",
      body: { caregiverProfileId, scheduledStartUtc: start.toISOString(), durationMinutes },
    });
  } catch (error) {
    return toState(error, "We could not request this interview.");
  }

  revalidateInterviewPaths(interview.id);
  redirect(`/interviews/${interview.id}`);
}

async function transition(
  interviewId: string,
  verb: "accept" | "decline" | "cancel",
  fallback: string,
): Promise<InterviewFormState> {
  try {
    await apiFetch<Interview>(`/api/v1/interviews/${interviewId}/${verb}`, { method: "POST" });
  } catch (error) {
    return toState(error, fallback);
  }

  revalidateInterviewPaths(interviewId);

  return { done: true };
}

export async function acceptInterviewAction(interviewId: string): Promise<InterviewFormState> {
  return transition(interviewId, "accept", "We could not accept this interview.");
}

export async function declineInterviewAction(interviewId: string): Promise<InterviewFormState> {
  return transition(interviewId, "decline", "We could not decline this interview.");
}

export async function cancelInterviewAction(interviewId: string): Promise<InterviewFormState> {
  return transition(interviewId, "cancel", "We could not cancel this interview.");
}

export interface JoinResult {
  error?: string;
  ticket?: JoinTicket;
}

/**
 * Asks the API for a way into the call.
 *
 * Consent travels with the request, and the API refuses without it. The ticket comes back
 * to the browser only from here — it is never rendered into a page — and it is good for
 * this interview's room and nothing else.
 */
export async function joinInterviewAction(
  interviewId: string,
  consentToRecording: boolean,
): Promise<JoinResult> {
  try {
    const ticket = await apiFetch<JoinTicket>(`/api/v1/interviews/${interviewId}/join`, {
      method: "POST",
      body: { consentToRecording },
    });

    return { ticket };
  } catch (error) {
    return toState(error, "We could not start the interview.");
  }
}

export interface ChatState {
  error?: string;
  messages?: Message[];
}

/** The latest page of the pair's conversation, for the chat panel during a call. */
export async function loadInterviewChatAction(conversationId: string): Promise<ChatState> {
  try {
    const conversation = await apiFetch<ConversationDetail>(
      `/api/v1/conversations/${conversationId}?take=50`,
    );

    return { messages: conversation.messages };
  } catch (error) {
    return toState(error, "We could not load the chat.");
  }
}

/**
 * Sends a chat message during a call. It goes into the pair's conversation like any other
 * message — same record, same contact-detail flagging, same moderation — and is there
 * after the call ends.
 */
export async function sendInterviewChatAction(
  conversationId: string,
  formData: FormData,
): Promise<ChatState> {
  const body = String(formData.get("body") ?? "").trim();

  if (!body) {
    return { error: "Write a message first." };
  }

  try {
    await apiFetch<Message>(`/api/v1/conversations/${conversationId}/messages`, {
      method: "POST",
      body: { body },
    });
  } catch (error) {
    return toState(error, "We could not send this message.");
  }

  return loadInterviewChatAction(conversationId);
}
