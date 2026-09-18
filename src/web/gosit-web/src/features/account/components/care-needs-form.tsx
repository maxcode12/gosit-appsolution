"use client";

import { useActionState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import { saveCareNeedsAction, type AccountFormState } from "@/features/account/actions";
import type { CareNeeds } from "@/features/account/types";

/**
 * Recorded support needs for one person.
 *
 * Everything here is sensitive. It is shown only to the person who wrote it and, once a
 * booking is confirmed, to that one caregiver. Nothing on this form appears in a list, a
 * URL, a page title or a log line.
 *
 * The API replaces the whole record on save, so every field is posted — including the
 * blank ones, which is how something is cleared.
 */
export function CareNeedsForm({
  careRecipientId,
  firstName,
  careNeeds,
}: {
  careRecipientId: string;
  firstName: string;
  careNeeds: CareNeeds;
}) {
  const [state, formAction] = useActionState<AccountFormState, FormData>(
    saveCareNeedsAction.bind(null, careRecipientId),
    {},
  );

  return (
    <form action={formAction} className="flex flex-col gap-6" noValidate>
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

      <Section
        title="Day to day"
        description={`How ${firstName} moves, communicates and likes their day to go.`}
      >
        <Area
          name="mobilityNeeds"
          label="Mobility"
          placeholder="Uses a walker indoors. Needs a steady arm on stairs."
          defaultValue={careNeeds.mobilityNeeds}
        />
        <Area
          name="communicationStyle"
          label="Communication"
          placeholder="Uses short sentences and a picture board. Give time to answer."
          defaultValue={careNeeds.communicationStyle}
        />
        <Area
          name="sensoryNeeds"
          label="Sensory"
          placeholder="Loud noise is hard. Headphones are in the blue bag."
          defaultValue={careNeeds.sensoryNeeds}
        />
        <Area
          name="routinePreferences"
          label="Routine"
          placeholder="Snack at 3. Quiet time after school before anything else."
          defaultValue={careNeeds.routinePreferences}
        />
      </Section>

      <Section
        title="Support"
        description="Only tick what applies. A caregiver sees these once a booking is confirmed."
      >
        <Check
          name="toiletingAssistance"
          label="Needs help with toileting"
          defaultChecked={careNeeds.toiletingAssistance}
        />
        <Check
          name="feedingAssistance"
          label="Needs help with eating"
          defaultChecked={careNeeds.feedingAssistance}
        />
      </Section>

      <Section
        title="Food"
        description="Allergies are the first thing a caregiver reads, so be specific."
      >
        <Area
          name="allergies"
          label="Allergies"
          placeholder="Peanuts — carries an EpiPen in the front pocket of the school bag."
          defaultValue={careNeeds.allergies}
        />
        <Area
          name="dietaryRestrictions"
          label="Dietary needs"
          placeholder="No dairy. Soft food only."
          defaultValue={careNeeds.dietaryRestrictions}
        />
      </Section>

      <Section
        title="Difficult moments"
        description="What sets them off, and what helps. This is the part caregivers say they need most."
      >
        <Area
          name="triggers"
          label="Triggers"
          placeholder="Sudden changes of plan. Being rushed."
          defaultValue={careNeeds.triggers}
        />
        <Area
          name="calmingStrategies"
          label="What helps"
          placeholder="Dim the lights and offer the weighted blanket. Do not raise your voice."
          defaultValue={careNeeds.calmingStrategies}
        />
        <Area
          name="behaviourSupportNotes"
          label="Anything else about behaviour"
          placeholder="Wanders towards the front door when anxious — keep it locked."
          defaultValue={careNeeds.behaviourSupportNotes}
        />
      </Section>

      <Section
        title="In an emergency"
        description="Who to call and what to do first. GoSit is a non-medical service — this is information for a caregiver, not a care plan."
      >
        <Area
          name="emergencyInstructions"
          label="Emergency instructions"
          placeholder="Call me first, then Dr. Okafor's office. The health card is on the fridge."
          defaultValue={careNeeds.emergencyInstructions}
        />
      </Section>

      {/*
        The API replaces the whole record, so skills absent from this post would be
        cleared. There is no picker for them yet — it needs the skills catalogue — so
        they are carried through untouched rather than silently dropped.
      */}
      {careNeeds.requiredSkills.map((skill) => (
        <input key={skill} type="hidden" name="requiredSkills" value={skill} />
      ))}

      <SaveButton firstName={firstName} />
    </form>
  );
}

function SaveButton({ firstName }: { firstName: string }) {
  const { pending } = useFormStatus();

  return (
    <div className="flex items-center gap-3">
      <Button type="submit" disabled={pending}>
        {pending ? "Saving…" : `Save ${firstName}'s care needs`}
      </Button>
      <p className="text-sm text-text-secondary">Only you and a confirmed caregiver see this.</p>
    </div>
  );
}

function Section({
  title,
  description,
  children,
}: {
  title: string;
  description: string;
  children: React.ReactNode;
}) {
  return (
    <fieldset className="rounded-card border border-border bg-surface p-5">
      <legend className="font-heading text-base font-medium text-text-primary">{title}</legend>
      <p className="mt-1 text-sm text-text-secondary">{description}</p>
      <div className="mt-4 flex flex-col gap-4">{children}</div>
    </fieldset>
  );
}

function Area({
  name,
  label,
  placeholder,
  defaultValue,
}: {
  name: string;
  label: string;
  placeholder: string;
  defaultValue: string | null;
}) {
  return (
    <div>
      <label htmlFor={name} className="font-control text-sm text-text-secondary">
        {label}
      </label>
      <textarea
        id={name}
        name={name}
        rows={2}
        maxLength={2000}
        placeholder={placeholder}
        defaultValue={defaultValue ?? ""}
        className="mt-1 w-full rounded-control border border-border bg-surface p-3 font-control text-sm text-text-primary"
      />
    </div>
  );
}

function Check({
  name,
  label,
  defaultChecked,
}: {
  name: string;
  label: string;
  defaultChecked: boolean;
}) {
  return (
    <label className="flex cursor-pointer items-center gap-3 text-sm text-text-primary">
      <input
        type="checkbox"
        name={name}
        defaultChecked={defaultChecked}
        className="size-4 accent-[var(--color-brand-primary)]"
      />
      {label}
    </label>
  );
}
