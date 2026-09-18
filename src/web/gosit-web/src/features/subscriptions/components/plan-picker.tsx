"use client";

import { useActionState, useState, useTransition } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { inputClassName } from "@/components/ui/text-input";
import {
  cancelPlanAction,
  changePlanAction,
  openBillingPortalAction,
  type PlanFormState,
} from "@/features/subscriptions/actions";
import { featureLabel, formatPrice } from "@/features/subscriptions/types";
import type { MySubscription, Plan } from "@/features/subscriptions/types";

/**
 * Choosing a plan.
 *
 * Written against what the platform actually does, which the API reports rather than
 * this component assuming:
 *
 * - With billing configured, switching plan sends someone to Stripe's hosted checkout
 *   and grants nothing until payment is confirmed.
 * - Without it, the plan starts immediately and no money moves — which the screen says
 *   plainly instead of implying a charge that did not happen.
 *
 * The feature caveat stands in both worlds: none of the listed features are enforced
 * anywhere in the product yet, so they are described as what a plan *includes* and
 * flagged as not active. A page that took someone's money by describing benefits that
 * do not exist would be the single most dishonest thing in this codebase.
 */
export function PlanPicker({
  plans,
  current,
  checkout,
}: {
  plans: Plan[];
  current: MySubscription;
  /** How the browser got back here from the provider, if it did. */
  checkout?: "complete" | "cancelled";
}) {
  return (
    <div className="flex flex-col gap-8">
      {checkout ? <CheckoutOutcome outcome={checkout} current={current} /> : null}

      <FeatureNotice billingEnabled={current.billingEnabled} />

      <CurrentPlan current={current} />

      <section>
        <h2 className="font-heading text-lg font-semibold text-text-primary">
          Available plans
        </h2>

        <ul className="mt-4 grid gap-5 lg:grid-cols-2">
          {plans.map((plan) => (
            <li key={plan.code}>
              <PlanCard
                plan={plan}
                isCurrent={plan.code === current.planCode}
              />
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}

/**
 * The disclosure.
 *
 * Deliberately at the top rather than in small print at the bottom. Someone deciding
 * whether to change plan needs this before they decide, not after.
 *
 * What it says depends on whether billing is live. The feature caveat applies either
 * way; the "no payment is taken" line is only true — and only shown — when there is no
 * provider configured.
 */
/**
 * What happened at the provider.
 *
 * The success case deliberately does not claim the plan is live. This page is reached by
 * a redirect that anyone can type, and the subscription is only real once the webhook
 * lands — which may be a second or two behind the browser. So it reports what the API
 * actually says: if the plan has already switched, it says so; if not, it says the
 * payment is being confirmed rather than asserting something it cannot know.
 */
function CheckoutOutcome({
  outcome,
  current,
}: {
  outcome: "complete" | "cancelled";
  current: MySubscription;
}) {
  if (outcome === "cancelled") {
    return (
      <section className="rounded-card border border-border bg-surface p-5">
        <h2 className="font-heading text-base font-semibold text-text-primary">
          Checkout cancelled
        </h2>
        <p className="mt-1 text-sm text-text-secondary">
          Nothing was charged and your plan has not changed.
        </p>
      </section>
    );
  }

  const isLive = current.status === "Active" || current.status === "PastDue";

  return (
    <section
      className="rounded-card border border-brand-soft-border bg-brand-soft p-5"
      role="status"
    >
      <h2 className="font-heading text-base font-semibold text-brand-primary">
        {isLive ? `You are on ${current.planName}` : "Confirming your payment"}
      </h2>
      <p className="mt-1 text-sm text-text-primary">
        {isLive
          ? "Thank you. Your payment went through and your plan is active."
          : "Your payment has been taken and we are waiting on confirmation. This page will show your new plan once it arrives — refresh in a moment."}
      </p>
    </section>
  );
}

/**
 * What is and is not true about a plan right now.
 *
 * Rewritten twice as the gap closed, which is the point of keeping it here rather than
 * in a comment nobody reads. It first said no listed feature was applied anywhere; then
 * that all but insights were; now every feature works, and the only shortfall left is
 * that insights covers bookings and not search, because GoSit does not measure search
 * appearances at all. Each version was true when written, and each stopped being true
 * the moment the product moved.
 */
function FeatureNotice({ billingEnabled }: { billingEnabled: boolean }) {
  const insightsCaveat = (
    <li>
      · Everything listed below works today. Insights cover your bookings — requests,
      replies, repeat families — but not how you do in search, which we do not measure
      yet.
    </li>
  );

  return (
    <section className="rounded-card border border-brand-accent/40 bg-accent-soft p-5">
      <h2 className="font-heading text-base font-semibold text-text-primary">
        {billingEnabled ? "Before you subscribe" : "Plans are a preview"}
      </h2>
      <ul className="mt-2 flex flex-col gap-1.5 text-sm leading-relaxed text-text-primary">
        {billingEnabled ? (
          insightsCaveat
        ) : (
          <>
            <li>· Changing plan takes no payment. Billing is not connected yet.</li>
            <li>· Nothing renews and nothing is charged when a period ends.</li>
            {insightsCaveat}
          </>
        )}
      </ul>
    </section>
  );
}

function CurrentPlan({ current }: { current: MySubscription }) {
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  const canCancel = !current.cancelAtPeriodEnd && current.currentPeriodEndUtc !== null;

  return (
    <section className="rounded-card border border-border bg-surface p-6">
      <p className="font-control text-sm text-text-secondary">Your plan</p>
      <p className="mt-1 font-heading text-2xl font-bold text-text-primary">
        {current.planName}
      </p>

      <dl className="mt-4 flex flex-wrap gap-x-8 gap-y-2 text-sm">
        <div className="flex gap-2">
          <dt className="text-text-secondary">Status</dt>
          <dd className="text-text-primary">{current.status}</dd>
        </div>

        {current.currentPeriodEndUtc ? (
          <div className="flex gap-2">
            <dt className="text-text-secondary">
              {current.cancelAtPeriodEnd ? "Ends" : "Renews"}
            </dt>
            <dd className="text-text-primary">
              {new Date(current.currentPeriodEndUtc).toLocaleDateString("en-CA", {
                day: "numeric",
                month: "long",
                year: "numeric",
              })}
            </dd>
          </div>
        ) : null}

        {current.discountApplied ? (
          <div className="flex gap-2">
            <dt className="text-text-secondary">Discount applied</dt>
            <dd className="text-text-primary">
              −
              {current.discountApplied.toLocaleString("en-CA", {
                style: "currency",
                currency: "CAD",
              })}
            </dd>
          </div>
        ) : null}
      </dl>

      {current.cancelAtPeriodEnd ? (
        <p className="mt-4 rounded-control bg-surface-muted px-4 py-3 text-sm text-text-primary">
          This plan will not renew. Everything it includes stays with you until it ends.
        </p>
      ) : null}

      {error ? (
        <p role="alert" className="mt-3 text-sm text-danger-accent">
          {error}
        </p>
      ) : null}

      <div className="mt-4 flex flex-wrap gap-2">
        {canCancel ? (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            disabled={isPending}
            onClick={() =>
              startTransition(async () => {
                setError(null);

                const result = await cancelPlanAction();

                if (result.error) {
                  setError(result.error);
                }
              })
            }
          >
            {isPending ? "Cancelling…" : "Cancel renewal"}
          </Button>
        ) : null}

        {/*
          Card changes, invoices and receipts live in the provider's own portal. Building
          them here would mean handling card data and reimplementing tax receipts for no
          benefit to anyone.
        */}
        {current.hasBillingPortal ? (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            disabled={isPending}
            onClick={() =>
              startTransition(async () => {
                setError(null);

                const result = await openBillingPortalAction();

                if (result?.error) {
                  setError(result.error);
                }
              })
            }
          >
            Payment method and invoices
          </Button>
        ) : null}
      </div>
    </section>
  );
}

function PlanCard({ plan, isCurrent }: { plan: Plan; isCurrent: boolean }) {
  const [isOpen, setIsOpen] = useState(false);

  const [state, formAction] = useActionState<PlanFormState, FormData>(
    changePlanAction.bind(null, plan.code),
    {},
  );

  return (
    <article
      className={`flex h-full flex-col rounded-card border p-6 ${
        isCurrent
          ? "border-brand-primary bg-brand-soft"
          : "border-border bg-surface shadow-sm"
      }`}
    >
      <div className="flex items-start justify-between gap-3">
        <h3 className="font-heading text-xl font-bold text-text-primary">{plan.name}</h3>
        {isCurrent ? (
          <span className="rounded-pill bg-brand-primary px-2.5 py-1 text-xs font-semibold text-text-on-brand">
            Current
          </span>
        ) : null}
      </div>

      <p className="mt-2">
        <span className="font-heading text-3xl font-extrabold text-text-primary">
          {formatPrice(plan)}
        </span>
        {!plan.isFree && plan.monthlyPrice > 0 ? (
          <span className="ml-1.5 text-sm text-text-secondary">/ month</span>
        ) : null}
      </p>

      {plan.features.length > 0 ? (
        <>
          <p className="mt-5 font-control text-sm font-medium text-text-secondary">
            Includes
          </p>
          <ul className="mt-2 flex flex-col gap-2">
            {plan.features.map((feature) => (
              <li key={feature} className="flex gap-2.5 text-sm text-text-primary">
                <span aria-hidden className="mt-1.5 size-1.5 shrink-0 rounded-full bg-brand-primary" />
                {featureLabel(feature)}
              </li>
            ))}
          </ul>
        </>
      ) : (
        // The free plan declares no features. What it actually gives depends on which
        // side of the marketplace is reading — "find and book care" is nonsense to a
        // caregiver.
        <p className="mt-5 text-sm text-text-secondary">
          {plan.audience === "Caregiver"
            ? "Everything you need to take bookings and get paid."
            : "Everything you need to find and book care."}
        </p>
      )}

      <div className="mt-auto pt-6">
        {isCurrent ? (
          <p className="text-sm text-text-secondary">You are on this plan.</p>
        ) : isOpen ? (
          <form action={formAction} className="flex flex-col gap-3">
            <div>
              <label
                htmlFor={`promo-${plan.code}`}
                className="font-control text-sm text-text-secondary"
              >
                Promo code (optional)
              </label>
              <input
                id={`promo-${plan.code}`}
                name="promoCode"
                maxLength={32}
                placeholder="WELCOME10"
                className={`mt-1 ${inputClassName} uppercase`}
              />
            </div>

            {state.error ? (
              <p role="alert" className="text-sm text-danger-accent">
                {state.error}
              </p>
            ) : null}

            <div className="flex gap-2">
              <SwitchButton planName={plan.name} />
              <Button type="button" variant="subtle" onClick={() => setIsOpen(false)}>
                Cancel
              </Button>
            </div>
          </form>
        ) : (
          <Button
            type="button"
            variant={plan.isFree ? "ghost" : "primary"}
            className="w-full"
            onClick={() => setIsOpen(true)}
          >
            Switch to {plan.name}
          </Button>
        )}
      </div>
    </article>
  );
}

function SwitchButton({ planName }: { planName: string }) {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" disabled={pending}>
      {pending ? "Switching…" : `Switch to ${planName}`}
    </Button>
  );
}
