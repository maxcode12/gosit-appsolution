import type { Metadata } from "next";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { CaregiverCard } from "@/features/search/components/caregiver-card";
import { careRequesterNav } from "@/features/search/nav";
import { listFavorites } from "@/features/favorites/actions";

export const metadata: Metadata = {
  title: "Saved caregivers",
  description: "The caregivers you have saved.",
};

/** Figma node 129:4 — the Saved screen. */
export default async function SavedPage() {
  const saved = await listFavorites();

  return (
    <DashboardShell
      items={careRequesterNav}
      currentPath="/saved"
      heading="Saved caregivers"
      description={
        saved.length === 0
          ? undefined
          : `${saved.length} ${saved.length === 1 ? "caregiver" : "caregivers"} saved.`
      }
    >
      {saved.length === 0 ? (
        <EmptyState
          title="Nothing saved yet"
          description="Save a caregiver while you are searching and they will be here when you come back to compare."
          action={
            <Link href="/dashboard" className={buttonClassName()}>
              Find care
            </Link>
          }
        />
      ) : (
        // The container must be an ancestor of the grid: container queries apply to
        // descendants, so `@container` on the grid itself would never match.
        <div className="@container">
          <ul className="grid gap-4 @xl:grid-cols-2 @5xl:grid-cols-3">
            {saved.map((caregiver) => (
              <li key={caregiver.caregiverProfileId} className="flex">
                <CaregiverCard caregiver={caregiver} />
              </li>
            ))}
          </ul>
        </div>
      )}
    </DashboardShell>
  );
}
