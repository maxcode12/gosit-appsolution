import { redirect } from "next/navigation";
import type { ReactNode } from "react";
import { getSessionRole, homePathFor } from "@/lib/auth/session";

/**
 * Frame for the staff portal.
 *
 * Sends anyone who is not staff back to their own home. Navigation, not authorisation:
 * every admin route is gated by the API's AdminOnly policy, so a forged role cookie gets
 * a rail whose every request is refused. `src/proxy.ts` catches this before the page
 * renders; this is the backstop for a route the matcher misses.
 */
export default async function AdminLayout({ children }: { children: ReactNode }) {
  const role = await getSessionRole();

  if (role && role !== "Admin") {
    redirect(homePathFor(role));
  }

  return <>{children}</>;
}
