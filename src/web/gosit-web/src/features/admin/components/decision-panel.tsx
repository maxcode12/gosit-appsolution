"use client";

import { useActionState, useState, useTransition } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { textareaClassName } from "@/components/ui/text-input";
import type { AdminFormState } from "@/features/admin/actions";

/**
 * Approve or refuse, with the refusal behind a required reason.
 *
 * Shared by the checks and certificates queues, which make the same shape of decision.
 * Approving is one press; refusing takes a sentence, on purpose — the person on the
 * other end is being kept from work and deserves to know what to fix. The API enforces
 * the same rule, so this is a courtesy rather than the guard.
 */
export function DecisionPanel({
  approve,
  reject,
  approveLabel = "Approve",
  rejectLabel = "Reject",
  rejectPrompt,
  rejectPlaceholder,
  approveBlockedReason,
}: {
  approve: () => Promise<AdminFormState>;
  reject: (previous: AdminFormState, formData: FormData) => Promise<AdminFormState>;
  approveLabel?: string;
  rejectLabel?: string;
  rejectPrompt: string;
  rejectPlaceholder: string;
  /**
   * Why approval is not available, when it is not.
   *
   * The server refuses regardless — this only saves someone pressing a button to be told
   * no. Rejection stays available on purpose: the reason approval is blocked is usually
   * also the reason to reject.
   */
  approveBlockedReason?: string;
}) {
  const [isRejecting, setIsRejecting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  const [rejectState, rejectAction] = useActionState<AdminFormState, FormData>(reject, {});

  if (isRejecting) {
    return (
      <form action={rejectAction} className="mt-4 border-t border-border pt-4">
        <label
          htmlFor="reason"
          className="font-control text-sm font-medium text-text-secondary"
        >
          {rejectPrompt}
        </label>

        <textarea
          id="reason"
          name="reason"
          rows={2}
          maxLength={1000}
          required
          autoFocus
          placeholder={rejectPlaceholder}
          className={`mt-1.5 ${textareaClassName}`}
        />

        {rejectState.error ? (
          <p role="alert" className="mt-2 text-sm text-danger-accent">
            {rejectState.error}
          </p>
        ) : null}

        <div className="mt-3 flex gap-2">
          <RejectButton label={rejectLabel} />
          <Button type="button" variant="subtle" size="sm" onClick={() => setIsRejecting(false)}>
            Cancel
          </Button>
        </div>
      </form>
    );
  }

  return (
    <div className="mt-4 border-t border-border pt-4">
      {error ? (
        <p role="alert" className="mb-3 text-sm text-danger-accent">
          {error}
        </p>
      ) : null}

      {approveBlockedReason ? (
        <p className="mb-3 text-sm text-text-secondary">{approveBlockedReason}</p>
      ) : null}

      <div className="flex flex-wrap gap-2">
        <Button
          type="button"
          size="sm"
          disabled={isPending || approveBlockedReason !== undefined}
          onClick={() =>
            startTransition(async () => {
              setError(null);

              const result = await approve();

              if (result.error) {
                setError(result.error);
              }
            })
          }
        >
          {isPending ? "Approving…" : approveLabel}
        </Button>

        <Button
          type="button"
          variant="danger"
          size="sm"
          onClick={() => setIsRejecting(true)}
        >
          {rejectLabel}
        </Button>
      </div>
    </div>
  );
}

function RejectButton({ label }: { label: string }) {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" variant="danger" size="sm" disabled={pending}>
      {pending ? "Saving…" : `Confirm ${label.toLowerCase()}`}
    </Button>
  );
}
