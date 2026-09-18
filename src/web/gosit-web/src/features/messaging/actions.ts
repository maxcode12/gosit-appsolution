"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { ApiError, SessionExpiredError, apiFetch } from "@/lib/api/client";
import type { Message } from "@/features/messaging/types";

/**
 * Messaging mutations.
 *
 * Message bodies travel as FormData, never as scalar Server Action arguments: Next's
 * development server echoes scalar arguments to the console, and a private message
 * between two people is not console material.
 */

export interface MessageFormState {
  error?: string;
  /** Set when a reply was accepted, so the composer can clear itself. */
  sentAt?: number;
}

function toState(error: unknown, fallback: string): MessageFormState {
  if (error instanceof SessionExpiredError) {
    return { error: "Your session has expired. Log in again to continue." };
  }

  if (error instanceof ApiError) {
    // The API's own message is written to be shown — it is what explains a body rejected
    // for containing contact details.
    return { error: error.problem?.title ?? fallback };
  }

  throw error;
}

/**
 * Starts a conversation with a caregiver, or adds to the existing one.
 *
 * Care requesters only, matching the API: a caregiver replies rather than initiating.
 * The API returns the message, not the conversation, so the inbox is revalidated and the
 * caller is sent to the thread list rather than guessing an id.
 */
export async function startConversationAction(
  caregiverProfileId: string,
  _previous: MessageFormState,
  formData: FormData,
): Promise<MessageFormState> {
  const body = String(formData.get("body") ?? "").trim();

  if (!body) {
    return { error: "Write a message first." };
  }

  try {
    await apiFetch<Message>("/api/v1/conversations", {
      method: "POST",
      body: { caregiverProfileId, body },
    });
  } catch (error) {
    return toState(error, "We could not send this message.");
  }

  revalidatePath("/messages");
  redirect("/messages");
}

export async function replyAction(
  conversationId: string,
  _previous: MessageFormState,
  formData: FormData,
): Promise<MessageFormState> {
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

  revalidatePath("/messages");
  revalidatePath(`/messages/${conversationId}`);

  // A changing stamp rather than a boolean: two consecutive sends must both register,
  // and `sent: true` twice in a row is indistinguishable from one send.
  return { sentAt: Date.now() };
}
