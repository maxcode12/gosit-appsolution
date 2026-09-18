"use client";

import { loadStripe } from "@stripe/stripe-js";
import {
  Elements,
  PaymentElement,
  useElements,
  useStripe,
} from "@stripe/react-stripe-js";
import { useState } from "react";
import { Button } from "@/components/ui/button";

/**
 * Loaded once per publishable key, not per render — Stripe.js caches nothing across
 * separate `loadStripe` calls, and calling it fresh each render would open a new
 * connection to Stripe every time this component re-renders.
 */
let stripePromise: ReturnType<typeof loadStripe> | null = null;

function getStripe(publishableKey: string) {
  stripePromise ??= loadStripe(publishableKey);
  return stripePromise;
}

/**
 * Card entry for a booking's payment intent.
 *
 * Mounts Stripe's own Payment Element inside Stripe's own iframe — the card number
 * never reaches this component, this app's server, or anything GoSit's code can read.
 * That is the whole reason to use it instead of a plain input bound to state.
 */
export function PaymentForm({
  publishableKey,
  clientSecret,
  bookingId,
}: {
  publishableKey: string;
  clientSecret: string;
  bookingId: string;
}) {
  return (
    <Elements
      stripe={getStripe(publishableKey)}
      options={{ clientSecret, appearance: { theme: "stripe" } }}
    >
      <ConfirmForm bookingId={bookingId} />
    </Elements>
  );
}

function ConfirmForm({ bookingId }: { bookingId: string }) {
  const stripe = useStripe();
  const elements = useElements();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();

    if (!stripe || !elements) {
      return;
    }

    setSubmitting(true);
    setError(null);

    // Stripe's own redirect brings the family back to this same booking, which already
    // reads payment status from the API rather than trusting anything in the URL.
    const { error: confirmError } = await stripe.confirmPayment({
      elements,
      confirmParams: {
        return_url: `${window.location.origin}/bookings/${bookingId}`,
      },
    });

    // A successful confirmation navigates away via return_url and never reaches here.
    // Only a failure — a declined card, a validation problem — returns control to this
    // component.
    if (confirmError) {
      setError(confirmError.message ?? "The payment could not be completed.");
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="mt-4 flex flex-col gap-4">
      <PaymentElement />

      {error ? (
        <p role="alert" className="text-sm text-red-700">
          {error}
        </p>
      ) : null}

      <Button type="submit" variant="secondary" disabled={!stripe || submitting}>
        {submitting ? "Paying…" : "Pay now"}
      </Button>
    </form>
  );
}
