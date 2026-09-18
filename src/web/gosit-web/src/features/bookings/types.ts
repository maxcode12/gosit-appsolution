import type { Pet } from "@/features/account/types";
import type { AddressDto } from "@/features/bookings/address";

/**
 * Mirrors the API's booking contracts.
 *
 * Note what a booking carries and what it does not. A caregiver gets the area of a request
 * but not the address, notes or pet care notes until they accept — the family always sees
 * their own. A recipient appears only as a first name, an age and whether care needs
 * exist, never the needs themselves.
 */

export type BookingStatus =
  | "Pending"
  | "Accepted"
  | "Declined"
  | "Cancelled"
  | "InProgress"
  | "Completed"
  | "Disputed";

/** City, province and postal prefix: enough to judge the distance, not to find the door. */
export interface BookingArea {
  city: string;
  province: string;
  postalCodePrefix: string;
}

export interface PetCareNotes {
  feedingInstructions: string | null;
  medication: string | null;
  behaviour: string | null;
  vetClinic: string | null;
  otherNotes: string | null;
}

/** A pet named on a booking, as it was when booked. */
export interface BookingPetSummary {
  petId: string;
  name: string;
  species: string;
  breed: string | null;
  size: string | null;
  /** Null for a caregiver until they accept. */
  careNotes: PetCareNotes | null;
}

export interface BookingRecipientSummary {
  careRecipientId: string;
  firstName: string;
  age: number;
  recipientType: string;
  /** Whether needs are recorded, never what they say. */
  hasRecordedCareNeeds: boolean;
}

export interface Booking {
  id: string;
  status: BookingStatus;
  caregiverProfileId: string;
  caregiverFirstName: string;
  caregiverLastInitial: string;
  careRequesterFirstName: string;
  careRequesterLastInitial: string;
  careServiceId: string;
  careServiceCode: string;
  careServiceName: string;
  startAtUtc: string;
  endAtUtc: string;
  /**
   * The exact address — the family's, or for boarding the caregiver's. Null whenever
   * `addressDisclosed` is false: for care at home, to a caregiver who has not accepted; for
   * boarding, to a family whose caregiver has not accepted. Show `area` instead.
   */
  address: AddressDto | null;
  area: BookingArea;
  /** The family's notes. Null for a caregiver until they accept, like the address. */
  instructions: string | null;
  hourlyRate: number;
  /** "Nightly" for boarding and in-home pet sitting, priced per night rather than per hour. */
  pricingUnit: "Hourly" | "Nightly";
  nightlyRate: number | null;
  /** Nights booked, for a booking priced by the night. */
  nights: number | null;
  /** "CaregiverHome" for boarding: `address` is then the caregiver's, not the family's. */
  careLocation: "RecipientHome" | "CaregiverHome";
  estimatedTotal: number;
  /** Set at check-out, from the hours actually worked. */
  actualTotal: number | null;
  checkedInAtUtc: string | null;
  checkedOutAtUtc: string | null;
  /** True when the platform closed the booking because nobody checked out. */
  /**
   * Whether the caregiver was at the address when they started and finished.
   *
   * A status, never a location — the coordinates were compared against the booking
   * address on the server and discarded. "NotProvided" means the device could not offer
   * one, which is common and is nobody's fault; it must never be shown as a failure.
   */
  checkInVerification: ArrivalVerification;
  checkOutVerification: ArrivalVerification;
  wasAutoCompleted: boolean;
  cancelledBy: string | null;
  isLateCancellation: boolean;
  resolutionReason: string | null;
  /**
   * How staff settled a dispute on this booking, in the same words for both sides.
   *
   * The notification only says a decision was made; this is where it is read.
   */
  disputeResolvedAtUtc: string | null;
  disputeResolutionNotes: string | null;
  recipients: BookingRecipientSummary[];
  /** Pets on a pet-care booking. A booking names people or pets, never both. */
  pets: BookingPetSummary[];
  /** Whether the address, notes and pet care notes are included in this response. */
  householdDetailsDisclosed: boolean;
  /**
   * Whether `address` is included. For boarding the address is the caregiver's, so the
   * family sees it once the caregiver accepts, and the caregiver always.
   */
  addressDisclosed: boolean;
  createdAtUtc: string;
}

export interface CreateBookingInput {
  caregiverProfileId: string;
  careServiceCode: string;
  careRecipientIds?: string[];
  petIds?: string[];
  startAtUtc: string;
  endAtUtc: string;
  /** Omitted to use the address on the requester's profile. */
  address?: AddressDto;
  instructions?: string;
}

/** The receipt for a booking. No card details — GoSit never sees a card number. */
export interface PaymentReceipt {
  paymentId: string;
  careBookingId: string;
  amount: number;
  refundedAmount: number;
  netAmount: number;
  currency: string;
  status: string;
  succeededAtUtc: string | null;
  refunds: { amount: number; reason: string | null; createdAtUtc: string }[];
}

export interface CareRecipientSummary {
  id: string;
  firstName: string;
  lastName: string | null;
  dateOfBirth: string;
  age: number;
  recipientType: string;
  hasRecordedCareNeeds: boolean;
}

export interface CareRequesterProfile {
  id: string;
  phoneNumber: string;
  aboutMe: string | null;
  address: AddressDto | null;
  careRecipients: CareRecipientSummary[];
  pets: Pet[];
  emergencyContacts: {
    id: string;
    fullName: string;
    relationship: string;
    phoneNumber: string;
  }[];
}
export type { AddressDto } from "@/features/bookings/address";

export type ArrivalVerification =
  | "NotProvided"
  | "NotComparable"
  | "AtTheAddress"
  | "AwayFromTheAddress";

/**
 * How arrival reads to a person, and only when it says something.
 *
 * `NotProvided` and `NotComparable` both return null on purpose. Neither is news: one
 * means the phone could not fix a location, the other that the address was never
 * geocoded, and dressing either up as "location unconfirmed" invites a family to read
 * suspicion into a phone with no signal.
 *
 * `AwayFromTheAddress` is stated plainly but not accusingly. It is a fact about a GPS
 * reading, not a finding about a person, and the family is the one placed to judge it —
 * a caregiver taking a child to the park is exactly where they should be.
 */
export function arrivalNote(verification: ArrivalVerification): string | null {
  if (verification === "AtTheAddress") {
    return "Location confirmed at the address.";
  }

  if (verification === "AwayFromTheAddress") {
    return "The location recorded was not at the address.";
  }

  return null;
}

/** The catalogue category every pet service belongs to. */
export const PET_CARE_CATEGORY = "PET_CARE";

export function isPetCareService(service: { categoryCode: string }): boolean {
  return service.categoryCode === PET_CARE_CATEGORY;
}

const SPECIES_WORDS: Record<string, string> = {
  Dog: "dog",
  Cat: "cat",
  SmallAnimal: "small animal",
  Bird: "bird",
  Other: "pet",
};

const SIZE_WORDS: Record<string, string> = {
  Small: "small",
  Medium: "medium",
  Large: "large",
  ExtraLarge: "extra large",
};

/** "Biscuit (large dog, Labrador)". Takes a pet from a booking or from a profile. */
export function describePet(pet: {
  name: string;
  species: string;
  breed: string | null;
  size: string | null;
}): string {
  const kind = [pet.size ? SIZE_WORDS[pet.size] : null, SPECIES_WORDS[pet.species] ?? "pet"]
    .filter(Boolean)
    .join(" ");

  return `${pet.name} (${kind}${pet.breed ? `, ${pet.breed}` : ""})`;
}

/** Who, or what, a booking is for, in one line. */
export function careForSummary(booking: Pick<Booking, "recipients" | "pets">): string {
  if (booking.pets.length > 0) {
    return booking.pets.map(describePet).join(", ");
  }

  return booking.recipients
    .map((recipient) => `${recipient.firstName} (${recipient.age})`)
    .join(", ");
}
