"use client";

import { useState, useTransition } from "react";
import { Button } from "@/components/ui/button";
import { startPayoutOnboardingAction } from "@/features/caregivers/actions";
import { formatMoney } from "@/features/bookings/components/booking-status";
import type { PayoutAccount } from "@/features/caregivers/types";

/**
 * Getting a caregiver set up to be paid.
 *
 * The states are kept apart on purpose. Someone who has submitted everything and is
 * waiting on a review needs to be told that, not told to start again — and someone whose
 * account has been restricted needs to know why. A single "set up payouts" button for
 * all three would be the most frustrating screen in the product.
 *
 * The money already owed is shown in every state, because that is the thing that makes
 * finishing onboarding worth doing.
 */
export function PayoutSetup({
  account,
  outcome,
}: {
  account: PayoutAccount;
  /** How the browser got back here from the provider, if it did. */
  outcome?: "complete" | "refresh";
}) {
  if (!account.billingConfigured) {
    return (
      <section className="rounded-card border border-border bg-surface p-5">
        <h2 className="font-heading text-base font-semibold text-text-primary">Payouts</h2>
        <p className="mt-1 text-sm text-text-secondary">
          Payouts are not connected on this environment yet. Anything you earn is still
          recorded and will be sent once they are.
        </p>
        {account.pendingBalance > 0 ? <Owed amount={account.pendingBalance} /> : null}
      </section>
    );
  }

  if (account.payoutsEnabled) {
    return (
      <section className="rounded-card border border-brand-soft-border bg-brand-soft p-5">
        <div className="flex items-center gap-2">
          <span aria-hidden className="size-2.5 rounded-full bg-brand-primary" />
          <h2 className="font-heading text-base font-semibold text-text-primary">
            You are set up to be paid
          </h2>
        </div>
        {/*
          "Once a booking has been paid for" was true until payouts started waiting out
          the family's dispute window. It is the wait, not the payment, that decides when
          money moves now — and a caregiver reading the old wording would be counting days
          from the wrong event.
        */}
        <p className="mt-1 text-sm text-text-secondary">
          Earnings are sent automatically once a booking can no longer be disputed, which
          is two weeks after the care finished. They are deposited to your bank account and
          usually arrive within a few days of being sent.
        </p>
        {account.pendingBalance > 0 ? <Owed amount={account.pendingBalance} /> : null}
      </section>
    );
  }

  return <NeedsSetup account={account} outcome={outcome} />;
}

function NeedsSetup({
  account,
  outcome,
}: {
  account: PayoutAccount;
  outcome?: "complete" | "refresh";
}) {
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  // Submitted but not yet enabled: the provider is checking. Telling this person to
  // "set up payouts" would be telling them to redo what they just did.
  const underReview = account.detailsSubmitted && !account.disabledReason;

  return (
    <section className="rounded-card border border-brand-accent/40 bg-accent-soft p-5">
      <h2 className="font-heading text-base font-semibold text-text-primary">
        {underReview ? "We are checking your details" : "Set up payouts to get paid"}
      </h2>

      <p className="mt-1 text-sm leading-relaxed text-text-primary">
        {underReview
          ? "You have submitted everything. Our payment provider is reviewing it, which usually takes a day or two. Nothing more is needed from you right now."
          : "You can take bookings without this, but we cannot send you the money until it is done. It takes a few minutes."}
      </p>

      {account.disabledReason ? (
        <p className="mt-3 rounded-control bg-surface px-4 py-3 text-sm text-text-primary">
          {explainDisabledReason(account.disabledReason)}
        </p>
      ) : null}

      {outcome === "refresh" ? (
        <p className="mt-3 text-sm text-text-secondary">
          That link expired before you finished. Start again below — nothing you entered
          was lost.
        </p>
      ) : null}

      {outcome === "complete" && !underReview ? (
        <p className="mt-3 text-sm text-text-secondary">
          It looks like there is still something outstanding. Continue below to finish.
        </p>
      ) : null}

      {account.pendingBalance > 0 ? <Owed amount={account.pendingBalance} waiting /> : null}

      {error ? (
        <p role="alert" className="mt-3 text-sm text-danger-accent">
          {error}
        </p>
      ) : null}

      {underReview ? null : (
        <Button
          type="button"
          disabled={isPending}
          className="mt-4"
          onClick={() =>
            startTransition(async () => {
              setError(null);

              const result = await startPayoutOnboardingAction();

              // A successful call redirects, so reaching here means it failed.
              if (result?.error) {
                setError(result.error);
              }
            })
          }
        >
          {isPending
            ? "Opening…"
            : account.hasAccount
              ? "Finish setting up payouts"
              : "Set up payouts"}
        </Button>
      )}

      <p className="mt-3 text-xs leading-relaxed text-text-secondary">
        Your bank details go straight to our payment provider. GoSit never sees or
        stores them.
      </p>
    </section>
  );
}

function Owed({ amount, waiting = false }: { amount: number; waiting?: boolean }) {
  return (
    <p className="mt-4 rounded-control bg-surface px-4 py-3 text-sm text-text-primary">
      <strong className="font-semibold">{formatMoney(amount)}</strong>{" "}
      {waiting
        ? "is waiting to be sent to you. It is yours — it stays owed until it reaches you."
        : "is on its way to you."}
    </p>
  );
}

/**
 * Turns the provider's machine reason into something a person can act on.
 *
 * Falls back to the raw code rather than swallowing it: an unrecognised reason someone
 * can search for beats a vague "there was a problem".
 */
function explainDisabledReason(reason: string): string {
  const known: Record<string, string> = {
    // Stripe's v2 Accounts API returns these as status_details codes, underscore-
    // separated throughout — not the dot-separated shape v1 used, which this map was
    // first written against and which never actually matched a live account.
    "requirements_past_due":
      "Some details are outstanding. Continue below to provide them.",
    "requirements_pending_verification":
      "Your details are being verified. This usually takes a day or two.",
    "rejected_fraud": "Our payment provider could not verify this account. Contact support.",
    "rejected_terms_of_service":
      "This account was declined by our payment provider. Contact support.",
    "listed": "This account is under additional review. Contact support if it persists.",
    "under_review": "This account is being reviewed by our payment provider.",
    "provider_not_configured": "Payouts are not connected on this environment yet.",
  };

  return known[reason] ?? `Our payment provider is holding this account (${reason}).`;
}
