"use client";

import { useRef, useState, type FormEvent, type ReactNode } from "react";
import { useSearchParams } from "next/navigation";
import { Button } from "@/components/ui/button";
import { TextInput, inputClassName, textareaClassName } from "@/components/ui/text-input";
import { PUBLIC_API_BASE_URL } from "@/features/marketing/public-mode";

type Role = "Family" | "Caregiver";
type Status = "idle" | "sending" | "sent" | "error";

const WHO = ["A child", "A senior", "An adult who needs support", "A pet"];

const CARE_BY_WHO: Record<string, string[]> = {
  "A child": ["Babysitting", "Nanny care", "After-school care", "Overnight care"],
  "A senior": ["Companionship", "Meal support", "Errands", "Mobility assistance", "Appointment companion", "Dementia companion care"],
  "An adult who needs support": ["Respite care", "Autism or developmental support", "Communication support", "Mobility support", "Light household support"],
  "A pet": ["Dog walking", "Drop-in visits", "Pet sitting at my home", "Boarding at the caregiver's home"],
};

const CAREGIVER_SERVICES = Array.from(new Set(Object.values(CARE_BY_WHO).flat()));
const CARED_FOR = ["Infants", "Children", "Teens", "Seniors", "Adults with disabilities", "Pets"];
const CREDENTIALS = ["CPR", "First Aid", "Early childhood education", "Personal support worker", "Dementia care training", "Pet first aid", "None yet"];
const SCHEDULE = ["Weekday daytime", "Evenings", "Overnight", "Weekends", "One-time or occasional", "Regular and ongoing"];
const START = ["As soon as possible", "Within a month", "Just exploring"];
const EXPERIENCE = ["Under a year", "1 to 2 years", "3 to 5 years", "6 to 10 years", "More than 10 years"];
const CHECKS = ["Yes, happy to", "I have questions first"];
const INSURANCE = ["I have my own", "I can arrange it", "Not sure yet"];
const TRANSPORT = ["Own vehicle", "Public transit or on foot", "Neither"];

const POSTAL_START = /^[A-Za-z]\d[A-Za-z]/;

/**
 * The early-access list, as a short guided form rather than a name, an email and a box.
 *
 * Sign-up is invitation-only for now, so this is how we decide who to invite first and what
 * to ask them for. It collects enough to do that — roughly where, what kind of care, how
 * soon — and deliberately nothing sensitive: no street address, no names of the people who
 * need care, no health detail. Those belong inside the account, after an invitation.
 *
 * The answers travel as a labelled summary in the enquiry's message, so staff read exactly
 * what the person chose in the admin enquiries list with no separate store to keep in step.
 */
export function EarlyAccessForm() {
  const asParam = useSearchParams().get("as");
  const [role, setRole] = useState<Role>(asParam === "caregiver" ? "Caregiver" : "Family");
  const [step, setStep] = useState(0);
  const [status, setStatus] = useState<Status>("idle");
  const [error, setError] = useState<string | null>(null);
  const [stepError, setStepError] = useState<string | null>(null);

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [postal, setPostal] = useState("");
  const [who, setWho] = useState<string[]>([]);
  const [count, setCount] = useState("1");
  const [care, setCare] = useState<string[]>([]);
  const [start, setStart] = useState("");
  const [schedule, setSchedule] = useState<string[]>([]);
  const [experience, setExperience] = useState("");
  const [caredFor, setCaredFor] = useState<string[]>([]);
  const [credentials, setCredentials] = useState<string[]>([]);
  const [checks, setChecks] = useState("");
  const [insurance, setInsurance] = useState("");
  const [transport, setTransport] = useState("");
  const [languages, setLanguages] = useState("");
  const [note, setNote] = useState("");
  const [consent, setConsent] = useState(false);
  const trap = useRef<HTMLInputElement>(null);

  const steps =
    role === "Family"
      ? ["About you", "Who needs care", "What help", "When", "Finish"]
      : ["About you", "What you offer", "Credentials", "Availability", "Finish"];
  const last = steps.length - 1;

  const toggle = (list: string[], set: (next: string[]) => void, value: string) =>
    set(list.includes(value) ? list.filter((v) => v !== value) : [...list, value]);

  /** Why the current step cannot move on yet, or null. */
  function problem(): string | null {
    if (step === 0) {
      if (name.trim().length < 2) return "Please tell us your name.";
      if (!/^\S+@\S+\.\S+$/.test(email.trim())) return "Please enter an email address we can reach you on.";
      if (!POSTAL_START.test(postal.trim())) return "Please enter the first three characters of your postal code, like M5V.";
    }
    if (step === 1 && role === "Family" && who.length === 0) return "Choose who the care is for.";
    if (step === 1 && role === "Caregiver" && care.length === 0) return "Choose at least one kind of care you offer.";
    if (step === 2 && role === "Family" && care.length === 0) return "Choose at least one kind of help.";
    if (step === 3 && role === "Family" && !start) return "Let us know how soon you need care.";
    return null;
  }

  function next() {
    const issue = problem();
    setStepError(issue);
    if (!issue) setStep((s) => Math.min(s + 1, last));
  }

  function summary(): string {
    const lines: string[] = [`Early access: ${role === "Family" ? "family" : "caregiver"}`, `Area: ${postal.trim().toUpperCase().slice(0, 3)}`];

    if (role === "Family") {
      lines.push(`Care for: ${who.join(", ")}${who.length ? ` (${count === "3" ? "3 or more" : count})` : ""}`);
      lines.push(`Help wanted: ${care.join(", ")}`);
      lines.push(`Start: ${start}`);
      if (schedule.length) lines.push(`Schedule: ${schedule.join(", ")}`);
    } else {
      lines.push(`Offers: ${care.join(", ")}`);
      if (experience) lines.push(`Experience: ${experience}`);
      if (caredFor.length) lines.push(`Has cared for: ${caredFor.join(", ")}`);
      if (credentials.length) lines.push(`Credentials: ${credentials.join(", ")}`);
      if (checks) lines.push(`Identity and vulnerable sector check: ${checks}`);
      if (insurance) lines.push(`Own insurance: ${insurance}`);
      if (transport) lines.push(`Transport: ${transport}`);
      if (languages.trim()) lines.push(`Languages: ${languages.trim()}`);
      if (schedule.length) lines.push(`Available: ${schedule.join(", ")}`);
    }

    if (note.trim()) lines.push(`Note: ${note.trim()}`);
    return lines.join("\n").slice(0, 2000);
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (step < last) {
      next();
      return;
    }

    if (!consent) {
      setStepError("Please agree to be contacted so we can reply.");
      return;
    }

    setStatus("sending");
    setError(null);
    setStepError(null);

    try {
      const response = await fetch(`${PUBLIC_API_BASE_URL}/api/v1/enquiries`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: name.trim(),
          email: email.trim(),
          kind: role,
          message: summary(),
          consentGiven: true,
          website: trap.current?.value ?? "",
        }),
      });

      if (response.status === 202) {
        setStatus("sent");
        return;
      }

      setError(
        response.status === 429
          ? "You have sent a few already. Please try again later."
          : response.status === 400
            ? "Something in the form needs another look. Please go back and check it."
            : "We could not send this. Please try again in a few minutes.",
      );
    } catch {
      setError("We could not reach GoSit. Check your connection and try again.");
    }

    setStatus("error");
  }

  if (status === "sent") {
    return (
      <div role="status" className="rounded-card border border-brand-soft-border bg-brand-soft p-8 text-center">
        <h3 className="font-heading text-2xl font-semibold text-text-primary">You are on the list</h3>
        <p className="mt-3 text-text-secondary">
          {role === "Family"
            ? "Thank you. We will email you the moment accounts open in your area, with the first steps to find a caregiver."
            : "Thank you. We will email you when accounts open, with how identity and background checks work, so you can get started right away."}
        </p>
        <p className="mt-3 text-sm text-text-secondary">
          We have not added you to a mailing list, and we will only use these details to reply to you.
        </p>
      </div>
    );
  }

  const sending = status === "sending";

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-6">
      <div role="group" aria-label="I am" className="grid grid-cols-2 gap-2 rounded-control bg-surface-muted p-1">
        {(["Family", "Caregiver"] as const).map((option) => (
          <button
            key={option}
            type="button"
            aria-pressed={role === option}
            onClick={() => {
              setRole(option);
              setStep(0);
              setCare([]);
              setSchedule([]);
              setStepError(null);
            }}
            className={`rounded-control px-4 py-2.5 text-sm font-semibold transition ${
              role === option ? "bg-brand-primary text-text-on-brand shadow-sm" : "text-text-secondary hover:text-text-primary"
            }`}
          >
            {option === "Family" ? "I need a caregiver" : "I am a caregiver"}
          </button>
        ))}
      </div>

      <div>
        <div className="flex items-center justify-between text-sm">
          <span className="font-semibold text-text-primary">{steps[step]}</span>
          <span className="text-text-secondary">
            Step {step + 1} of {steps.length}
          </span>
        </div>
        <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-surface-muted" aria-hidden="true">
          <div className="h-full rounded-full bg-brand-primary transition-all" style={{ width: `${((step + 1) / steps.length) * 100}%` }} />
        </div>
      </div>

      {error ? (
        <p role="alert" className="rounded-control border border-danger-accent/20 bg-danger-soft px-4 py-3 text-sm text-danger-accent">
          {error}
        </p>
      ) : null}

      {step === 0 ? (
        <div className="flex flex-col gap-5">
          <div className="grid gap-5 sm:grid-cols-2">
            <TextInput label="Your name" name="name" autoComplete="name" value={name} onChange={(e) => setName(e.target.value)} maxLength={120} />
            <TextInput label="Email address" name="email" type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} maxLength={256} />
          </div>
          <TextInput
            label="Postal code (first three characters)"
            name="postal"
            autoComplete="postal-code"
            placeholder="M5V"
            value={postal}
            onChange={(e) => setPostal(e.target.value.slice(0, 7))}
            maxLength={7}
          />
          <p className="text-sm text-text-secondary">
            Just the area, never your street address. It tells us where to open first.
          </p>
        </div>
      ) : null}

      {role === "Family" && step === 1 ? (
        <div className="flex flex-col gap-5">
          <Field label="Who is the care for? Choose all that apply.">
            <Chips options={WHO} selected={who} onToggle={(v) => toggle(who, setWho, v)} />
          </Field>
          <div>
            <label htmlFor="count" className="mb-1.5 block font-control text-sm font-medium text-text-secondary">
              How many people or pets?
            </label>
            <select id="count" value={count} onChange={(e) => setCount(e.target.value)} className={inputClassName}>
              <option value="1">1</option>
              <option value="2">2</option>
              <option value="3">3 or more</option>
            </select>
          </div>
          <Privacy />
        </div>
      ) : null}

      {role === "Family" && step === 2 ? (
        <Field label="What kind of help are you looking for?">
          <Chips
            options={Array.from(new Set(who.flatMap((w) => CARE_BY_WHO[w] ?? [])))}
            selected={care}
            onToggle={(v) => toggle(care, setCare, v)}
          />
        </Field>
      ) : null}

      {role === "Family" && step === 3 ? (
        <div className="flex flex-col gap-5">
          <Field label="How soon do you need care?">
            <Chips options={START} selected={start ? [start] : []} onToggle={(v) => setStart(v)} />
          </Field>
          <Field label="What does the schedule look like? Choose any.">
            <Chips options={SCHEDULE} selected={schedule} onToggle={(v) => toggle(schedule, setSchedule, v)} />
          </Field>
        </div>
      ) : null}

      {role === "Caregiver" && step === 1 ? (
        <div className="flex flex-col gap-5">
          <Field label="What care can you offer? Choose all that apply.">
            <Chips options={CAREGIVER_SERVICES} selected={care} onToggle={(v) => toggle(care, setCare, v)} />
          </Field>
          <Field label="Who have you cared for?">
            <Chips options={CARED_FOR} selected={caredFor} onToggle={(v) => toggle(caredFor, setCaredFor, v)} />
          </Field>
          <Field label="Years of experience">
            <Chips options={EXPERIENCE} selected={experience ? [experience] : []} onToggle={(v) => setExperience(v)} />
          </Field>
        </div>
      ) : null}

      {role === "Caregiver" && step === 2 ? (
        <div className="flex flex-col gap-5">
          <Field label="Certifications you hold">
            <Chips options={CREDENTIALS} selected={credentials} onToggle={(v) => toggle(credentials, setCredentials, v)} />
          </Field>
          <Field label="Everyone on GoSit completes identity verification, and care for people also needs a vulnerable sector check. Are you comfortable with that?">
            <Chips options={CHECKS} selected={checks ? [checks] : []} onToggle={(v) => setChecks(v)} />
          </Field>
          <Field label="Caregivers on GoSit are independent and carry their own insurance. Where are you with that?">
            <Chips options={INSURANCE} selected={insurance ? [insurance] : []} onToggle={(v) => setInsurance(v)} />
          </Field>
          <Field label="How do you get around?">
            <Chips options={TRANSPORT} selected={transport ? [transport] : []} onToggle={(v) => setTransport(v)} />
          </Field>
        </div>
      ) : null}

      {role === "Caregiver" && step === 3 ? (
        <div className="flex flex-col gap-5">
          <Field label="When are you usually available? Choose any.">
            <Chips options={SCHEDULE} selected={schedule} onToggle={(v) => toggle(schedule, setSchedule, v)} />
          </Field>
          <TextInput label="Languages you speak (optional)" name="languages" value={languages} onChange={(e) => setLanguages(e.target.value)} maxLength={120} />
        </div>
      ) : null}

      {step === last ? (
        <div className="flex flex-col gap-5">
          <div className="rounded-control border border-brand-soft-border bg-brand-soft p-4 text-sm text-text-secondary">
            <p className="mb-2 font-semibold text-text-primary">Here is what we will receive</p>
            <pre className="whitespace-pre-wrap font-sans">{summary()}</pre>
          </div>
          <div>
            <label htmlFor="note" className="mb-1.5 block font-control text-sm font-medium text-text-secondary">
              Anything else we should know? (optional)
            </label>
            <textarea id="note" rows={3} maxLength={500} value={note} onChange={(e) => setNote(e.target.value)} className={textareaClassName} />
          </div>
          {role === "Family" ? <Privacy /> : null}
          <label className="flex items-start gap-3 text-sm text-text-secondary">
            <input type="checkbox" checked={consent} onChange={(e) => setConsent(e.target.checked)} className="mt-1 size-4 accent-brand-primary" />
            <span>
              I agree that GoSit may use these details to reply to me. We do not add anyone to a mailing list.
            </span>
          </label>
        </div>
      ) : null}

      {/* A field no person can see. Anything that fills it is a script. */}
      <div aria-hidden="true" className="absolute -left-[9999px] h-0 w-0 overflow-hidden">
        <label>
          Website
          <input ref={trap} type="text" name="website" tabIndex={-1} autoComplete="off" />
        </label>
      </div>

      {stepError ? (
        <p role="alert" className="text-sm text-danger-accent">
          {stepError}
        </p>
      ) : null}

      <div className="flex items-center gap-3">
        {step > 0 ? (
          <Button type="button" variant="secondary" size="lg" onClick={() => { setStepError(null); setStep(step - 1); }}>
            Back
          </Button>
        ) : null}
        <Button type="submit" size="lg" disabled={sending} className="flex-1 sm:flex-none">
          {step < last ? "Continue" : sending ? "Sending…" : "Join the early list"}
        </Button>
      </div>
    </form>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <fieldset className="min-w-0">
      <legend className="mb-2.5 font-control text-sm font-medium text-text-secondary">{label}</legend>
      {children}
    </fieldset>
  );
}

function Chips({
  options,
  selected,
  onToggle,
}: {
  options: string[];
  selected: string[];
  onToggle: (value: string) => void;
}) {
  return (
    <div className="flex flex-wrap gap-2">
      {options.map((option) => {
        const on = selected.includes(option);

        return (
          <button
            key={option}
            type="button"
            aria-pressed={on}
            onClick={() => onToggle(option)}
            className={`rounded-full border px-3.5 py-2 text-sm transition ${
              on
                ? "border-brand-primary bg-brand-primary text-text-on-brand"
                : "border-border-strong text-text-secondary hover:border-brand-primary hover:text-text-primary"
            }`}
          >
            {option}
          </button>
        );
      })}
    </div>
  );
}

function Privacy() {
  return (
    <p className="text-sm text-text-secondary">
      Please do not include names, health details or your home address here. You can share those privately,
      inside your account, once you are invited.
    </p>
  );
}
