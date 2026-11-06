"use client";

import { useActionState, useEffect, useRef } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { inputClassName } from "@/components/ui/text-input";
import { inviteStaffAction, type AdminFormState } from "@/features/admin/actions";
import type { StaffMember } from "@/features/admin/types";

/**
 * Who is on the team, and a form to add someone.
 *
 * Adding a person emails them a link to choose their own password: nobody types, sees or
 * sends anyone else's password here. They start as ordinary staff, who can work the queues
 * but cannot add people themselves.
 */
export function StaffManager({ staff }: { staff: StaffMember[] }) {
  const [state, formAction] = useActionState<AdminFormState, FormData>(inviteStaffAction, {});
  const formRef = useRef<HTMLFormElement>(null);

  // A new person added: clear the form so the same details are not sent twice by habit.
  useEffect(() => {
    if (state.done) {
      formRef.current?.reset();
    }
  }, [state]);

  return (
    <div className="flex flex-col gap-8">
      <form
        ref={formRef}
        action={formAction}
        className="flex flex-col gap-4 rounded-card border border-border bg-surface p-5"
      >
        <div>
          <h2 className="font-heading text-lg font-medium text-text-primary">Add a staff member</h2>
          <p className="mt-1 text-sm text-text-secondary">
            They get an email with a link to choose their own password, then sign in to this site.
            New staff can review checks, documents and reports, but cannot add other people.
          </p>
        </div>

        {state.error ? (
          <p role="alert" className="rounded-control bg-red-50 px-4 py-3 text-sm text-red-800">
            {state.error}
          </p>
        ) : null}

        {state.done ? (
          <p role="status" className="rounded-control bg-brand-primary/10 px-4 py-3 text-sm text-brand-primary">
            Added. They have been emailed a link to set their password.
          </p>
        ) : null}

        <div className="grid gap-4 sm:grid-cols-2">
          <label className="font-control text-sm text-text-secondary">
            First name
            <input name="firstName" required maxLength={100} autoComplete="off" className={`mt-1 ${inputClassName}`} />
          </label>
          <label className="font-control text-sm text-text-secondary">
            Last name
            <input name="lastName" required maxLength={100} autoComplete="off" className={`mt-1 ${inputClassName}`} />
          </label>
        </div>

        <label className="font-control text-sm text-text-secondary">
          Work email address
          <input
            name="email"
            type="email"
            required
            maxLength={320}
            autoComplete="off"
            className={`mt-1 ${inputClassName}`}
          />
        </label>

        <div>
          <SubmitButton />
        </div>
      </form>

      <section aria-labelledby="team-heading">
        <h2 id="team-heading" className="font-heading text-lg font-medium text-text-primary">
          Team
        </h2>

        <ul className="mt-3 flex flex-col gap-3">
          {staff.map((member) => (
            <li
              key={member.id}
              className="flex flex-wrap items-center justify-between gap-3 rounded-card border border-border bg-surface p-4"
            >
              <div className="min-w-0">
                <p className="font-heading font-medium text-text-primary">
                  {member.firstName} {member.lastName}
                </p>
                <p className="mt-0.5 break-all text-sm text-text-secondary">{member.email}</p>
              </div>

              <div className="flex items-center gap-2">
                {member.status !== "Active" ? (
                  <span className="rounded-pill bg-danger-accent/10 px-3 py-1 text-xs font-semibold text-danger-accent">
                    {member.status}
                  </span>
                ) : null}
                <span className="rounded-pill bg-brand-soft px-3 py-1 text-xs font-semibold text-brand-primary">
                  {member.isSuperAdmin ? "Super administrator" : "Staff"}
                </span>
              </div>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" disabled={pending}>
      {pending ? "Adding…" : "Add and email them"}
    </Button>
  );
}
