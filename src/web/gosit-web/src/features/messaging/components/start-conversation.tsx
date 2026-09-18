"use client";

import Link from "next/link";
import { useActionState, useState } from "react";
import { useFormStatus } from "react-dom";
import { Button, buttonClassName } from "@/components/ui/button";
import { startConversationAction, type MessageFormState } from "@/features/messaging/actions";

/**
 * Opens a conversation with a caregiver from their profile.
 *
 * Inline rather than a separate route: the question someone wants to ask occurs to them
 * while reading the profile, and sending them elsewhere to ask it loses the thought.
 */
export function StartConversation({
  caregiverProfileId,
  caregiverName,
  hasProfile,
}: {
  caregiverProfileId: string;
  caregiverName: string;
  /** The API refuses to open a thread without one, so it is checked before offering. */
  hasProfile: boolean;
}) {
  const [isOpen, setIsOpen] = useState(false);

  const [state, formAction] = useActionState<MessageFormState, FormData>(
    startConversationAction.bind(null, caregiverProfileId),
    {},
  );

  if (!hasProfile) {
    return (
      <div>
        <Link href="/account" className={buttonClassName("ghost", "w-full")}>
          Message {caregiverName}
        </Link>
        <p className="mt-2 text-xs text-text-secondary">
          Add your details first so {caregiverName} knows who is writing.
        </p>
      </div>
    );
  }

  if (!isOpen) {
    return (
      <Button
        type="button"
        variant="ghost"
        className="w-full"
        onClick={() => setIsOpen(true)}
      >
        Message {caregiverName}
      </Button>
    );
  }

  return (
    <form action={formAction} className="flex flex-col gap-3">
      <label htmlFor="body" className="font-control text-sm text-text-secondary">
        Message {caregiverName}
      </label>

      <textarea
        id="body"
        name="body"
        rows={4}
        maxLength={4000}
        required
        autoFocus
        placeholder={`Hi ${caregiverName} — are you free on Saturday mornings?`}
        className="w-full rounded-control border border-border bg-surface p-3 font-control text-sm text-text-primary"
      />

      {state.error ? (
        <p role="alert" className="text-sm text-red-700">
          {state.error}
        </p>
      ) : null}

      <p className="text-xs text-text-secondary">
        Keep arrangements on GoSit. Off-platform bookings lose the record of what was
        agreed, the verified identity, and the payment protection.
      </p>

      <div className="flex gap-2">
        <SendButton />
        <Button type="button" variant="ghost" onClick={() => setIsOpen(false)}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

function SendButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" disabled={pending}>
      {pending ? "Sending…" : "Send message"}
    </Button>
  );
}
