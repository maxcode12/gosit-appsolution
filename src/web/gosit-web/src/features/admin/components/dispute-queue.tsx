"use client";

import { useActionState, useState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { WalletIcon } from "@/components/ui/icons";
import { inputClassName, textareaClassName } from "@/components/ui/text-input";
import { resolveDisputeAction, type AdminFormState } from "@/features/admin/actions";
import { waitedFor } from "@/features/admin/components/subject-line";
import type { OpenDispute } from "@/features/admin/types";

/**
 * Disputed bookings waiting on a decision.
 *
 * Each row is holding a caregiver's payout: they are not being paid and the family is not
 * being answered, both at once, for as long as it sits here. Oldest first, and the wait is
 * shown, because that is the number that should make someone uncomfortable.
 *
 * The decision is a single amount — what goes back to the family — and the screen is built
 * around saying so plainly, because the consequences of each answer are not obvious from
 * the number alone.
 */
export function DisputeQueue({ disputes }: { disputes: OpenDispute[] }) {
  if (disputes.length === 0) {
    return (
      <EmptyState
        icon={<WalletIcon />}
        title="No disputes waiting"
        description="When a family or a caregiver disputes a booking it appears here, and the caregiver's payout is held until someone decides."
      />
    );
  }

  return (
    <ul className="flex flex-col gap-4">
      {disputes.map((dispute) => (
        <li key={dispute.careBookingId} className="surface-card p-5">
          <DisputeRow dispute={dispute} />
        </li>
      ))}
    </ul>
  );
}

function DisputeRow({ dispute }: { dispute: OpenDispute }) {
  const [state, formAction] = useActionState<AdminFormState, FormData>(
    resolveDisputeAction.bind(null, dispute.careBookingId),
    {},
  );

  /*
   * The amount is held here so the consequence below it can react as it is typed.
   *
   * That reaction is the point of the screen: "60" means nothing on its own, and what a
   * staff member needs to see is that it leaves the caregiver with a particular figure.
   */
  const [refund, setRefund] = useState("0");

  return (
    <>
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <p className="font-control text-sm font-semibold text-text-primary">
            {dispute.careServiceName}
          </p>
          <p className="mt-0.5 text-sm text-text-secondary">
            {dispute.careRequesterName} and {dispute.caregiverName}
          </p>
        </div>

        <p className="text-xs text-text-secondary">
          Disputed {waitedFor(dispute.disputedAtUtc)}
        </p>
      </div>

      {dispute.reason ? (
        <blockquote className="mt-3 border-l-2 border-border-strong pl-4 text-sm text-text-primary">
          {dispute.reason}
        </blockquote>
      ) : (
        <p className="mt-3 text-sm text-text-secondary">No reason was given.</p>
      )}

      <dl className="mt-4 flex flex-wrap gap-x-8 gap-y-2 rounded-control bg-surface-muted px-4 py-3 text-sm">
        <div className="flex gap-2">
          <dt className="text-text-secondary">Family paid</dt>
          <dd className="text-text-primary">{money(dispute.amountPaid)}</dd>
        </div>
        <div className="flex gap-2">
          <dt className="text-text-secondary">Caregiver is owed</dt>
          <dd className="text-text-primary">{money(dispute.payoutAmount)}</dd>
        </div>
      </dl>

      {/*
        The case worth warning about before the decision rather than after it. If the
        transfer already left, a refund comes out of GoSit rather than out of the payout.
      */}
      {!dispute.payoutIsRecoverable ? (
        <p className="mt-3 rounded-control bg-accent-soft px-4 py-3 text-sm text-text-primary">
          The caregiver has already been paid for this booking. A refund here comes out of
          GoSit, not out of their earnings.
        </p>
      ) : null}

      <form action={formAction} className="mt-4 border-t border-border pt-4">
        <label
          htmlFor={`refund-${dispute.careBookingId}`}
          className="font-control text-sm font-medium text-text-primary"
        >
          How much goes back to the family?
        </label>

        <div className="mt-1.5 flex flex-wrap items-center gap-2">
          <input
            id={`refund-${dispute.careBookingId}`}
            name="refundAmount"
            type="number"
            min="0"
            max={dispute.amountPaid}
            step="0.01"
            required
            value={refund}
            onChange={(event) => setRefund(event.target.value)}
            className={`${inputClassName} max-w-36`}
          />

          {/* The two ends, because they are the two most common answers. */}
          <Button type="button" variant="subtle" size="sm" onClick={() => setRefund("0")}>
            Nothing
          </Button>
          <Button
            type="button"
            variant="subtle"
            size="sm"
            onClick={() => setRefund(String(dispute.amountPaid))}
          >
            Everything
          </Button>
        </div>

        <p className="mt-2 text-sm text-text-secondary">{consequence(dispute, refund)}</p>

        <label
          htmlFor={`notes-${dispute.careBookingId}`}
          className="mt-4 block font-control text-sm font-medium text-text-primary"
        >
          Why
        </label>
        <p className="text-xs text-text-secondary">
          Both the family and the caregiver read this on the booking, in these words.
        </p>
        <textarea
          id={`notes-${dispute.careBookingId}`}
          name="notes"
          rows={3}
          required
          maxLength={2000}
          placeholder="Check-in and check-out show three hours, not the five booked. Refunding the difference."
          className={`mt-1.5 ${textareaClassName}`}
        />

        {state.error ? (
          <p role="alert" className="mt-2 text-sm text-danger-accent">
            {state.error}
          </p>
        ) : null}

        <div className="mt-3">
          <SettleButton />
        </div>
      </form>
    </>
  );
}

/**
 * What this amount actually does, in the terms the decision is about.
 *
 * A number in a box is not a decision someone can check. "$60" is only meaningful once it
 * reads as "the caregiver keeps the rest" or "the caregiver is paid nothing".
 */
function consequence(dispute: OpenDispute, refund: string): string {
  const amount = Number(refund);

  if (!Number.isFinite(amount) || amount < 0) {
    return "Enter an amount, or zero to dismiss the dispute.";
  }

  if (amount > dispute.amountPaid) {
    return `That is more than the family paid (${money(dispute.amountPaid)}).`;
  }

  if (amount === 0) {
    return `The dispute is dismissed and ${dispute.caregiverName} is paid in full.`;
  }

  if (amount >= dispute.amountPaid) {
    return `The family is refunded everything and ${dispute.caregiverName} is paid nothing for this booking.`;
  }

  return (
    `The family gets ${money(amount)} back. ${dispute.caregiverName}'s payout is `
    + `recalculated on the ${money(dispute.amountPaid - amount)} they keep, less GoSit's fee.`
  );
}

function SettleButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" size="sm" disabled={pending}>
      {pending ? "Settling…" : "Settle this dispute"}
    </Button>
  );
}

function money(amount: number): string {
  return amount.toLocaleString("en-CA", { style: "currency", currency: "CAD" });
}
