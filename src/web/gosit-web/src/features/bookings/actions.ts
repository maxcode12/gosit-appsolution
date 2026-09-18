"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { ApiError, SessionExpiredError, apiFetch, apiSend } from "@/lib/api/client";
import type { Booking, CreateBookingInput } from "@/features/bookings/types";

/**
 * Booking mutations.
 *
 * There is deliberately no "set status" action, mirroring the API: each lifecycle change
 * is its own operation, so the state machine stays on the server.
 *
 * Note what is absent from the create payload — a price. The rate comes from the
 * caregiver's stored profile and the total is computed server-side.
 */

export interface BookingFormState {
  error?: string;
  fieldErrors?: Record<string, string>;
  saved?: boolean;
}

/** Turns an API failure into something worth showing a person. */
function toState(error: unknown, fallback: string): BookingFormState {
  if (error instanceof SessionExpiredError) {
    return { error: "Your session has expired. Log in again to continue." };
  }

  if (error instanceof ApiError) {
    const fieldErrors: Record<string, string> = {};

    for (const [key, messages] of Object.entries(error.problem?.errors ?? {})) {
      const field = key.charAt(0).toLowerCase() + key.slice(1);
      if (messages.length > 0) {
        fieldErrors[field] = messages[0];
      }
    }

    // The API's own message is written to be safe to show, so it is preferred over a
    // generic one when present.
    return { error: error.problem?.title ?? fallback, fieldErrors };
  }

  throw error;
}

export async function createBookingAction(
  _previous: BookingFormState,
  formData: FormData,
): Promise<BookingFormState> {
  const caregiverProfileId = String(formData.get("caregiverProfileId") ?? "");
  const careServiceCode = String(formData.get("careServiceCode") ?? "");
  const date = String(formData.get("date") ?? "");
  const startTime = String(formData.get("startTime") ?? "");
  const endTime = String(formData.get("endTime") ?? "");
  // Only a stay has a pick-up date of its own.
  const endDate = String(formData.get("endDate") ?? "");
  const careRecipientIds = formData.getAll("careRecipientIds").map(String);
  const petIds = formData.getAll("petIds").map(String);
  const instructions = String(formData.get("instructions") ?? "").trim();

  const fieldErrors: Record<string, string> = {};

  if (!careServiceCode) fieldErrors.careServiceCode = "Choose the type of care.";
  if (careRecipientIds.length === 0 && petIds.length === 0) {
    fieldErrors.careRecipientIds = "Choose who, or which pets, the care is for.";
  }
  if (!date) fieldErrors.date = "Choose a date.";
  if (!startTime || !endTime) fieldErrors.startTime = "Choose a start and end time.";

  if (Object.keys(fieldErrors).length > 0) {
    return { fieldErrors };
  }

  /*
   * The form collects local wall-clock time; the API takes UTC. Converting here keeps
   * the browser's zone out of the contract.
   *
   * An end time earlier in the day than the start means the care runs past midnight —
   * overnight child care is a service GoSit sells, and this used to refuse it outright
   * with "the end time must be after the start time". The end lands on the following
   * day instead.
   */
  const start = new Date(`${date}T${startTime}`);
  const end = new Date(`${endDate || date}T${endTime}`);

  if (endDate) {
    if (end <= start) {
      return { fieldErrors: { endDate: "The stay must end after it starts." } };
    }
  } else if (end <= start) {
    end.setDate(end.getDate() + 1);
  }

  if (end.getTime() === start.getTime()) {
    return { fieldErrors: { endTime: "The start and end times must differ." } };
  }

  const startAtUtc = start.toISOString();
  const endAtUtc = end.toISOString();

  const input: CreateBookingInput = {
    caregiverProfileId,
    careServiceCode,
    careRecipientIds: careRecipientIds.length > 0 ? careRecipientIds : undefined,
    petIds: petIds.length > 0 ? petIds : undefined,
    startAtUtc,
    endAtUtc,
    instructions: instructions || undefined,
  };

  let booking: Booking;

  try {
    booking = await apiFetch<Booking>("/api/v1/bookings", { method: "POST", body: input });
  } catch (error) {
    return toState(error, "We could not request this booking.");
  }

  revalidatePath("/bookings");
  redirect(`/bookings/${booking.id}`);
}

/**
 * Cancels a booking.
 *
 * Takes the reason as FormData rather than a string argument. Next's development server
 * echoes scalar Server Action arguments to the console; FormData it does not. Whatever
 * someone types about their own family is not console material.
 */
export async function cancelBookingAction(
  bookingId: string,
  _previous: BookingFormState,
  formData: FormData,
): Promise<BookingFormState> {
  const reason = String(formData.get("reason") ?? "").trim();

  try {
    await apiSend(`/api/v1/bookings/${bookingId}/cancel`, {
      method: "POST",
      body: { reason: reason || null },
    });
  } catch (error) {
    return toState(error, "We could not cancel this booking.");
  }

  revalidatePath("/bookings");
  revalidatePath(`/bookings/${bookingId}`);

  return {};
}

/**
 * Creates the payment intent for a booking.
 *
 * Returns the client secret to the browser now that something actually uses it — the
 * Payment Element needs it to mount. Nothing else about the intent (amount, provider
 * ids) crosses with it.
 */
export async function startPaymentAction(
  bookingId: string,
): Promise<BookingFormState & { clientSecret?: string }> {
  let clientSecret: string;

  try {
    const intent = await apiFetch<{ clientSecret: string }>(
      `/api/v1/bookings/${bookingId}/payment-intent`,
      { method: "POST" },
    );
    clientSecret = intent.clientSecret;
  } catch (error) {
    return toState(error, "We could not start this payment.");
  }

  return { clientSecret };
}

/** Submits a review. FormData for the same reason as {@link cancelBookingAction}. */
export async function submitReviewAction(
  bookingId: string,
  _previous: BookingFormState,
  formData: FormData,
): Promise<BookingFormState> {
  const rating = Number(formData.get("rating"));
  const comment = String(formData.get("comment") ?? "").trim();

  if (!Number.isInteger(rating) || rating < 1 || rating > 5) {
    return { fieldErrors: { rating: "Choose a rating from 1 to 5." } };
  }

  try {
    await apiSend(`/api/v1/bookings/${bookingId}/review`, {
      method: "POST",
      body: { rating, comment: comment || null },
    });
  } catch (error) {
    return toState(error, "We could not submit this review.");
  }

  revalidatePath(`/bookings/${bookingId}`);

  return { saved: true };
}
