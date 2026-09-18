import type { AddressDto } from "@/features/bookings/address";

/**
 * Account contracts.
 *
 * A care recipient appears in two shapes on purpose. The summary is what a profile
 * listing returns; the full record — the only thing that carries CareNeeds — is fetched
 * one recipient at a time, so sensitive detail is never loaded incidentally.
 */

export type CareRecipientType = "Child" | "Adult" | "Senior";

export const CARE_RECIPIENT_TYPES: CareRecipientType[] = ["Child", "Adult", "Senior"];

/**
 * Recorded support needs for one recipient.
 *
 * Never shown in a list, never logged, and disclosed to a caregiver only once a booking
 * exists.
 */
export interface CareNeeds {
  mobilityNeeds: string | null;
  communicationStyle: string | null;
  sensoryNeeds: string | null;
  behaviourSupportNotes: string | null;
  dietaryRestrictions: string | null;
  allergies: string | null;
  toiletingAssistance: boolean;
  feedingAssistance: boolean;
  routinePreferences: string | null;
  triggers: string | null;
  calmingStrategies: string | null;
  emergencyInstructions: string | null;
  requiredSkills: string[];
  updatedAtUtc: string | null;
}

export interface CareRecipient {
  id: string;
  firstName: string;
  lastName: string | null;
  dateOfBirth: string;
  age: number;
  recipientType: CareRecipientType;
  careNeeds: CareNeeds;
}

export interface UpsertProfileInput {
  phoneNumber: string;
  aboutMe: string | null;
  address: AddressDto | null;
}

export interface AddCareRecipientInput {
  firstName: string;
  lastName: string | null;
  /** "YYYY-MM-DD" — the API takes a date, not an instant. */
  dateOfBirth: string;
  recipientType: CareRecipientType;
}

/**
 * A family's pets.
 *
 * A pet is not a care recipient: it has no date of birth and none of the care-needs
 * record, and the rules protecting children and vulnerable adults do not describe it.
 */
export type PetSpecies = "Dog" | "Cat" | "SmallAnimal" | "Bird" | "Other";

export const PET_SPECIES: PetSpecies[] = ["Dog", "Cat", "SmallAnimal", "Bird", "Other"];

export const PET_SPECIES_LABELS: Record<PetSpecies, string> = {
  Dog: "Dog",
  Cat: "Cat",
  SmallAnimal: "Small animal",
  Bird: "Bird",
  Other: "Other",
};

export type PetSize = "Small" | "Medium" | "Large" | "ExtraLarge";

export const PET_SIZES: PetSize[] = ["Small", "Medium", "Large", "ExtraLarge"];

export const PET_SIZE_LABELS: Record<PetSize, string> = {
  Small: "Small",
  Medium: "Medium",
  Large: "Large",
  ExtraLarge: "Extra large",
};

export interface Pet {
  id: string;
  name: string;
  species: PetSpecies;
  breed: string | null;
  size: PetSize | null;
  feedingInstructions: string | null;
  medication: string | null;
  behaviour: string | null;
  vetClinic: string | null;
  otherNotes: string | null;
}

export type SavePetInput = Omit<Pet, "id">;

export type { AddressDto };
