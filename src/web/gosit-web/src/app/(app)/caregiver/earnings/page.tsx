import type { Metadata } from "next";
import Link from "next/link";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { buttonClassName } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { formatMoney } from "@/features/bookings/components/booking-status";
import { getEarnings, getPayoutAccount } from "@/features/caregivers/api";
import { PayoutSetup } from "@/features/caregivers/components/payout-setup";
import { caregiverNav } from "@/features/caregivers/nav";
import type { StatementLine } from "@/features/caregivers/types";

export const metadata: Metadata = {
  title: "Earnings",
  description: "What you have earned and what is still to come.",
};

/**
 * Earnings and statement — wireframes `148:31` and `148:59`.
 *
 * The wireframes split these into an earnings screen and a wallet screen. They are one
 * page here: the two questions are "what am I owed" and "where did that number come
 * from", and putting the answer one tap away from the question helps nobody.
 *
 * Every line shows gross, fee and net together. Someone should never have to work out
 * what was deducted, and a platform that hides its own cut does not deserve trust.
 */
export default async function CaregiverEarningsPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const [earnings, payoutAccount, params] = await Promise.all([
    getEarnings(),
    getPayoutAccount(),
    searchParams,
  ]);

  // Set by the provider redirect. Not trusted for anything — it only decides which
  // message to show, and the panel itself reads real state from the API.
  const raw = Array.isArray(params.payouts) ? params.payouts[0] : params.payouts;
  const outcome = raw === "complete" || raw === "refresh" ? raw : undefined;

  if (!earnings) {
    return (
      <DashboardShell items={caregiverNav} currentPath="/caregiver/earnings" heading="Earnings">
        <div className="max-w-2xl">
          <EmptyState
            title="Nothing to show yet"
            description="Set up your profile and take your first booking. Everything you earn will be itemised here."
            action={
              <Link href="/caregiver/account" className={buttonClassName()}>
                Set up your profile
              </Link>
            }
          />
        </div>
      </DashboardShell>
    );
  }

  return (
    <DashboardShell
      items={caregiverNav}
      currentPath="/caregiver/earnings"
      heading="Earnings"
      description={`All amounts in ${earnings.currency}.`}
    >
      <div className="flex max-w-3xl flex-col gap-8">
        {/*
          First, because it is the thing standing between someone and their money.
        */}
        {payoutAccount ? (
          <PayoutSetup account={payoutAccount} outcome={outcome} />
        ) : null}

        <dl className="grid gap-4 sm:grid-cols-2">
          <Stat
            label="Not yet sent"
            value={formatMoney(earnings.pendingBalance)}
            hint="Earned and owed to you, not yet transferred."
            emphasis
          />
          <Stat
            label="Paid out so far"
            value={formatMoney(earnings.totalPaid)}
            hint="Settled and in your account."
          />
        </dl>

        <section>
          <h2 className="font-heading text-lg font-medium text-text-primary">Lifetime</h2>

          <dl className="mt-4 flex flex-col gap-2 rounded-card border border-border bg-surface p-5 text-sm">
            <div className="flex justify-between">
              <dt className="text-text-secondary">Billed to families</dt>
              <dd className="text-text-primary">{formatMoney(earnings.lifetimeGross)}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-text-secondary">GoSit&apos;s fee</dt>
              <dd className="text-text-primary">
                −{formatMoney(earnings.lifetimePlatformFees)}
              </dd>
            </div>
            <div className="flex justify-between border-t border-border pt-2">
              <dt className="font-control text-text-primary">Yours</dt>
              <dd className="font-heading text-lg font-medium text-text-primary">
                {formatMoney(earnings.lifetimeGross - earnings.lifetimePlatformFees)}
              </dd>
            </div>
          </dl>
        </section>

        <section>
          <h2 className="font-heading text-lg font-medium text-text-primary">Statement</h2>
          <p className="mt-1 text-sm text-text-secondary">
            One line per job, showing exactly what was deducted and why.
          </p>

          <div className="mt-4">
            {earnings.statement.length === 0 ? (
              <EmptyState
                title="No jobs yet"
                description="Once you have completed a booking, it appears here with the full breakdown."
              />
            ) : (
              <Statement lines={earnings.statement} />
            )}
          </div>
        </section>
      </div>
    </DashboardShell>
  );
}

/**
 * The statement.
 *
 * A table on a wide screen, stacked cards on a narrow one — a five-column money table
 * squeezed onto a phone is unreadable, and this is the screen someone is most likely to
 * check on a phone.
 */
function Statement({ lines }: { lines: StatementLine[] }) {
  return (
    <ul className="flex flex-col gap-3">
      {lines.map((line) => (
        <li
          key={line.payoutId}
          className="rounded-card border border-border bg-surface p-4"
        >
          <div className="flex flex-wrap items-baseline justify-between gap-3">
            <div>
              <p className="font-heading text-base font-medium text-text-primary">
                {formatMoney(line.net)}
                <span className="ml-2 font-control text-sm font-normal text-text-secondary">
                  to you
                </span>
              </p>
              <p className="mt-0.5 text-sm text-text-secondary">
                <Link
                  href={`/caregiver/bookings/${line.careBookingId}`}
                  className="hover:underline"
                >
                  View the job
                </Link>
              </p>
            </div>

            <div className="text-right">
              <PayoutStatus status={line.status} paidAtUtc={line.paidAtUtc} />
              <p className="mt-0.5 text-xs text-text-secondary">
                <time dateTime={line.createdAtUtc}>
                  {new Date(line.createdAtUtc).toLocaleDateString("en-CA", {
                    day: "numeric",
                    month: "short",
                    year: "numeric",
                  })}
                </time>
              </p>
            </div>
          </div>

          <dl className="mt-3 flex flex-wrap gap-x-6 gap-y-1 border-t border-border pt-3 text-sm">
            <div className="flex gap-2">
              <dt className="text-text-secondary">Billed</dt>
              <dd className="text-text-primary">{formatMoney(line.gross)}</dd>
            </div>
            <div className="flex gap-2">
              <dt className="text-text-secondary">
                Fee ({(line.commissionRate * 100).toFixed(0)}%)
              </dt>
              <dd className="text-text-primary">−{formatMoney(line.platformFee)}</dd>
            </div>
          </dl>

          {/*
            Why money that has been earned has not arrived.

            A payout waits out the family's dispute window before it moves — before that,
            a dispute reached us after the money had gone. A balance sitting still with no
            explanation reads as a bug, and it reads that way to the person who has
            already done the work.
          */}
          {availability(line) ? (
            <p className="mt-2 text-sm text-text-secondary">{availability(line)}</p>
          ) : null}
        </li>
      ))}
    </ul>
  );
}

/** Null once the money is on its way or gone. */
function availability(line: StatementLine): string | null {
  if (line.status === "Paid" || line.status === "InTransit") {
    return null;
  }

  if (line.isHeldForDispute) {
    return "On hold while the family's dispute is looked at.";
  }

  const available = new Date(line.availableAtUtc);

  if (available <= new Date()) {
    return "Ready to be sent.";
  }

  return `Available ${available.toLocaleDateString("en-CA", {
    day: "numeric",
    month: "short",
  })}, once the booking can no longer be disputed.`;
}

function PayoutStatus({ status, paidAtUtc }: { status: string; paidAtUtc: string | null }) {
  const isPaid = paidAtUtc !== null;

  return (
    <span
      className={`inline-flex items-center rounded-pill px-3 py-1 text-xs font-semibold ${
        isPaid
          ? "bg-brand-primary/10 text-brand-primary"
          : "bg-brand-accent/15 text-text-primary"
      }`}
    >
      {isPaid ? "Paid" : status}
    </span>
  );
}

function Stat({
  label,
  value,
  hint,
  emphasis = false,
}: {
  label: string;
  value: string;
  hint: string;
  emphasis?: boolean;
}) {
  return (
    <div
      className={`rounded-card border p-5 ${
        emphasis ? "border-brand-primary/30 bg-brand-primary/5" : "border-border bg-surface"
      }`}
    >
      <dt className="text-sm text-text-secondary">{label}</dt>
      <dd className="mt-1 font-heading text-2xl font-medium text-text-primary">{value}</dd>
      <p className="mt-1 text-sm text-text-secondary">{hint}</p>
    </div>
  );
}
