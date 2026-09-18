import type { Metadata } from "next";
import Link from "next/link";
import { AuthCard } from "@/features/authentication/components/auth-card";
import { ResetPasswordForm } from "@/features/authentication/components/reset-password-form";

export const metadata: Metadata = {
  title: "Choose a new password",
  // Reset links must never be indexed or followed by a crawler.
  robots: { index: false, follow: false },
};

/**
 * Redeems a password-reset link.
 *
 * The token arrives in the query string, which is unavoidable for an emailed link. It is
 * read here and passed to the action bound server-side, so it never lands in a form field.
 */
export default async function ResetPasswordPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const raw = Array.isArray(params.token) ? params.token[0] : params.token;
  const token = raw?.trim();

  if (!token) {
    return (
      <AuthCard title="That link is not complete">
        <div className="text-center">
          <p className="text-sm leading-relaxed text-text-secondary">
            The link seems to have been cut short — some mail clients do that to long
            links. Ask for a fresh one and open it in a single click.
          </p>
          <Link
            href="/forgot-password"
            className="mt-6 inline-block font-control text-sm font-semibold text-brand-primary hover:underline"
          >
            Send me a new link
          </Link>
        </div>
      </AuthCard>
    );
  }

  return (
    <AuthCard title="Choose a new password">
      <ResetPasswordForm token={token} />
    </AuthCard>
  );
}
