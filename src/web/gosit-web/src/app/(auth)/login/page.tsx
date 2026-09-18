import type { Metadata } from "next";
import { AuthCard } from "@/features/authentication/components/auth-card";
import { LoginForm } from "@/features/authentication/components/login-form";
import { SocialLoginGroup } from "@/features/authentication/components/social-login-group";

export const metadata: Metadata = {
  title: "Log in",
  description: "Log in to your GoSit account.",
};

/** Figma node 49:8. */
export default function LoginPage() {
  return (
    <AuthCard title="Log in to GoSit">
      <SocialLoginGroup />
      <div className="mt-6">
        <LoginForm />
      </div>
    </AuthCard>
  );
}
