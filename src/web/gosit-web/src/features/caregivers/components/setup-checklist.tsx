import Link from "next/link";
import { buttonClassName } from "@/components/ui/button";
import { setupProgress } from "@/features/caregivers/setup-steps";
import type { SetupStep, StepKind, StepState } from "@/features/caregivers/setup-steps";

const KIND_LABEL: Record<StepKind, string> = {
  listing: "Needed to be listed",
  paid: "Needed to get paid",
  recommended: "Recommended",
};

const STATE_LABEL: Record<StepState, string> = {
  done: "Done",
  review: "With our team",
  todo: "To do",
};

/**
 * The caregiver's registration after sign-up, as a list they can work through in any order.
 *
 * A list rather than a wizard for the reason the account page is one page: someone waiting
 * on a background check should still be able to do everything else. The next unfinished step
 * is the one given a button, so the way forward is never a question.
 */
export function SetupChecklist({
  steps,
  isListable,
}: {
  steps: SetupStep[];
  isListable: boolean;
}) {
  const { done, total } = setupProgress(steps);
  const next = steps.find((s) => s.state === "todo");

  return (
    <section aria-labelledby="setup-heading" className="flex flex-col gap-6">
      <div className="rounded-card border border-border bg-surface p-5">
        <div className="flex items-baseline justify-between gap-4">
          <h2 id="setup-heading" className="font-heading text-lg font-medium text-text-primary">
            {isListable ? "You are visible to families" : "Finish setting up"}
          </h2>
          <p className="text-sm text-text-secondary">
            {done} of {total} done
          </p>
        </div>

        <div
          role="progressbar"
          aria-valuemin={0}
          aria-valuemax={total}
          aria-valuenow={done}
          aria-label="Setup progress"
          className="mt-3 h-2 overflow-hidden rounded-full bg-surface-muted"
        >
          <div
            className="h-full rounded-full bg-brand-primary"
            style={{ width: `${(done / total) * 100}%` }}
          />
        </div>

        <p className="mt-3 text-sm text-text-secondary">
          Signing up only asked for the basics. These are the details we need before families can
          find you and before you are paid. Save as you go and come back any time.
        </p>
      </div>

      <ol className="flex flex-col gap-3">
        {steps.map((step, index) => (
          <li
            key={step.id}
            className="flex flex-col gap-3 rounded-card border border-border bg-surface p-4 sm:flex-row sm:items-center sm:justify-between"
          >
            <div className="flex items-start gap-3">
              <span
                aria-hidden
                className={`mt-0.5 grid size-7 shrink-0 place-items-center rounded-full border text-xs font-semibold ${
                  step.state === "done"
                    ? "border-brand-primary bg-brand-primary text-text-on-brand"
                    : "border-border-strong text-text-secondary"
                }`}
              >
                {step.state === "done" ? "✓" : index + 1}
              </span>
              <div>
                <p className="font-heading font-medium text-text-primary">{step.title}</p>
                <p className="mt-0.5 text-sm text-text-secondary">{step.detail}</p>
                <p className="mt-1 text-xs text-text-muted">
                  {KIND_LABEL[step.kind]} · <span className="sr-only">Status: </span>
                  {STATE_LABEL[step.state]}
                </p>
              </div>
            </div>

            {step.state === "done" ? (
              <Link href={step.href} className="font-control text-sm text-brand-primary hover:underline">
                Review
              </Link>
            ) : step.state === "review" ? (
              <Link href={step.href} className="font-control text-sm text-brand-primary hover:underline">
                See status
              </Link>
            ) : (
              <Link
                href={step.href}
                className={buttonClassName(step.id === next?.id ? "primary" : "ghost", "shrink-0")}
              >
                {step.action}
              </Link>
            )}
          </li>
        ))}
      </ol>
    </section>
  );
}
