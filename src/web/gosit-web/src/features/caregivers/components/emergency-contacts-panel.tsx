import { formatPhoneNumber } from "@/features/account/phone";
import type { EmergencyContact } from "@/features/caregivers/types";

/**
 * Who to ring if the family cannot be reached.
 *
 * Placed with the care needs and built to be used one-handed in the ten seconds when
 * something is going wrong: every number is a `tel:` link, so it dials rather than asking
 * someone to memorise eleven digits and switch apps. The relationship is shown beside the
 * name because that is what decides who to try first, and whether this is a person who may
 * collect a child.
 *
 * Nothing here is hidden behind a disclosure or a click. A number that takes two taps to
 * reach is a number nobody reaches.
 */
export function EmergencyContactsPanel({ contacts }: { contacts: EmergencyContact[] }) {
  if (contacts.length === 0) {
    return null;
  }

  return (
    <section className="rounded-card border border-border bg-surface p-5">
      <h2 className="font-heading text-base font-medium text-text-primary">
        If you cannot reach the family
      </h2>
      <p className="mt-1 text-sm text-text-secondary">
        {contacts.length === 1 ? "This person" : "These people"} agreed to be called about
        this booking. Please keep {contacts.length === 1 ? "the number" : "the numbers"} to
        yourself.
      </p>

      <ul className="mt-3 flex flex-col gap-2">
        {contacts.map((contact) => (
          <li
            key={contact.id}
            className="flex flex-wrap items-center justify-between gap-3 rounded-control bg-surface-muted px-4 py-3"
          >
            <div className="min-w-0">
              <p className="text-sm text-text-primary">{contact.fullName}</p>
              <p className="text-sm text-text-secondary">{contact.relationship}</p>
            </div>

            <a
              href={`tel:${contact.phoneNumber.replace(/[^\d+]/g, "")}`}
              className="font-control text-sm font-semibold text-brand-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
            >
              {formatPhoneNumber(contact.phoneNumber)}
            </a>
          </li>
        ))}
      </ul>

      {/*
        Said plainly rather than left implied. GoSit is not an emergency service and must
        never read as one — this list is for reaching a family, not for a crisis.
      */}
      <p className="mt-3 text-xs text-text-secondary">
        In a medical emergency call 911 first.
      </p>
    </section>
  );
}
