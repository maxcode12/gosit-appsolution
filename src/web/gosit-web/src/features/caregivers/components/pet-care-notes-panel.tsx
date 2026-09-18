import { describePet, type BookingPetSummary, type PetCareNotes } from "@/features/bookings/types";

/**
 * Care notes for the pets on a booking, as disclosed to the sitter who accepted it.
 *
 * The API sends notes only once a booking has been accepted, so on a request this renders
 * nothing. There is nothing to hide here, because nothing arrived.
 *
 * Behaviour comes first, for the same reason emergency instructions lead the care needs
 * panel: "reactive to other dogs" is what matters in the first ten seconds of a walk.
 */
export function PetCareNotesPanel({ pets }: { pets: BookingPetSummary[] }) {
  const withNotes = pets.filter(
    (pet): pet is BookingPetSummary & { careNotes: PetCareNotes } => pet.careNotes !== null,
  );

  if (withNotes.length === 0) {
    return null;
  }

  return (
    <>
      {withNotes.map((pet) => (
        <section key={pet.petId} className="rounded-card border border-border bg-surface p-5">
          <h2 className="font-heading text-base font-medium text-text-primary">
            Looking after {pet.name}
          </h2>
          <p className="mt-0.5 text-sm text-text-secondary">{describePet(pet)}</p>

          <dl className="mt-3 flex flex-col gap-3 text-sm">
            {NOTE_FIELDS.map(([key, label]) => {
              const value = pet.careNotes[key];

              return value ? (
                <div key={key}>
                  <dt className="font-control text-text-secondary">{label}</dt>
                  <dd className="mt-0.5 whitespace-pre-wrap text-text-primary">{value}</dd>
                </div>
              ) : null;
            })}
          </dl>
        </section>
      ))}
    </>
  );
}

const NOTE_FIELDS: ReadonlyArray<readonly [keyof PetCareNotes, string]> = [
  ["behaviour", "Temperament"],
  ["feedingInstructions", "Feeding"],
  ["medication", "Medication"],
  ["vetClinic", "Vet"],
  ["otherNotes", "Anything else"],
];
