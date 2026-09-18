import type { Metadata } from "next";
import { AuthCard } from "@/features/authentication/components/auth-card";
import { ForgotPasswordForm } from "@/features/authentication/components/forgot-password-form";

export const metadata: Metadata = {
  title: "Forgot password",
  description: "Get a link to choose a new GoSit password.",
};

export default function ForgotPasswordPage() {
  return (
    <AuthCard title="Forgot your password?">
      <ForgotPasswordForm />
    </AuthCard>
  );
}
