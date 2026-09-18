"use client";

import { useActionState, useCallback, useEffect, useState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { inputClassName } from "@/components/ui/text-input";
import {
  createPromoCodeAction,
  deactivatePromoCodeAction,
  type AdminFormState,
} from "@/features/admin/actions";
import {
  PROMO_STATUS_HINTS,
  PROMO_STATUS_LABELS,
  type AdminPromoCode,
} from "@/features/admin/types";

/**
 * The list of campaigns, and a form to add one.
 *
 * The form is collapsed by default. Someone arriving here is far more often checking
 * what a campaign has cost than starting a new one, and putting a money-giving-away form
 * at the top of the screen makes the rarer, riskier action the easiest one to trip over.
 */
export function PromoCodeManager({ promoCodes }: { promoCodes: AdminPromoCode[] }) {
  const [isCreating, setCreating] = useState(false);

  // Stable, so the effect that closes the form on success does not re-fire on every
  // render of the parent.
  const close = useCallback(() => setCreating(false), []);

  return (
    <div className="flex flex-col gap-8">
      <section>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="font-heading text-lg font-semibold text-text-primary">
            Campaigns
          </h2>

          <Button type="button" variant="ghost" onClick={() => setCreating((open) => !open)}>
            {isCreating ? "Cancel" : "New code"}
          </Button>
        </div>

        {isCreating ? <CreateForm onCreated={close} /> : null}
      </section>

      {promoCodes.length === 0 ? (
        <p className="rounded-card border border-dashed border-border-strong px-6 py-10 text-center text-sm text-text-secondary">
          No promo codes yet. Anything created here is accepted at checkout straight away.
        </p>
      ) : (
        <ul className="flex flex-col gap-3">
          {promoCodes.map((promo) => (
            <PromoRow key={promo.id} promo={promo} />
          ))}
        </ul>
      )}
    </div>
  );
}

function PromoRow({ promo }: { promo: AdminPromoCode }) {
  const [state, formAction] = useActionState<AdminFormState, FormData>(
    deactivatePromoCodeAction,
    {},
  );

  const discount =
    promo.discountType === "Percentage"
      ? `${promo.value}% off`
      : `${promo.value.toLocaleString("en-CA", { style: "currency", currency: "CAD" })} off`;

  return (
    <li className="rounded-card border border-border bg-surface p-5">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-3">
            <code className="rounded-control bg-surface-muted px-2.5 py-1 font-heading text-base font-bold tracking-widest text-text-primary">
              {promo.code}
            </code>
            <StatusBadge status={promo.status} />
          </div>

          <p className="mt-2 text-sm text-text-primary">
            {discount}
            {promo.audience === null
              ? ""
              : promo.audience === "Caregiver"
                ? " · caregivers only"
                : " · families only"}
          </p>

          <p className="mt-1 text-sm text-text-secondary">{promo.description}</p>

          <p className="mt-2 text-xs text-text-muted">
            <Budget promo={promo} /> · {formatWindow(promo)}
          </p>
        </div>

        {/*
          Only offered while the code is still on. A switched-off code shows nothing —
          there is no way back, and a disabled button would invite the click anyway.
        */}
        {promo.isActive ? (
          <form action={formAction} className="shrink-0">
            <input type="hidden" name="promoCodeId" value={promo.id} />
            <DeactivateButton code={promo.code} />
          </form>
        ) : null}
      </div>

      {state.error ? (
        <p role="alert" className="mt-3 text-sm text-danger-accent">
          {state.error}
        </p>
      ) : null}
    </li>
  );
}

/**
 * How much of the campaign is left — the question someone usually arrives with.
 *
 * An uncapped code says so rather than showing a redemption count against nothing,
 * because "412 redeemed" with no denominator reads as a number that ought to have a
 * limit and does not.
 */
function Budget({ promo }: { promo: AdminPromoCode }) {
  if (promo.maxRedemptions === null) {
    return (
      <>
        {promo.redemptionCount} redeemed · no cap
      </>
    );
  }

  return (
    <>
      {promo.redemptionCount} of {promo.maxRedemptions} redeemed
    </>
  );
}

function formatWindow(promo: AdminPromoCode): string {
  const date = (value: string) =>
    new Date(value).toLocaleDateString("en-CA", {
      day: "numeric",
      month: "short",
      year: "numeric",
    });

  return promo.validUntilUtc === null
    ? `from ${date(promo.validFromUtc)}, no end date`
    : `${date(promo.validFromUtc)} – ${date(promo.validUntilUtc)}`;
}

function StatusBadge({ status }: { status: AdminPromoCode["status"] }) {
  const live = status === "Live";

  return (
    <span
      title={PROMO_STATUS_HINTS[status]}
      className={`rounded-pill px-2.5 py-0.5 font-control text-xs font-semibold ${
        live
          ? "bg-success-soft text-success-accent"
          : "bg-surface-muted text-text-secondary"
      }`}
    >
      {PROMO_STATUS_LABELS[status]}
    </span>
  );
}

function DeactivateButton({ code }: { code: string }) {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" variant="ghost" disabled={pending}>
      {pending ? "Switching off…" : `Switch off ${code}`}
    </Button>
  );
}

/**
 * The form.
 *
 * Nothing is validated here beyond what the browser does for free. Every rule about what
 * makes a coherent offer — a percentage over 100, an end before a start, a cap of zero —
 * lives on the server, and a second copy would be a second place for it to drift. What
 * comes back is the API's own message, which is written to be read.
 */
function CreateForm({ onCreated }: { onCreated: () => void }) {
  const [state, formAction] = useActionState<AdminFormState, FormData>(
    createPromoCodeAction,
    {},
  );

  // In an effect, not in the render body: calling the parent's setter while rendering
  // this component is a React error, and the version that "works" only does so until a
  // concurrent render replays it.
  useEffect(() => {
    if (state.done) {
      onCreated();
    }
  }, [state.done, onCreated]);

  return (
    <form action={formAction} className="mt-4 flex flex-col gap-4 rounded-card border border-border bg-surface p-5">
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Code" hint="Letters and numbers. Case does not matter.">
          <input
            name="code"
            required
            maxLength={32}
            placeholder="SPRING25"
            className={`${inputClassName} uppercase tracking-widest`}
          />
        </Field>

        <Field label="What it is for" hint="Internal only. Nobody outside staff sees this.">
          <input
            name="description"
            required
            maxLength={200}
            placeholder="Spring newsletter"
            className={inputClassName}
          />
        </Field>

        <Field label="Discount">
          {/* Values are the DiscountType enum's own numbers, so the two cannot drift. */}
          <select name="discountType" defaultValue="1" className={inputClassName}>
            <option value="1">Percentage off</option>
            <option value="2">Amount off (CAD)</option>
          </select>
        </Field>

        <Field label="Amount" hint="25 means 25% or $25, depending on the type.">
          <input
            name="value"
            type="number"
            required
            min="0.01"
            step="0.01"
            placeholder="25"
            className={inputClassName}
          />
        </Field>

        <Field label="Starts" hint="Leave empty to start straight away.">
          <input name="validFrom" type="date" className={inputClassName} />
        </Field>

        <Field label="Ends" hint="Leave empty for no end date.">
          <input name="validUntil" type="date" className={inputClassName} />
        </Field>

        <Field
          label="Redemption cap"
          hint="Leave empty for unlimited. This is the budget."
        >
          <input
            name="maxRedemptions"
            type="number"
            min="1"
            step="1"
            placeholder="500"
            className={inputClassName}
          />
        </Field>

        <Field label="Who can use it">
          <select name="audience" defaultValue="" className={inputClassName}>
            <option value="">Anyone</option>
            <option value="CareRequester">Families only</option>
            <option value="Caregiver">Caregivers only</option>
          </select>
        </Field>
      </div>

      {/*
        Said before the button, not after. A code cannot be edited once it exists, and
        someone should know that while they still have the form in front of them.
      */}
      <p className="text-sm text-text-secondary">
        A code cannot be edited after it is created — whoever holds it has the offer as
        written. To change the terms, switch this one off and create another.
      </p>

      {state.error ? (
        <p role="alert" className="text-sm text-danger-accent">
          {state.error}
        </p>
      ) : null}

      <CreateButton />
    </form>
  );
}

function CreateButton() {
  const { pending } = useFormStatus();

  return (
    <div>
      <Button type="submit" disabled={pending}>
        {pending ? "Creating…" : "Create code"}
      </Button>
    </div>
  );
}

function Field({
  label,
  hint,
  children,
}: {
  label: string;
  hint?: string;
  children: React.ReactNode;
}) {
  return (
    <label className="flex flex-col gap-1.5">
      <span className="font-control text-sm font-medium text-text-primary">{label}</span>
      {children}
      {hint ? <span className="text-xs text-text-muted">{hint}</span> : null}
    </label>
  );
}
