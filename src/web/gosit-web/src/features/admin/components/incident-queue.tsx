"use client";

import { useActionState, useEffect, useState, useTransition } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { ShieldIcon } from "@/components/ui/icons";
import { textareaClassName } from "@/components/ui/text-input";
import {
  closeIncidentAction,
  reinstateAccountAction,
  suspendAccountAction,
  type AdminFormState,
} from "@/features/admin/actions";
import { SubjectLine, waitedFor } from "@/features/admin/components/subject-line";
import type { AdminIncident } from "@/features/admin/types";

/**
 * Reports waiting on triage.
 *
 * Urgent ones are marked rather than merely sorted first: a reviewer scrolling a long
 * queue should not have to infer severity from position. Suspension sits on the row,
 * because the moment you decide someone is a danger is the moment you want to act, not
 * three screens later.
 */
export function IncidentQueue({ incidents }: { incidents: AdminIncident[] }) {
  if (incidents.length === 0) {
    return (
      <EmptyState
        icon={<ShieldIcon />}
        title="Nothing to triage"
        description="Reports from families and caregivers land here. An empty queue is the goal, not a bug."
      />
    );
  }

  return (
    <ul className="flex flex-col gap-4">
      {incidents.map((incident) => (
        <li key={incident.id}>
          <IncidentRow incident={incident} />
        </li>
      ))}
    </ul>
  );
}

function IncidentRow({ incident }: { incident: AdminIncident }) {
  const [panel, setPanel] = useState<"none" | "close" | "suspend">("none");

  const isSuspended = incident.subjectAccountStatus === "Suspended";

  return (
    <article
      className={`surface-card p-5 ${
        incident.requiresUrgentReview ? "border-danger-accent/40 bg-danger-soft/30" : ""
      }`}
    >
      <div className="flex flex-wrap items-start justify-between gap-4">
        <SubjectLine
          name={incident.subjectName}
          email={incident.subjectEmail}
          role={incident.subjectRole}
          accountStatus={incident.subjectAccountStatus}
        />

        <div className="flex flex-wrap items-center gap-2">
          {incident.requiresUrgentReview ? (
            <span className="rounded-pill bg-danger-accent px-2.5 py-1 text-xs font-bold uppercase tracking-wide text-white">
              Urgent
            </span>
          ) : null}
          <span className="rounded-pill bg-surface-muted px-2.5 py-1 text-xs font-semibold text-text-secondary">
            {incident.severity}
          </span>
          <span className="rounded-pill bg-surface-muted px-2.5 py-1 text-xs font-medium text-text-secondary">
            {incident.category}
          </span>
        </div>
      </div>

      <p className="mt-4 whitespace-pre-wrap rounded-control bg-surface-muted px-4 py-3 text-sm leading-relaxed text-text-primary">
        {incident.description}
      </p>

      <p className="mt-3 text-xs text-text-secondary">
        Reported by {incident.reportedByName} · {waitedFor(incident.createdAtUtc)}
        {incident.careBookingId ? " · linked to a booking" : ""}
      </p>

      {panel === "close" ? (
        <CloseForm incident={incident} onCancel={() => setPanel("none")} />
      ) : panel === "suspend" ? (
        <SuspendForm incident={incident} onCancel={() => setPanel("none")} />
      ) : (
        <div className="mt-4 flex flex-wrap gap-2 border-t border-border pt-4">
          <Button type="button" size="sm" onClick={() => setPanel("close")}>
            Resolve
          </Button>

          {isSuspended ? (
            <Reinstate userId={incident.subjectUserId} />
          ) : (
            <Button
              type="button"
              variant="danger"
              size="sm"
              onClick={() => setPanel("suspend")}
            >
              Suspend account
            </Button>
          )}
        </div>
      )}
    </article>
  );
}

/**
 * Closing a report.
 *
 * "Resolve" and "dismiss" are recorded differently on purpose — a report that was acted
 * on and one that was found to be baseless are not the same history, and flattening them
 * would make the record useless the first time someone audits a pattern of complaints.
 */
function CloseForm({
  incident,
  onCancel,
}: {
  incident: AdminIncident;
  onCancel: () => void;
}) {
  const [dismiss, setDismiss] = useState(false);

  const [state, action] = useActionState<AdminFormState, FormData>(
    (previous, formData) => closeIncidentAction(incident.id, dismiss, previous, formData),
    {},
  );

  return (
    <form action={action} className="mt-4 border-t border-border pt-4">
      <label htmlFor={`notes-${incident.id}`} className="font-control text-sm font-medium text-text-secondary">
        What was done?
      </label>

      <textarea
        id={`notes-${incident.id}`}
        name="notes"
        rows={2}
        maxLength={2000}
        required
        autoFocus
        placeholder="Spoke to both parties. Caregiver has been warned about lateness."
        className={`mt-1.5 ${textareaClassName}`}
      />

      <label className="mt-3 flex cursor-pointer items-center gap-2.5 text-sm text-text-primary">
        <input
          type="checkbox"
          checked={dismiss}
          onChange={(event) => setDismiss(event.target.checked)}
          className="size-4 accent-[var(--color-brand-primary)]"
        />
        No action needed — dismiss this report
      </label>

      {state.error ? (
        <p role="alert" className="mt-2 text-sm text-danger-accent">
          {state.error}
        </p>
      ) : null}

      <div className="mt-3 flex gap-2">
        <PendingButton idle={dismiss ? "Dismiss report" : "Close report"} busy="Saving…" />
        <Button type="button" variant="subtle" size="sm" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

function SuspendForm({
  incident,
  onCancel,
}: {
  incident: AdminIncident;
  onCancel: () => void;
}) {
  const [state, action] = useActionState<AdminFormState, FormData>(
    suspendAccountAction.bind(null, incident.subjectUserId),
    {},
  );

  // Revalidation re-renders the row as suspended, but the panel is local state and
  // would otherwise stay open over the top of it. Closing on success is what makes the
  // row settle into showing "Reinstate".
  useEffect(() => {
    if (state.done) {
      onCancel();
    }
  }, [state.done, onCancel]);

  return (
    <form action={action} className="mt-4 border-t border-border pt-4">
      <p className="text-sm text-text-primary">
        Suspending {incident.subjectName} signs them out everywhere and removes them from
        search immediately. Existing bookings are not cancelled automatically.
      </p>

      <label
        htmlFor={`reason-${incident.id}`}
        className="mt-3 block font-control text-sm font-medium text-text-secondary"
      >
        Reason for suspension
      </label>

      <textarea
        id={`reason-${incident.id}`}
        name="reason"
        rows={2}
        maxLength={1000}
        required
        autoFocus
        placeholder="Credible safety concern raised by a family; pending investigation."
        className={`mt-1.5 ${textareaClassName}`}
      />

      {state.error ? (
        <p role="alert" className="mt-2 text-sm text-danger-accent">
          {state.error}
        </p>
      ) : null}

      <div className="mt-3 flex gap-2">
        <PendingButton idle="Suspend account" busy="Suspending…" variant="danger" />
        <Button type="button" variant="subtle" size="sm" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

function Reinstate({ userId }: { userId: string }) {
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  return (
    <>
      <Button
        type="button"
        variant="ghost"
        size="sm"
        disabled={isPending}
        onClick={() =>
          startTransition(async () => {
            setError(null);

            const result = await reinstateAccountAction(userId);

            if (result.error) {
              setError(result.error);
            }
          })
        }
      >
        {isPending ? "Reinstating…" : "Reinstate account"}
      </Button>

      {error ? (
        <p role="alert" className="w-full text-sm text-danger-accent">
          {error}
        </p>
      ) : null}
    </>
  );
}

function PendingButton({
  idle,
  busy,
  variant = "primary",
}: {
  idle: string;
  busy: string;
  variant?: "primary" | "danger";
}) {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" variant={variant} size="sm" disabled={pending}>
      {pending ? busy : idle}
    </Button>
  );
}
