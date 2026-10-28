import type { Metadata } from "next";
import { AuthCard } from "@/features/authentication/components/auth-card";
import { SignupForm } from "@/features/authentication/components/signup-form";
import { SocialLoginGroup } from "@/features/authentication/components/social-login-group";

export const metadata: Metadata = {
  title: "Sign up",
  description: "Create your free GoSit account.",
};

/** Figma node 77:12. */
export default async function SignupPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const raw = Array.isArray(params.postalCode) ? params.postalCode[0] : params.postalCode;

  // `?type=` is set by the website's "Find a caregiver" / "Become a caregiver" buttons.
  const type = Array.isArray(params.type) ? params.type[0] : params.type;
  const accountType = type === "caregiver" ? "Caregiver" : type === "family" ? "CareRequester" : undefined;

  return (
    <AuthCard
      title={accountType === "Caregiver" ? "Become a GoSit caregiver" : "Welcome to GoSit"}
      subtitle={
        accountType === "Caregiver"
          ? "Create your profile. You will complete your checks next."
          : "Sign up for free, no commitment"
      }
    >
      <SocialLoginGroup role={accountType} />
      <div className="mt-6">
        {/* Carried from the landing page's search box, if that is where they came from. */}
        <SignupForm postalCode={raw} accountType={accountType} />
      </div>
    </AuthCard>
  );
}
