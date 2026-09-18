"use client";

import { useActionState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { setServicesAction, type CaregiverFormState } from "@/features/caregivers/actions";
import type { CareServiceCategory } from "@/features/search/types";

/**
 * The services this caregiver offers.
 *
 * Each service also demands certifications, and the API refuses a booking when those do
 * not line up. Rather than hide the ones that cannot be booked yet, they stay selectable
 * and say what is missing — a caregiver deciding whether to get their First Aid renewed
 * needs to see what it would unlock.
 */
export function ServicesForm({
  categories,
  selected,
  heldCertifications,
}: {
  categories: CareServiceCategory[];
  selected: string[];
  /** Codes the caregiver validly holds today, from their own profile. */
  heldCertifications: string[];
}) {
  const [state, formAction] = useActionState<CaregiverFormState, FormData>(
    setServicesAction,
    {},
  );

  const chosen = new Set(selected);
  const held = new Set(heldCertifications);

  return (
    <form action={formAction} className="flex flex-col gap-5">
      {state.error ? (
        <p role="alert" className="rounded-control bg-red-50 px-4 py-3 text-sm text-red-800">
          {state.error}
        </p>
      ) : null}

      {state.saved ? (
        <p
          role="status"
          className="rounded-control bg-brand-primary/10 px-4 py-3 text-sm text-brand-primary"
        >
          Saved.
        </p>
      ) : null}

      {categories.map((category) => (
        <fieldset key={category.code}>
          <legend className="font-control text-sm text-text-secondary">{category.name}</legend>

          <ul className="mt-2 flex flex-col gap-2">
            {category.services.map((service) => {
              const missing = service.requiredCertificationCodes.filter(
                (code) => !held.has(code),
              );

              return (
                <li key={service.code}>
                  <label className="flex cursor-pointer items-start gap-3 rounded-control border border-border p-3 has-[:checked]:border-brand-primary has-[:checked]:bg-brand-primary/5">
                    <input
                      type="checkbox"
                      name="serviceCodes"
                      value={service.code}
                      defaultChecked={chosen.has(service.code)}
                      className="mt-0.5 size-4 accent-[var(--color-brand-primary)]"
                    />
                    <span className="min-w-0">
                      <span className="text-text-primary">{service.name}</span>
                      {missing.length > 0 ? (
                        <span className="mt-0.5 block text-sm text-text-secondary">
                          Needs {missing.map(humanize).join(" and ")} before families can
                          book it.
                        </span>
                      ) : null}
                      {service.pricingUnit === "Nightly" ? (
                        <span className="mt-0.5 block text-sm text-text-secondary">
                          Priced per night, from the nightly rate on your profile.
                        </span>
                      ) : null}
                      {service.careLocation === "CaregiverHome" ? (
                        // Said before the box is ticked rather than after the first booking:
                        // this is the one service that shows a family where the caregiver lives.
                        <span className="mt-0.5 block text-sm text-text-secondary">
                          At your home. Families you accept for boarding see your home address.
                        </span>
                      ) : null}
                    </span>
                  </label>
                </li>
              );
            })}
          </ul>
        </fieldset>
      ))}

      <SaveButton />
    </form>
  );
}

function SaveButton() {
  const { pending } = useFormStatus();

  return (
    <div>
      <Button type="submit" disabled={pending}>
        {pending ? "Saving…" : "Save services"}
      </Button>
    </div>
  );
}

/** "VULNERABLE_SECTOR" to "a vulnerable sector check", in a sentence. */
function humanize(code: string): string {
  const words = code.toLowerCase().replace(/_/g, " ");

  return words === "vulnerable sector"
    ? "a vulnerable sector check"
    : `${words === "cpr" ? "CPR" : words}`;
}
