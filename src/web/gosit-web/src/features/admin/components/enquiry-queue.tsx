"use client";

import { useState, useTransition } from "react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { InboxIcon } from "@/components/ui/icons";
import { closeEnquiryAction } from "@/features/admin/actions";
import { waitedFor } from "@/features/admin/components/subject-line";
import type { AdminEnquiry } from "@/features/admin/types";

const KIND_LABELS: Record<string, string> = {
  Family: "Family",
  Caregiver: "Caregiver",
  Partner: "Partner or vendor",
  Other: "Other",
};

/**
 * Messages from the public contact form, oldest first.
 *
 * The sender has no account, so reply is a plain email link rather than an in-app action.
 * Closing is one click either way, and a spam message is marked rather than deleted so a
 * run of them stays visible.
 */
export function EnquiryQueue({ enquiries }: { enquiries: AdminEnquiry[] }) {
  if (enquiries.length === 0) {
    return (
      <EmptyState
        icon={<InboxIcon />}
        title="No new messages"
        description="Messages sent through the contact form on the website appear here, oldest first."
      />
    );
  }

  return (
    <ul className="flex flex-col gap-4">
      {enquiries.map((enquiry) => (
        <li key={enquiry.enquiryId}>
          <EnquiryRow enquiry={enquiry} />
        </li>
      ))}
    </ul>
  );
}

function EnquiryRow({ enquiry }: { enquiry: AdminEnquiry }) {
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  function close(outcome: "handled" | "spam") {
    setError(null);

    startTransition(async () => {
      const result = await closeEnquiryAction(enquiry.enquiryId, outcome);

      if (result.error) {
        setError(result.error);
      }
    });
  }

  return (
    <article className="surface-card p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="font-heading text-base font-semibold text-text-primary">{enquiry.name}</p>
          <a
            href={`mailto:${enquiry.email}`}
            className="text-sm text-brand-primary hover:underline"
          >
            {enquiry.email}
          </a>
        </div>

        <span className="rounded-pill bg-surface-muted px-2.5 py-1 text-xs font-medium text-text-secondary">
          {KIND_LABELS[enquiry.kind] ?? enquiry.kind}
        </span>
      </div>

      <p className="mt-4 whitespace-pre-wrap rounded-control bg-surface-muted px-4 py-3 text-sm leading-relaxed text-text-primary">
        {enquiry.message}
      </p>

      <p className="mt-3 text-xs text-text-secondary">Received {waitedFor(enquiry.createdAtUtc)}</p>

      {error ? (
        <p role="alert" className="mt-3 text-sm text-danger-accent">
          {error}
        </p>
      ) : null}

      <div className="mt-4 flex flex-wrap gap-2">
        <Button type="button" size="sm" disabled={isPending} onClick={() => close("handled")}>
          {isPending ? "Saving…" : "Mark as handled"}
        </Button>
        <Button
          type="button"
          size="sm"
          variant="ghost"
          disabled={isPending}
          onClick={() => close("spam")}
        >
          Not a real message
        </Button>
      </div>
    </article>
  );
}
