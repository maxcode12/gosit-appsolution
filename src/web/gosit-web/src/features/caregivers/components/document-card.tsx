import type { ReactNode } from "react";
import { buttonClassName } from "@/components/ui/button";
import {
  daysUntil,
  documentAction,
  needsAttention,
  type DocumentItem,
  type DocumentState,
} from "@/features/caregivers/documents";

const CHIP: Record<DocumentState, { label: string; className: string }> = {
  missing: { label: "Needed", className: "bg-danger-accent/10 text-danger-accent" },
  review: { label: "In review", className: "bg-brand-accent/15 text-text-primary" },
  valid: { label: "Approved", className: "bg-brand-primary/10 text-brand-primary" },
  expiring: { label: "Expires soon", className: "bg-brand-accent/15 text-text-primary" },
  expired: { label: "Expired", className: "bg-danger-accent/10 text-danger-accent" },
  rejected: { label: "Not accepted", className: "bg-danger-accent/10 text-danger-accent" },
};

/**
 * One document: what it is, where it stands, and the way to upload or renew it.
 *
 * The upload itself is the existing panel for that document, passed in as `children` and
 * shown when the card is opened — so there is exactly one way to submit each kind, and this
 * page only organises them. Without children the card is a plain row that points elsewhere.
 */
export function DocumentCard({
  item,
  children,
  defaultOpen = false,
  linkTo,
}: {
  item: DocumentItem;
  children?: ReactNode;
  defaultOpen?: boolean;
  /** For a row with no panel of its own: the card that holds the upload. */
  linkTo?: string;
}) {
  // Nothing is wrong with an optional document that has not been added.
  const chip =
    !item.required && item.state === "missing"
      ? { label: "Not added", className: "bg-surface-muted text-text-secondary" }
      : CHIP[item.state];
  const urgent = item.required && needsAttention(item.state);

  const summary = (
    <>
      <div className="min-w-0">
        <p className="font-heading font-medium text-text-primary">
          {item.title}
          <span className="ml-2 font-control text-xs font-normal text-text-secondary">
            {item.required ? "Required" : "Optional"}
          </span>
        </p>
        <p className="mt-0.5 text-sm text-text-secondary">{subtitle(item)}</p>
      </div>

      <div className="flex shrink-0 items-center gap-3">
        <span
          className={`inline-flex items-center rounded-pill px-3 py-1 text-xs font-semibold ${chip.className}`}
        >
          {chip.label}
        </span>
        <span className={buttonClassName(urgent ? "primary" : "ghost", "pointer-events-none", "sm")}>
          {documentAction(item)}
        </span>
      </div>
    </>
  );

  if (!children) {
    return (
      <a
        id={item.id}
        href={linkTo ?? "#certificates"}
        className="flex scroll-mt-24 flex-wrap items-center justify-between gap-3 rounded-card border border-border bg-surface p-4"
      >
        {summary}
      </a>
    );
  }

  return (
    <details
      id={item.id}
      open={defaultOpen}
      className="scroll-mt-24 rounded-card border border-border bg-surface"
    >
      <summary className="flex cursor-pointer list-none flex-wrap items-center justify-between gap-3 p-4 [&::-webkit-details-marker]:hidden">
        {summary}
      </summary>
      <div className="border-t border-border p-4">{children}</div>
    </details>
  );
}

function subtitle(item: DocumentItem): string {
  switch (item.state) {
    case "review":
      return "With our team. We will email you when it is done.";
    case "rejected":
      return item.note ?? "Our team could not accept this. Upload a new copy.";
    case "expired":
      return item.note ?? (item.expiresOn ? `Expired ${formatDate(item.expiresOn)}.` : "No longer in date.");
    case "expiring": {
      const days = item.expiresOn ? daysUntil(item.expiresOn) : 0;

      return `Expires ${days <= 0 ? "today" : days === 1 ? "tomorrow" : `in ${days} days`} (${formatDate(item.expiresOn)}).`;
    }
    case "valid":
      return item.note ?? (item.expiresOn ? `Expires ${formatDate(item.expiresOn)}.` : "Up to date.");
    default:
      return item.detail;
  }
}

function formatDate(isoDate: string | null): string {
  if (!isoDate) return "";

  const [year, month, day] = isoDate.slice(0, 10).split("-").map(Number);

  return new Date(year, month - 1, day).toLocaleDateString("en-CA", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}
