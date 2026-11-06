import type { Metadata } from "next";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { isSuperAdmin, listStaff } from "@/features/admin/api";
import { StaffManager } from "@/features/admin/components/staff-manager";
import { adminNav } from "@/features/admin/nav";

export const metadata: Metadata = {
  title: "Staff",
  description: "The people who work on GoSit, and adding new ones.",
};

/**
 * The team, for super administrators. Anyone else sees why they cannot use it, rather than an
 * empty page that looks broken; the API enforces the rule either way.
 */
export default async function AdminStaffPage() {
  const allowed = await isSuperAdmin();
  const staff = allowed ? await listStaff() : [];

  return (
    <DashboardShell
      items={adminNav()}
      currentPath="/admin/staff"
      heading="Staff"
      description="The people who work on GoSit."
    >
      <div className="max-w-3xl">
        {allowed ? (
          <StaffManager staff={staff} />
        ) : (
          <p className="text-sm text-text-secondary">
            Only a super administrator can add staff or see the team list. Ask one to add anyone who
            needs access.
          </p>
        )}
      </div>
    </DashboardShell>
  );
}
