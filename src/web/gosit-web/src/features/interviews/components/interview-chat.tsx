"use client";

import { useCallback, useEffect, useRef, useState, useTransition } from "react";
import { useDataChannel } from "@livekit/components-react";
import { Button } from "@/components/ui/button";
import { loadInterviewChatAction, sendInterviewChatAction } from "@/features/interviews/actions";
import type { Message } from "@/features/messaging/types";

/**
 * Chat during the call.
 *
 * Messages are GoSit messages: sent to and read from the pair's conversation, so they
 * carry the same contact-detail flagging and moderation as anything typed in the inbox,
 * and they are still there after the call. The video room's data channel carries only a
 * nudge — "something new, reload" — never the text itself, so nothing said in the chat
 * exists only in the video service.
 */
export function InterviewChat({
  conversationId,
  initialMessages,
  otherName,
}: {
  conversationId: string;
  initialMessages: Message[];
  otherName: string;
}) {
  const [messages, setMessages] = useState<Message[]>(initialMessages);
  const [error, setError] = useState<string | null>(null);
  const [isSending, startSending] = useTransition();
  const formRef = useRef<HTMLFormElement>(null);
  const listRef = useRef<HTMLOListElement>(null);

  const reload = useCallback(async () => {
    const result = await loadInterviewChatAction(conversationId);

    if (result.messages) {
      setMessages(result.messages);
    }
  }, [conversationId]);

  const { send } = useDataChannel("chat", () => {
    void reload();
  });

  // Newest message in view. Chat in a call is read as it arrives, not scrolled to.
  useEffect(() => {
    listRef.current?.scrollTo({ top: listRef.current.scrollHeight });
  }, [messages]);

  function submit(formData: FormData) {
    startSending(async () => {
      const result = await sendInterviewChatAction(conversationId, formData);

      if (result.error) {
        setError(result.error);
        return;
      }

      setError(null);
      formRef.current?.reset();

      if (result.messages) {
        setMessages(result.messages);
      }

      // A nudge, not the message. Best effort: the other side also sees it on their next
      // send or page load.
      try {
        await send(new TextEncoder().encode("1"), { reliable: true });
      } catch {
        // The data channel is a convenience; the message is already saved.
      }
    });
  }

  return (
    <aside className="flex max-h-[28rem] flex-col border-t border-border/20 bg-surface text-text-primary lg:absolute lg:inset-y-0 lg:right-0 lg:w-[22rem] lg:max-h-none lg:border-l lg:border-t-0 lg:border-border">
      <h2 className="border-b border-border px-4 py-3 font-heading text-sm font-semibold">
        Chat with {otherName}
      </h2>

      <ol ref={listRef} className="flex flex-1 flex-col gap-2 overflow-y-auto p-4">
        {messages.length === 0 ? (
          <li className="my-auto text-center text-sm text-text-secondary">
            Nothing yet. Anything you type here stays in your conversation afterwards.
          </li>
        ) : (
          messages.map((message) => (
            <li key={message.id} className={`flex ${message.sentByMe ? "justify-end" : "justify-start"}`}>
              <span
                className={`max-w-[85%] whitespace-pre-wrap rounded-card px-3 py-2 text-sm ${
                  message.sentByMe
                    ? "bg-brand-primary text-text-on-brand"
                    : "border border-border bg-surface-muted"
                }`}
              >
                {message.body}
              </span>
            </li>
          ))
        )}
      </ol>

      <form ref={formRef} action={submit} className="border-t border-border p-3">
        {error ? (
          <p role="alert" className="mb-2 text-sm text-danger-accent">
            {error}
          </p>
        ) : null}

        <div className="flex items-end gap-2">
          <textarea
            name="body"
            rows={1}
            maxLength={4000}
            required
            aria-label={`Message ${otherName}`}
            placeholder="Type a message…"
            onKeyDown={(event) => {
              if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                formRef.current?.requestSubmit();
              }
            }}
            className="min-h-10 flex-1 resize-none rounded-control border border-border bg-surface p-2 font-control text-sm text-text-primary"
          />
          <Button type="submit" size="sm" disabled={isSending}>
            {isSending ? "…" : "Send"}
          </Button>
        </div>
      </form>
    </aside>
  );
}
