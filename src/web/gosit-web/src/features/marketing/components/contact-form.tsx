"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/components/ui/button";
import { TextInput, inputClassName, textareaClassName } from "@/components/ui/text-input";
import { PUBLIC_API_BASE_URL } from "@/features/marketing/public-mode";

type Status = "idle" | "sending" | "sent" | "error";

const KINDS = [
  { value: "Family", label: "A family looking for care" },
  { value: "Caregiver", label: "A caregiver" },
  { value: "Partner", label: "A partner, vendor or organisation" },
  { value: "Other", label: "Something else" },
];

/**
 * The public contact form.
 *
 * It posts straight to the API from the visitor's browser: the public site is plain static
 * files with no server of its own. The API accepts this website's address and no other, and
 * throttles by IP. A visitor never sees a stack trace or a status code — every failure is
 * a sentence about what to do next.
 */
export function ContactForm() {
  const [status, setStatus] = useState<Status>("idle");
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    // Read before the first await: React clears the event's target afterwards.
    const form = event.currentTarget;
    const data = new FormData(form);

    setStatus("sending");
    setError(null);
    setFieldErrors({});

    try {
      const response = await fetch(`${PUBLIC_API_BASE_URL}/api/v1/enquiries`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: data.get("name"),
          email: data.get("email"),
          kind: data.get("kind"),
          message: data.get("message"),
          consentGiven: data.get("consentGiven") === "on",
          // Hidden from people, tempting to scripts. See the API's enquiry endpoint.
          website: data.get("website"),
        }),
      });

      if (response.status === 202) {
        form.reset();
        setStatus("sent");
        return;
      }

      if (response.status === 400) {
        const problem = await response.json().catch(() => null);
        const mapped: Record<string, string> = {};

        for (const [key, messages] of Object.entries<string[]>(problem?.errors ?? {})) {
          if (messages?.length) {
            mapped[key.charAt(0).toLowerCase() + key.slice(1)] = messages[0];
          }
        }

        setFieldErrors(mapped);
        setError(
          Object.keys(mapped).length > 0
            ? "Please check the highlighted fields."
            : (problem?.title ?? "We could not send your message. Please check it and try again."),
        );
        setStatus("error");
        return;
      }

      if (response.status === 429) {
        setError("You have sent a few messages already. Please try again later.");
        setStatus("error");
        return;
      }

      setError("We could not send your message. Please try again in a few minutes.");
      setStatus("error");
    } catch {
      setError("We could not reach GoSit. Check your connection and try again.");
      setStatus("error");
    }
  }

  if (status === "sent") {
    return (
      <div role="status" className="rounded-card border border-brand-soft-border bg-brand-soft p-6 text-center">
        <h3 className="font-heading text-xl font-semibold text-text-primary">Message sent</h3>
        <p className="mt-2 text-text-secondary">
          Thank you. We will reply to the email address you gave us.
        </p>
        <Button type="button" variant="secondary" className="mt-5" onClick={() => setStatus("idle")}>
          Send another message
        </Button>
      </div>
    );
  }

  const sending = status === "sending";

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-5">
      {error ? (
        <p
          role="alert"
          className="rounded-control border border-danger-accent/20 bg-danger-soft px-4 py-3 text-sm text-danger-accent"
        >
          {error}
        </p>
      ) : null}

      <div className="grid gap-5 sm:grid-cols-2">
        <TextInput
          label="Your name"
          name="name"
          autoComplete="name"
          required
          maxLength={120}
          error={fieldErrors.name}
        />
        <TextInput
          label="Email address"
          name="email"
          type="email"
          autoComplete="email"
          required
          maxLength={256}
          error={fieldErrors.email}
        />
      </div>

      <div>
        <label htmlFor="kind" className="mb-1.5 block font-control text-sm font-medium text-text-secondary">
          I am
        </label>
        <select id="kind" name="kind" defaultValue="Family" className={inputClassName}>
          {KINDS.map((kind) => (
            <option key={kind.value} value={kind.value}>
              {kind.label}
            </option>
          ))}
        </select>
      </div>

      <div>
        <label htmlFor="message" className="mb-1.5 block font-control text-sm font-medium text-text-secondary">
          Your message
        </label>
        <textarea
          id="message"
          name="message"
          rows={5}
          required
          minLength={10}
          maxLength={2000}
          aria-invalid={fieldErrors.message ? true : undefined}
          aria-describedby={fieldErrors.message ? "message-error" : undefined}
          className={textareaClassName}
        />
        {fieldErrors.message ? (
          <p id="message-error" className="mt-1.5 font-control text-sm text-danger-accent">
            {fieldErrors.message}
          </p>
        ) : null}
      </div>

      {/* A field no person can see. Anything that fills it is a script. */}
      <div aria-hidden="true" className="absolute -left-[9999px] h-0 w-0 overflow-hidden">
        <label>
          Website
          <input type="text" name="website" tabIndex={-1} autoComplete="off" />
        </label>
      </div>

      <div>
        <label className="flex items-start gap-3 text-sm text-text-secondary">
          <input
            type="checkbox"
            name="consentGiven"
            required
            className="mt-1 size-4 accent-brand-primary"
          />
          <span>
            I agree that GoSit may use these details to reply to me. We do not add anyone to a
            mailing list.
          </span>
        </label>
        {fieldErrors.consentGiven ? (
          <p className="mt-1.5 font-control text-sm text-danger-accent">{fieldErrors.consentGiven}</p>
        ) : null}
      </div>

      <Button type="submit" size="lg" disabled={sending} className="w-full sm:w-auto sm:self-start">
        {sending ? "Sending…" : "Send message"}
      </Button>
    </form>
  );
}
