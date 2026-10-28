"use client";

import { useEffect, useRef, useState, useTransition } from "react";
import { googleSignInAction } from "@/features/authentication/actions";
import { env } from "@/lib/env";

/**
 * "Continue with Google", drawn by Google's own script so it follows their branding.
 *
 * The button hands back a signed ID token, which goes to the API to be verified. Someone
 * with no GoSit account yet is asked whether they are a family or a caregiver and the
 * same token is sent again — it stays valid long enough for that.
 *
 * Renders nothing when no client ID is configured, rather than a button that cannot work.
 * Facebook and Apple are not offered: neither is wired up, and a disabled button is noise.
 */

const SCRIPT_SRC = "https://accounts.google.com/gsi/client";

interface GoogleIdentityApi {
  accounts: {
    id: {
      initialize: (config: {
        client_id: string;
        callback: (response: { credential?: string }) => void;
      }) => void;
      renderButton: (element: HTMLElement, options: Record<string, unknown>) => void;
    };
  };
}

declare global {
  interface Window {
    google?: GoogleIdentityApi;
  }
}

type Role = "CareRequester" | "Caregiver";

export function SocialLoginGroup({ role }: { role?: Role }) {
  const clientId = env.googleClientId;
  const buttonRef = useRef<HTMLDivElement>(null);
  const [pendingToken, setPendingToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  useEffect(() => {
    if (!clientId) return;

    function render() {
      const google = window.google;
      if (!google || !buttonRef.current) return;

      google.accounts.id.initialize({
        client_id: clientId as string,
        callback: ({ credential }) => {
          if (!credential) {
            setError("Google did not return a sign-in. Try again.");
            return;
          }

          submit(credential, role);
        },
      });

      google.accounts.id.renderButton(buttonRef.current, {
        theme: "outline",
        size: "large",
        text: "continue_with",
        shape: "pill",
        width: 320,
      });
    }

    if (window.google) {
      render();
      return;
    }

    const existing = document.querySelector<HTMLScriptElement>(`script[src="${SCRIPT_SRC}"]`);
    if (existing) {
      existing.addEventListener("load", render);
      return () => existing.removeEventListener("load", render);
    }

    const script = document.createElement("script");
    script.src = SCRIPT_SRC;
    script.async = true;
    script.addEventListener("load", render);
    document.head.appendChild(script);

  }, [clientId, role]);

  function submit(idToken: string, chosenRole?: Role) {
    setError(null);

    startTransition(async () => {
      const result = await googleSignInAction(idToken, chosenRole);

      // A successful sign-in redirects, so anything returned here is a problem or a question.
      if (result.needsRole) {
        setPendingToken(idToken);
        return;
      }

      if (result.error) {
        setPendingToken(null);
        setError(result.error);
      }
    });
  }

  if (!clientId) {
    return null;
  }

  return (
    <div className="flex flex-col gap-3">
      {pendingToken ? (
        <div className="flex flex-col gap-3 rounded-xl border border-border bg-surface p-4">
          <p className="font-control text-sm text-text-primary">
            Welcome! Which describes you?
          </p>
          <div className="grid gap-2.5 sm:grid-cols-2">
            <button
              type="button"
              disabled={isPending}
              onClick={() => submit(pendingToken, "CareRequester")}
              className="h-11 rounded-xl border border-border bg-surface font-control text-sm text-text-primary hover:bg-surface-muted disabled:opacity-60"
            >
              I need care
            </button>
            <button
              type="button"
              disabled={isPending}
              onClick={() => submit(pendingToken, "Caregiver")}
              className="h-11 rounded-xl border border-border bg-surface font-control text-sm text-text-primary hover:bg-surface-muted disabled:opacity-60"
            >
              I provide care
            </button>
          </div>
        </div>
      ) : (
        <div ref={buttonRef} className="flex min-h-11 justify-center" aria-busy={isPending} />
      )}

      {error ? (
        <p role="alert" className="text-center text-sm text-danger-accent">
          {error}
        </p>
      ) : null}

      <div className="flex items-center gap-3" aria-hidden>
        <span className="h-px flex-1 bg-border" />
        <span className="font-control text-xs font-medium uppercase tracking-widest text-text-muted">or</span>
        <span className="h-px flex-1 bg-border" />
      </div>
    </div>
  );
}
