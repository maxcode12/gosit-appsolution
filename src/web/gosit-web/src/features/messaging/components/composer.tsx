"use client";

import { useActionState, useEffect, useRef } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { replyAction, type MessageFormState } from "@/features/messaging/actions";

/**
 * Writes a message into an open thread.
 *
 * Enter sends and Shift+Enter breaks the line — the convention every messaging app has
 * trained people into. The textarea grows with the message so a long one is not written
 * through a two-line slot.
 */
export function Composer({
  conversationId,
  recipientName,
}: {
  conversationId: string;
  recipientName: string;
}) {
  const [state, formAction] = useActionState<MessageFormState, FormData>(
    replyAction.bind(null, conversationId),
    {},
  );

  const formRef = useRef<HTMLFormElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  // Clear only once the server has accepted the message. Clearing on submit would throw
  // away what someone wrote if the send then failed.
  useEffect(() => {
    if (state.sentAt && textareaRef.current) {
      textareaRef.current.value = "";
      textareaRef.current.style.height = "auto";
    }
  }, [state.sentAt]);

  return (
    <form
      ref={formRef}
      action={formAction}
      className="sticky bottom-0 border-t border-border bg-surface p-4"
    >
      {state.error ? (
        <p role="alert" className="mb-3 rounded-control bg-red-50 px-4 py-3 text-sm text-red-800">
          {state.error}
        </p>
      ) : null}

      <div className="flex items-end gap-3">
        <textarea
          ref={textareaRef}
          name="body"
          rows={1}
          maxLength={4000}
          required
          aria-label={`Message ${recipientName}`}
          placeholder={`Message ${recipientName}…`}
          onInput={(event) => {
            const el = event.currentTarget;
            el.style.height = "auto";
            el.style.height = `${Math.min(el.scrollHeight, 200)}px`;
          }}
          onKeyDown={(event) => {
            if (event.key === "Enter" && !event.shiftKey) {
              event.preventDefault();
              formRef.current?.requestSubmit();
            }
          }}
          className="max-h-50 min-h-12 flex-1 resize-none rounded-control border border-border bg-surface p-3 font-control text-sm text-text-primary"
        />

        <SendButton />
      </div>

      {/*
        Not "contact details are not allowed" — the API flags such messages for review
        rather than blocking them, so saying they are blocked would be untrue. This gives
        the reason instead, which is the part that actually changes anyone's mind.
      */}
      <p className="mt-2 text-xs text-text-secondary">
        Keep arrangements on GoSit. Off-platform bookings lose the record of what was
        agreed, the verified identity, and the payment protection.
      </p>
    </form>
  );
}

function SendButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" disabled={pending} className="h-12 shrink-0">
      {pending ? "Sending…" : "Send"}
    </Button>
  );
}
