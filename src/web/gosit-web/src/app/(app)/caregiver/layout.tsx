import { redirect } from "next/navigation";
import type { ReactNode } from "react";
import { getSessionRole, homePathFor } from "@/lib/auth/session";

/**
 * Frame for the caregiver side.
 *
 * Sends a care requester who lands here back to their own home. This is navigation, not
 * authorisation: every caregiver endpoint is gated by the API's `CaregiverOnly` policy,
 * so a forged role cookie gets a rail it cannot use and a page of empty states. The
 * point is to avoid showing someone a section that could never work for them.
 */
export default async function CaregiverLayout({ children }: { children: ReactNode }) {
  const role = await getSessionRole();

  if (role && role !== "Caregiver") {
    redirect(homePathFor(role));
  }

  return <>{children}</>;
}
