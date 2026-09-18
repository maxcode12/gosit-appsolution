/**
 * A Canadian postal address, matching the API's AddressDto.
 *
 * Lives with bookings rather than search because search deliberately never returns one —
 * a caregiver's exact address is not disclosed until a booking exists.
 */
export interface AddressDto {
  line1: string;
  line2: string | null;
  city: string;
  /** Two-letter province code, e.g. "ON". */
  province: string;
  /** Stored without the inner space, e.g. "M5S1M2". */
  postalCode: string;
  country: string;
}

/** Formats an address for display, restoring the postal code's readable spacing. */
export function formatAddress(address: AddressDto): string {
  const postalCode =
    address.postalCode.length === 6
      ? `${address.postalCode.slice(0, 3)} ${address.postalCode.slice(3)}`
      : address.postalCode;

  return [address.line1, address.line2, `${address.city}, ${address.province} ${postalCode}`]
    .filter(Boolean)
    .join(", ");
}
