"use client";

import { useSearchParams } from "next/navigation";
import { ContactForm } from "@/features/marketing/components/contact-form";

/**
 * The early-list form. It is the contact form with the right "I am" pre-selected from the
 * link the visitor came through, so a caregiver who pressed "Become a caregiver" is not asked
 * to say so again.
 */
export function EarlyAccessForm() {
  const as = useSearchParams().get("as");

  return <ContactForm defaultKind={as === "caregiver" ? "Caregiver" : "Family"} />;
}
