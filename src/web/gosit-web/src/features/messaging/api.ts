import "server-only";

import { apiFetch } from "@/lib/api/client";
import type { ConversationDetail, ConversationSummary } from "@/features/messaging/types";

/**
 * Reads for the messaging screens.
 *
 * Both routes are scoped to the signed-in account by the API, and a conversation the
 * caller is not part of answers 404 rather than 403 — so a thread id cannot be probed.
 */

export async function listConversations(): Promise<ConversationSummary[]> {
  return apiFetch<ConversationSummary[]>("/api/v1/conversations");
}

/**
 * Reads a thread. Opening it marks it read for the caller, server-side — which is why
 * this is a plain GET with no companion "mark read" call to forget.
 */
export async function getConversation(
  conversationId: string,
  take = 50,
): Promise<ConversationDetail> {
  return apiFetch<ConversationDetail>(
    `/api/v1/conversations/${conversationId}?take=${take}`,
  );
}
