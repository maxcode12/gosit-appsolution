"use client";

import { useState } from "react";

/**
 * Parent / Caregiver selector.
 *
 * DESIGN GAP: the Sign Up wireframe (Figma 77:12) has no account-type control, but
 * the API requires a role and the two sides of the marketplace diverge immediately
 * after signup. PLAN.md lists "account-type selection" among the screens still to be
 * designed; this is a minimal stand-in built from existing tokens, not a new pattern.
 */

const OPTIONS = [
  { value: "CareRequester", label: "I need a caregiver" },
  { value: "Caregiver", label: "I am a caregiver" },
] as const;

export function AccountTypeChoice({
  name = "role",
  defaultValue = OPTIONS[0].value,
}: {
  name?: string;
  defaultValue?: (typeof OPTIONS)[number]["value"];
}) {
  const [selected, setSelected] = useState<string>(defaultValue);

  return (
    <fieldset className="w-full">
      <legend className="mb-2 font-control text-sm text-text-secondary">
        I am signing up as
      </legend>
      <div className="flex gap-3">
        {OPTIONS.map((option) => {
          const isSelected = selected === option.value;

          return (
            <label
              key={option.value}
              className={`flex h-[3.25rem] flex-1 cursor-pointer items-center justify-center rounded-field border-2 px-3 text-center font-control text-sm transition-colors ${
                isSelected
                  ? "border-brand-primary bg-brand-primary text-text-on-brand"
                  : "border-border bg-surface text-text-primary hover:border-brand-primary"
              }`}
            >
              <input
                type="radio"
                name={name}
                value={option.value}
                checked={isSelected}
                onChange={() => setSelected(option.value)}
                className="sr-only"
              />
              {option.label}
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}
