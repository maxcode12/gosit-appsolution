"use client";

import { useState } from "react";
import { HeartIcon } from "@/components/ui/icons";
import { HomeIcon } from "@/components/ui/marketing-icons";

/**
 * Family / Caregiver selector.
 *
 * DESIGN GAP: the Sign Up wireframe (Figma 77:12) has no account-type control, but
 * the API requires a role and the two sides of the marketplace diverge immediately
 * after signup. PLAN.md lists "account-type selection" among the screens still to be
 * designed; this is a stand-in built from existing tokens, not a new pattern.
 *
 * Two cards rather than a toggle, because this is the one choice on the form that
 * changes what happens next, so each side says in a line what it means. The labels are
 * kept as the radios' accessible names.
 */

const OPTIONS = [
  {
    value: "CareRequester",
    label: "I need a caregiver",
    hint: "Find care for my family",
    icon: <HomeIcon className="size-5" />,
  },
  {
    value: "Caregiver",
    label: "I am a caregiver",
    hint: "Offer my services",
    icon: <HeartIcon />,
  },
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
      <legend className="mb-2 font-control text-sm font-medium text-text-secondary">
        I am signing up as
      </legend>
      <div className="grid grid-cols-2 gap-3">
        {OPTIONS.map((option) => {
          const isSelected = selected === option.value;

          return (
            <label
              key={option.value}
              className={`relative flex cursor-pointer flex-col gap-2 rounded-2xl border-2 p-3.5 transition-all duration-200 focus-within:ring-4 focus-within:ring-brand-primary/15 ${
                isSelected
                  ? "border-brand-primary bg-brand-soft shadow-sm"
                  : "border-border bg-surface hover:border-border-strong"
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
              <span
                className={`grid size-9 place-items-center rounded-xl transition-colors ${
                  isSelected ? "bg-brand-primary text-text-on-brand" : "bg-surface-muted text-text-secondary"
                }`}
              >
                {option.icon}
              </span>
              <span className="font-heading text-sm font-bold leading-tight text-text-primary">
                {option.label}
              </span>
              <span className="text-xs leading-snug text-text-secondary">{option.hint}</span>
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}
