import { redirect } from "next/navigation";
import type { ReactNode } from "react";
import { getAccessToken } from "@/lib/auth/session";

/**
 * Frame for the signed-in application.
 *
 * Checks for a session before rendering anything beneath it. This is a convenience
 * gate, not the security boundary: the API authorises every request on its own, so a
 * forged cookie gets an empty page and nothing more.
 */
export default async function AppLayout({ children }: { children: ReactNode }) {
  if (!(await getAccessToken())) {
    redirect("/login");
  }

  return <>{children}</>;
}
