"use client";

import { useActionState, useState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { inputClassName } from "@/components/ui/text-input";
import {
  applyReferralCodeAction,
  type ReferralFormState,
} from "@/features/growth/actions";
import {
  REFERRAL_STATUS_HINTS,
  REFERRAL_STATUS_LABELS,
  type ReferralSummary,
} from "@/features/growth/types";

/**
 * Your referral code, what it has produced, and a place to enter someone else's.
 *
 * The reward is deliberately not stated as an amount. `Referral.Reward` records one, but
 * nothing in the platform decides what it should be or grants it yet — so promising "$20
 * when they book" would be a promise the product cannot keep. What is shown instead is
 * what is true: the invitation is tracked, and it counts once they complete a booking.
 */
export function ReferralPanel({ summary }: { summary: ReferralSummary }) {
  return (
    <div className="flex flex-col gap-8">
      <ShareCode code={summary.myReferralCode} />

      <section>
        <h2 className="font-heading text-lg font-semibold text-text-primary">
          Your invitations
        </h2>

        {/*
          An invitation that counts is recorded and shown. What follows a counted
          invitation is not decided yet — no amount is set and nothing grants one — so
          the "Rewarded" column below stays at zero. Saying that outright beats a column
          that reads like a payment on its way.
        */}
        <p className="mt-2 max-w-lg text-sm text-text-secondary">
          Invitations are tracked from today. What a counted invitation earns has not
          been set yet, so nothing has been paid out — we will tell you before that
          changes, and everything counted here still counts.
        </p>

        <dl className="mt-4 grid gap-4 sm:grid-cols-3">
          <Stat
            label="Signed up"
            value={summary.pendingCount}
            hint="Joined, but not booked yet."
          />
          <Stat
            label="Counted"
            value={summary.qualifiedCount}
            hint="Completed a booking."
            emphasis
          />
          <Stat
            label="Rewarded"
            value={summary.rewardedCount}
            hint="Reward granted."
          />
        </dl>

        <div className="mt-6">
          {summary.referrals.length === 0 ? (
            <p className="rounded-card border border-dashed border-border-strong px-6 py-10 text-center text-sm text-text-secondary">
              Nobody has used your code yet. Share it with someone who needs care and it
              will show up here.
            </p>
          ) : (
            <ul className="flex flex-col gap-3">
              {summary.referrals.map((referral) => (
                <li
                  key={referral.id}
                  className="flex flex-wrap items-center justify-between gap-3 rounded-card border border-border bg-surface p-4"
                >
                  <div>
                    <p className="font-control text-sm font-semibold text-text-primary">
                      {REFERRAL_STATUS_LABELS[referral.status] ?? referral.status}
                    </p>
                    <p className="mt-0.5 text-sm text-text-secondary">
                      {REFERRAL_STATUS_HINTS[referral.status] ?? ""}
                    </p>
                  </div>

                  <time
                    dateTime={referral.createdAtUtc}
                    className="text-xs text-text-secondary"
                  >
                    {new Date(referral.createdAtUtc).toLocaleDateString("en-CA", {
                      day: "numeric",
                      month: "short",
                      year: "numeric",
                    })}
                  </time>
                </li>
              ))}
            </ul>
          )}
        </div>
      </section>

      <ApplyCode />
    </div>
  );
}

/**
 * The code, with a copy button.
 *
 * Falls back to selecting the text when the clipboard API is unavailable — which it is
 * over plain HTTP on anything but localhost, so this is a real case rather than a
 * defensive flourish.
 */
function ShareCode({ code }: { code: string }) {
  const [copied, setCopied] = useState(false);

  return (
    <section className="rounded-card bg-brand-deep p-6 sm:p-8">
      <h2 className="font-heading text-xl font-bold text-text-on-deep">
        Invite someone who needs care
      </h2>
      <p className="mt-2 max-w-lg text-sm leading-relaxed text-text-on-deep/80">
        Share your code. When someone joins with it and completes their first booking,
        the referral counts towards you.
      </p>

      <div className="mt-6 flex flex-wrap items-center gap-3">
        <code className="rounded-control bg-white/15 px-5 py-3 font-heading text-2xl font-extrabold tracking-[0.2em] text-text-on-deep">
          {code}
        </code>

        <Button
          type="button"
          variant="secondary"
          onClick={async () => {
            try {
              await navigator.clipboard.writeText(code);
              setCopied(true);
              window.setTimeout(() => setCopied(false), 2000);
            } catch {
              // No clipboard permission, or an insecure context. Select it instead so
              // the reader can copy it themselves rather than being told nothing.
              const node = document.querySelector("code");
              if (node) {
                const range = document.createRange();
                range.selectNodeContents(node);
                const selection = window.getSelection();
                selection?.removeAllRanges();
                selection?.addRange(range);
              }
            }
          }}
        >
          {copied ? "Copied" : "Copy code"}
        </Button>
      </div>

      <p aria-live="polite" className="sr-only">
        {copied ? "Referral code copied to your clipboard." : ""}
      </p>
    </section>
  );
}

/** Enter a code you were given. Once only — the API refuses a second. */
function ApplyCode() {
  const [state, formAction] = useActionState<ReferralFormState, FormData>(
    applyReferralCodeAction,
    {},
  );

  if (state.applied) {
    return (
      <section className="rounded-card border border-brand-soft-border bg-brand-soft p-6">
        <h2 className="font-heading text-base font-semibold text-brand-primary">
          Code applied
        </h2>
        <p className="mt-1 text-sm text-text-primary">
          Whoever invited you gets credit once you have completed your first booking.
        </p>
      </section>
    );
  }

  return (
    <section className="rounded-card border border-border bg-surface p-6">
      <h2 className="font-heading text-base font-semibold text-text-primary">
        Were you invited?
      </h2>
      <p className="mt-1 text-sm text-text-secondary">
        Enter the code you were given. You can do this once, and not with your own code.
      </p>

      <form action={formAction} className="mt-4 flex flex-wrap items-start gap-3">
        <div className="min-w-48 flex-1">
          <label htmlFor="code" className="sr-only">
            Referral code
          </label>
          <input
            id="code"
            name="code"
            maxLength={32}
            required
            placeholder="ABC12345"
            className={`${inputClassName} uppercase tracking-widest`}
          />
        </div>

        <ApplyButton />
      </form>

      {state.error ? (
        <p role="alert" className="mt-3 text-sm text-danger-accent">
          {state.error}
        </p>
      ) : null}
    </section>
  );
}

function ApplyButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" variant="ghost" disabled={pending}>
      {pending ? "Applying…" : "Apply code"}
    </Button>
  );
}

function Stat({
  label,
  value,
  hint,
  emphasis = false,
}: {
  label: string;
  value: number;
  hint: string;
  emphasis?: boolean;
}) {
  return (
    <div
      className={`rounded-card border p-5 ${
        emphasis ? "border-brand-soft-border bg-brand-soft" : "border-border bg-surface"
      }`}
    >
      <dt className="text-sm text-text-secondary">{label}</dt>
      <dd className="mt-1 font-heading text-3xl font-extrabold text-text-primary">
        {value}
      </dd>
      <p className="mt-1 text-sm text-text-secondary">{hint}</p>
    </div>
  );
}
