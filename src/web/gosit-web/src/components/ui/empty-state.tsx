import type { ReactNode } from "react";

/**
 * Shown when a list has nothing in it.
 *
 * Always says what to do next rather than only that something is empty — an empty
 * search with no suggestion reads as a dead end.
 *
 * The dashed border is doing real work: it reads as "a space waiting to be filled"
 * rather than "a card that failed to load", which is how a solid-bordered empty card
 * tends to look.
 */
export function EmptyState({
  title,
  description,
  action,
  icon,
}: {
  title: string;
  description: string;
  action?: ReactNode;
  /** Optional glyph. Decorative — the title carries the meaning. */
  icon?: ReactNode;
}) {
  return (
    <div className="rounded-card border border-dashed border-border-strong bg-surface/60 px-6 py-14 text-center">
      {icon ? (
        <span
          aria-hidden
          className="mx-auto mb-4 grid size-12 place-items-center rounded-full bg-surface-muted text-text-muted"
        >
          {icon}
        </span>
      ) : null}

      <h2 className="font-heading text-lg font-semibold text-text-primary">{title}</h2>
      <p className="mx-auto mt-2 max-w-sm text-sm leading-relaxed text-text-secondary">
        {description}
      </p>
      {action ? <div className="mt-6 flex justify-center">{action}</div> : null}
    </div>
  );
}
