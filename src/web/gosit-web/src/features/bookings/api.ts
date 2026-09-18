import "server-only";

import { ApiError, apiFetch } from "@/lib/api/client";
import type {
  Booking,
  BookingStatus,
  CareRequesterProfile,
  PaymentReceipt,
} from "@/features/bookings/types";

/**
 * Reads for the booking screens. Every call is scoped to the signed-in account by the
 * API — no route takes a user or profile id, so one account cannot read another's.
 */

export async function listBookings(options: {
  status?: BookingStatus;
  upcomingOnly?: boolean;
} = {}): Promise<Booking[]> {
  const params = new URLSearchParams();

  if (options.status) {
    params.set("status", options.status);
  }

  if (options.upcomingOnly) {
    params.set("upcomingOnly", "true");
  }

  const query = params.toString();

  return apiFetch<Booking[]>(`/api/v1/bookings${query ? `?${query}` : ""}`);
}

export async function getBooking(bookingId: string): Promise<Booking> {
  return apiFetch<Booking>(`/api/v1/bookings/${bookingId}`);
}

/**
 * The receipt, or null when nobody has started paying yet. A 404 here is the normal
 * "no payment exists" case rather than an error worth surfacing.
 */
export async function getPaymentReceipt(bookingId: string): Promise<PaymentReceipt | null> {
  try {
    return await apiFetch<PaymentReceipt>(`/api/v1/bookings/${bookingId}/payment`);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return null;
    }

    throw error;
  }
}

/**
 * The caller's care-requester profile, or null if they have not created one.
 *
 * A new account has no profile, and booking is impossible without one — so callers use
 * the null to send someone to set it up rather than failing at submit.
 */
export async function getMyProfile(): Promise<CareRequesterProfile | null> {
  try {
    return await apiFetch<CareRequesterProfile>("/api/v1/care-requesters/me");
  } catch (error) {
    // 404 is "not created yet"; 403 is a caregiver reaching a `CareRequesterOnly`
    // route. Both mean the same thing to a page — no profile here — and throwing on
    // the second would race the layout redirect that is already sending them home.
    if (error instanceof ApiError && (error.status === 404 || error.status === 403)) {
      return null;
    }

    throw error;
  }
}
