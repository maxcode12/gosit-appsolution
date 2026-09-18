import { Chip, humanizeCode } from "@/components/ui/badges";
import type { BookingCareNeeds } from "@/features/caregivers/types";

/**
 * The care needs of one person, as shown to the caregiver looking after them.
 *
 * This is the most sensitive thing the product ever puts on a screen, and it earns its
 * place: someone is about to spend hours with a child or an older adult, and this is
 * what they need to do that well. The ordering is not the storage order — allergies and
 * emergency instructions come first, because those are what matter in the ten seconds
 * when something is going wrong.
 *
 * Empty fields are omitted rather than shown blank. A wall of "None recorded" buries the
 * three lines that were filled in.
 */
export function CareNeedsPanel({ record }: { record: BookingCareNeeds }) {
  const needs = record.careNeeds;

  const urgent = [
    { label: "Allergies", value: needs.allergies },
    { label: "In an emergency", value: needs.emergencyInstructions },
  ].filter((item) => item.value);

  const everyday = [
    { label: "Dietary needs", value: needs.dietaryRestrictions },
    { label: "Mobility", value: needs.mobilityNeeds },
    { label: "Communication", value: needs.communicationStyle },
    { label: "Sensory", value: needs.sensoryNeeds },
    { label: "Routine", value: needs.routinePreferences },
  ].filter((item) => item.value);

  const difficult = [
    { label: "Triggers", value: needs.triggers },
    { label: "What helps", value: needs.calmingStrategies },
    { label: "Behaviour", value: needs.behaviourSupportNotes },
  ].filter((item) => item.value);

  const support = [
    needs.toiletingAssistance ? "Needs help with toileting" : null,
    needs.feedingAssistance ? "Needs help with eating" : null,
  ].filter((item): item is string => item !== null);

  const isEmpty =
    urgent.length === 0
    && everyday.length === 0
    && difficult.length === 0
    && support.length === 0
    && needs.requiredSkills.length === 0;

  return (
    <section className="rounded-card border border-border bg-surface p-5">
      <h2 className="font-heading text-base font-medium text-text-primary">
        Caring for {record.firstName}
      </h2>
      <p className="mt-1 text-sm text-text-secondary">
        {record.age} · {record.recipientType}. Shared with you because you accepted this
        booking. Please keep it to yourself.
      </p>

      {isEmpty ? (
        <p className="mt-4 text-sm text-text-secondary">
          Nothing recorded. If there is something you should know, ask the family before
          the day.
        </p>
      ) : (
        <div className="mt-4 flex flex-col gap-5">
          {urgent.length > 0 ? (
            <div className="rounded-card bg-danger-accent/5 p-4">
              <dl className="flex flex-col gap-3">
                {urgent.map((item) => (
                  <Detail key={item.label} label={item.label} value={item.value!} emphasis />
                ))}
              </dl>
            </div>
          ) : null}

          {support.length > 0 ? (
            <ul className="flex flex-wrap gap-2">
              {support.map((item) => (
                <li key={item}>
                  <Chip>{item}</Chip>
                </li>
              ))}
            </ul>
          ) : null}

          {everyday.length > 0 ? (
            <Group title="Day to day" items={everyday} />
          ) : null}

          {difficult.length > 0 ? (
            <Group title="Difficult moments" items={difficult} />
          ) : null}

          {needs.requiredSkills.length > 0 ? (
            <div>
              <h3 className="font-control text-sm text-text-secondary">Skills the family asked for</h3>
              <ul className="mt-2 flex flex-wrap gap-2">
                {needs.requiredSkills.map((skill) => (
                  <li key={skill}>
                    <Chip>{humanizeCode(skill)}</Chip>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </div>
      )}
    </section>
  );
}

function Group({
  title,
  items,
}: {
  title: string;
  items: { label: string; value: string | null }[];
}) {
  return (
    <div className="border-t border-border pt-4">
      <h3 className="font-control text-sm text-text-secondary">{title}</h3>
      <dl className="mt-2 flex flex-col gap-3">
        {items.map((item) => (
          <Detail key={item.label} label={item.label} value={item.value!} />
        ))}
      </dl>
    </div>
  );
}

function Detail({
  label,
  value,
  emphasis = false,
}: {
  label: string;
  value: string;
  emphasis?: boolean;
}) {
  return (
    <div>
      <dt
        className={`text-sm ${
          emphasis ? "font-semibold text-danger-accent" : "text-text-secondary"
        }`}
      >
        {label}
      </dt>
      <dd className="mt-0.5 whitespace-pre-wrap text-sm text-text-primary">{value}</dd>
    </div>
  );
}
