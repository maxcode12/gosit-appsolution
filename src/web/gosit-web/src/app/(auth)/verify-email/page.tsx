import type { Metadata } from "next";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/button";
import { AuthCard } from "@/features/authentication/components/auth-card";
import { confirmEmailAction } from "@/features/authentication/recovery-actions";

export const metadata: Metadata = {
  title: "Confirm your email",
  // Verification links must never be indexed or followed by a crawler.
  robots: { index: false, follow: false },
};

/**
 * Redeems an email-verification link.
 *
 * Confirmed on load rather than behind a button. Someone arriving from their mailbox has
 * already expressed intent by clicking the link, and asking them to press a second button
 * to confirm they meant it is a step that exists only to make the page feel busy.
 *
 * The trade-off is that a link-scanning proxy — some corporate mail filters prefetch every
 * URL — will burn the token before the person sees it. They land on the "no longer valid"
 * message and can ask for another, which is the better failure of the two: the alternative
 * is a scanner that cannot verify, and a person who has to click twice, every time.
 */
export default async function VerifyEmailPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const raw = Array.isArray(params.token) ? params.token[0] : params.token;
  const token = raw?.trim();

  const verified = token ? await confirmEmailAction(token) : false;

  if (verified) {
    return (
      <AuthCard title="Email confirmed">
        <div className="text-center">
          <p className="text-sm leading-relaxed text-text-secondary">
            Thank you — your address is confirmed. If you ever forget your password, we
            can now get you back into your account.
          </p>
          <Link href="/login" className={buttonClassName("primary", "mt-6")}>
            Continue
          </Link>
        </div>
      </AuthCard>
    );
  }

  return (
    <AuthCard title="This link is no longer valid">
      <div className="text-center">
        <p className="text-sm leading-relaxed text-text-secondary">
          Verification links expire after a week, and each one can only be used once. Log
          in and we will send you a fresh one.
        </p>
        <Link href="/login" className={buttonClassName("primary", "mt-6")}>
          Log in
        </Link>
      </div>
    </AuthCard>
  );
}
