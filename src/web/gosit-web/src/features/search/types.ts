/**
 * Mirrors the API's search contracts. These are the shapes the API actually returns —
 * note there is no street address, email, or certification document anywhere here,
 * which is deliberate on the server side and must stay true of the client types too.
 */

export interface CaregiverSearchResult {
  caregiverProfileId: string;
  firstName: string;
  /** Last initial only. */
  lastInitial: string;
  bio: string | null;
  yearsOfExperience: number;
  hourlyRate: number;
  /** Price per night for overnight pet care. Null for a caregiver who offers none. */
  nightlyRate: number | null;
  /** City and forward sortation area, e.g. "Toronto, ON M5S". */
  approximateLocation: string;
  /** Null until both addresses are geocoded. */
  distanceKm: number | null;
  averageRating: number | null;
  reviewCount: number;
  isVerified: boolean;
  serviceCodes: string[];
  certificationCodes: string[];
  isFavorite: boolean;
}

export interface CaregiverSearchResponse {
  results: CaregiverSearchResult[];
  totalMatches: number;
  page: number;
  pageSize: number;
  /** True when results could not be ranked by proximity. */
  distanceUnavailable: boolean;
  /**
   * Why, so the screen can say something true. Only "NoAddress" is the reader's to
   * fix; the other two are ours, and telling someone to add an address they already
   * gave sends them hunting for a setting that is already filled in.
   */
  distanceUnavailableReason:
    | "NoAddress"
    | "AddressNotLocated"
    | "CaregiversNotLocated"
    | null;
}

export interface CareServiceSummary {
  code: string;
  name: string;
  categoryCode: string;
}

export interface AvailabilityWindow {
  day: string;
  startTime: string;
  endTime: string;
}

export interface CaregiverDetail {
  caregiverProfileId: string;
  firstName: string;
  lastInitial: string;
  bio: string | null;
  yearsOfExperience: number;
  hourlyRate: number;
  nightlyRate: number | null;
  approximateLocation: string;
  serviceRadiusKm: number;
  averageRating: number | null;
  reviewCount: number;
  isVerified: boolean;
  /** False for a caregiver cleared only for pet care: they cannot be booked for a person. */
  clearedForPeopleCare: boolean;
  services: CareServiceSummary[];
  certificationCodes: string[];
  availability: AvailabilityWindow[];
  isFavorite: boolean;
}

export interface CareServiceCatalogue {
  categories: CareServiceCategory[];
}

export interface CareServiceCategory {
  code: string;
  name: string;
  displayOrder: number;
  services: CareService[];
}

export interface CareService {
  code: string;
  name: string;
  description: string | null;
  requiredCertificationCodes: string[];
  applicableRecipientTypes: string[];
  /** The kinds of animal a pet service is for. Empty for care for people. */
  applicablePetSpecies: string[];
  isPetCare: boolean;
  /** "Nightly" for boarding and in-home pet sitting. */
  pricingUnit: "Hourly" | "Nightly";
  /** "CaregiverHome" for boarding: the booking carries the caregiver's address. */
  careLocation: "RecipientHome" | "CaregiverHome";
}

/** Filters accepted by caregiver search. Every field is optional. */
export interface CaregiverSearchFilters {
  serviceCode?: string;
  city?: string;
  postalCode?: string;
  radiusKm?: number;
  day?: string;
  startTime?: string;
  endTime?: string;
  maxHourlyRate?: number;
  minYearsOfExperience?: number;
  minRating?: number;
  certifications?: string[];
  page?: number;
  pageSize?: number;
}
