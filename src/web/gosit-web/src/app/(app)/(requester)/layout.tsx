import { redirect } from "next/navigation";
import type { ReactNode } from "react";
import { getSessionRole, homePathFor } from "@/lib/auth/session";

/**
 * Frame for the care-requester side.
 *
 * The mirror of the caregiver layout: a caregiver who lands here is sent to their own
 * home rather than shown screens whose every API call would be refused. A route group,
 * so the URLs are unchanged — `/dashboard` stays `/dashboard`.
 *
 * Navigation, not authorisation. The API enforces roles on every request; this only
 * avoids showing someone a section that could never work for them.
 */
export default async function CareRequesterLayout({ children }: { children: ReactNode }) {
  const role = await getSessionRole();

  if (role && role !== "CareRequester") {
    redirect(homePathFor(role));
  }

  return <>{children}</>;
}
