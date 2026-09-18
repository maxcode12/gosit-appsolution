import { Avatar } from "@/components/ui/badges";

/**
 * Who a queue row is about.
 *
 * Name, email and account status together, because all three change the decision: the
 * name is who you are judging, the email is how you would follow up, and "already
 * suspended" is the thing that stops a reviewer acting twice on the same person.
 */
export function SubjectLine({
  name,
  email,
  role,
  accountStatus,
}: {
  name: string;
  email: string;
  role?: string;
  accountStatus?: string;
}) {
  return (
    <div className="flex items-center gap-3">
      <Avatar name={name} />

      <div className="min-w-0">
        <p className="flex flex-wrap items-center gap-2 font-heading font-semibold text-text-primary">
          {name}
          {role ? (
            <span className="rounded-pill bg-surface-muted px-2 py-0.5 font-control text-xs font-medium text-text-secondary">
              {role === "CareRequester" ? "Family" : role}
            </span>
          ) : null}
          {accountStatus && accountStatus !== "Active" ? (
            <span className="rounded-pill bg-danger-soft px-2 py-0.5 font-control text-xs font-semibold text-danger-accent">
              {accountStatus}
            </span>
          ) : null}
        </p>
        {email ? (
          <p className="truncate text-sm text-text-secondary">{email}</p>
        ) : null}
      </div>
    </div>
  );
}

/** "3 days ago" for a queue, where waiting time is the thing that matters. */
export function waitedFor(iso: string): string {
  const days = (Date.now() - new Date(iso).getTime()) / 86_400_000;

  if (days < 1) return "today";
  if (days < 2) return "yesterday";

  return `${Math.floor(days)} days ago`;
}
