"use client";

import { useActionState, useState, useTransition } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { InboxIcon } from "@/components/ui/icons";
import { textareaClassName } from "@/components/ui/text-input";
import {
  moderateMessageAction,
  readFlaggedMessageAction,
  type AdminFormState,
} from "@/features/admin/actions";
import { waitedFor } from "@/features/admin/components/subject-line";
import type { FlaggedMessage } from "@/features/admin/types";

/**
 * Messages flagged as possible contact sharing, waiting on a decision.
 *
 * This is the one staff screen that can show a private message, and it is built so that
 * using it does not mean reading them all. Rows show who and when; the message itself
 * stays closed until someone opens it, and opening one is recorded against the staff
 * member by the API.
 *
 * The screen says so, in the row, before anyone clicks. A safeguard nobody is told about
 * changes no behaviour.
 */
export function FlaggedMessageQueue({ messages }: { messages: FlaggedMessage[] }) {
  if (messages.length === 0) {
    return (
      <EmptyState
        icon={<InboxIcon />}
        title="Nothing waiting"
        description="Messages that look like an exchange of phone numbers or email addresses appear here for a decision. Most are nothing."
      />
    );
  }

  return (
    <ul className="flex flex-col gap-4">
      {messages.map((message) => (
        <li key={message.messageId} className="surface-card p-5">
          <FlaggedMessageRow message={message} />
        </li>
      ))}
    </ul>
  );
}

function FlaggedMessageRow({ message }: { message: FlaggedMessage }) {
  const [state, formAction] = useActionState<AdminFormState, FormData>(
    moderateMessageAction.bind(null, message.messageId),
    {},
  );

  const [body, setBody] = useState<string | null>(null);
  const [readError, setReadError] = useState<string | null>(null);
  const [isReading, startReading] = useTransition();

  /*
   * Escalation asks for a reason before it will submit, so the note is held here to
   * decide whether the button is offered rather than letting someone press it and be
   * refused by the server.
   */
  const [notes, setNotes] = useState("");

  function open() {
    startReading(async () => {
      const result = await readFlaggedMessageAction(message.messageId);

      if (result.error) {
        setReadError(result.error);
        return;
      }

      setReadError(null);
      setBody(result.body ?? "");
    });
  }

  const sender = message.sentByCaregiver ? "caregiver" : "family";

  return (
    <>
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <p className="font-control text-sm font-semibold text-text-primary">
            {message.senderName} <span className="font-normal text-text-secondary">({sender})</span>
          </p>
          <p className="mt-0.5 text-sm text-text-secondary">to {message.recipientName}</p>
        </div>

        <p className="text-xs text-text-secondary">
          Flagged {waitedFor(message.sentAtUtc)}
        </p>
      </div>

      {body === null ? (
        <div className="mt-4 rounded-control bg-surface-muted px-4 py-3">
          <p className="text-sm text-text-primary">
            This looked like an exchange of contact details. The message is not shown until
            you open it.
          </p>
          <p className="mt-1 text-xs text-text-secondary">
            Opening it is recorded against your account.
          </p>

          <div className="mt-3">
            <Button type="button" variant="subtle" size="sm" onClick={open} disabled={isReading}>
              {isReading ? "Opening…" : "Open the message"}
            </Button>
          </div>

          {readError ? (
            <p role="alert" className="mt-2 text-sm text-danger-accent">
              {readError}
            </p>
          ) : null}
        </div>
      ) : (
        <blockquote className="mt-4 border-l-2 border-border-strong pl-4 text-sm text-text-primary">
          {body}
        </blockquote>
      )}

      <form action={formAction} className="mt-4 border-t border-border pt-4">
        <label
          htmlFor={`notes-${message.messageId}`}
          className="font-control text-sm font-medium text-text-primary"
        >
          Why, if you are escalating
        </label>
        <p className="text-xs text-text-secondary">
          This becomes a report about {message.senderName}. The message itself is not
          copied into it.
        </p>
        <textarea
          id={`notes-${message.messageId}`}
          name="notes"
          rows={2}
          maxLength={2000}
          value={notes}
          onChange={(event) => setNotes(event.target.value)}
          placeholder="Third time this account has pushed to move off-platform."
          className={`mt-1.5 ${textareaClassName}`}
        />

        {state.error ? (
          <p role="alert" className="mt-2 text-sm text-danger-accent">
            {state.error}
          </p>
        ) : null}

        <div className="mt-3 flex flex-wrap gap-2">
          {/*
            Nothing wrong is first and is the plain button, because it is the expected
            answer: the pattern matches ten digits in a row, which is also a postal code
            beside a house number or a date typed without spaces.
          */}
          <SettleButton
            value="NoConcern"
            label="Nothing wrong"
            pendingLabel="Clearing…"
          />
          <SettleButton
            value="Escalated"
            label="Escalate"
            pendingLabel="Escalating…"
            variant="subtle"
            disabled={notes.trim().length === 0}
          />
        </div>
      </form>
    </>
  );
}

function SettleButton({
  value,
  label,
  pendingLabel,
  variant,
  disabled,
}: {
  value: string;
  label: string;
  pendingLabel: string;
  variant?: "subtle";
  disabled?: boolean;
}) {
  const { pending } = useFormStatus();

  return (
    <Button
      type="submit"
      name="outcome"
      value={value}
      size="sm"
      variant={variant}
      disabled={pending || disabled}
    >
      {pending ? pendingLabel : label}
    </Button>
  );
}
