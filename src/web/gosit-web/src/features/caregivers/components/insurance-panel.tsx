"use client";

import { useActionState, useState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import {
  submitInsuranceAction,
  uploadInsuranceDocumentAction,
  type CaregiverFormState,
} from "@/features/caregivers/actions";
import {
  INSURANCE_COVERAGE_LABELS,
  type CaregiverInsurancePolicy,
  type InsuranceCoverageType,
} from "@/features/caregivers/types";

/**
 * Caregiver insurance: submitted by the caregiver, verified by GoSit, never issued or
 * held by GoSit — see docs/BUSINESS_MODEL.md. Mirrors CertificationsPanel closely, plus
 * the document upload certifications do not have yet: an insurance policy cannot be
 * approved without proof, so this is the one place a caregiver uploads a file.
 */
export function InsurancePanel({ policies }: { policies: CaregiverInsurancePolicy[] }) {
  const [isAdding, setIsAdding] = useState(policies.length === 0);

  return (
    <div className="flex flex-col gap-4">
      {policies.length === 0 ? (
        <p className="text-sm text-text-secondary">
          Nothing on file yet. Some services need proof of your own insurance before you
          can be booked for them.
        </p>
      ) : (
        <ul className="flex flex-col gap-3">
          {policies.map((policy) => (
            <PolicyRow key={policy.id} policy={policy} />
          ))}
        </ul>
      )}

      {isAdding ? (
        <AddInsuranceForm onDone={() => setIsAdding(false)} />
      ) : (
        <div>
          <Button type="button" variant="ghost" onClick={() => setIsAdding(true)}>
            Add a policy
          </Button>
        </div>
      )}
    </div>
  );
}

function PolicyRow({ policy }: { policy: CaregiverInsurancePolicy }) {
  const [isReplacing, setIsReplacing] = useState(false);

  return (
    <li className="rounded-card border border-border bg-surface p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-text-primary">{INSURANCE_COVERAGE_LABELS[policy.coverageType]}</p>
          <p className="mt-0.5 text-sm text-text-secondary">
            {policy.insurer} · Policy {policy.policyReference}
          </p>
          <p className="mt-0.5 text-sm text-text-secondary">
            ${policy.liabilityLimit.toLocaleString("en-CA")} liability · expires{" "}
            {formatMonth(policy.expiresOn)}
          </p>
        </div>

        <Status policy={policy} />
      </div>

      {policy.status === "Rejected" ? (
        <p className="mt-2 text-sm text-danger-accent">
          This was not accepted. Upload a current declaration page to have it looked at
          again.
        </p>
      ) : null}

      <div className="mt-3">
        {isReplacing ? (
          <UploadForm
            caregiverInsurancePolicyId={policy.id}
            onDone={() => setIsReplacing(false)}
          />
        ) : (
          <Button type="button" variant="ghost" size="sm" onClick={() => setIsReplacing(true)}>
            {policy.status === "Pending" ? "Upload proof of insurance" : "Replace document"}
          </Button>
        )}
      </div>
    </li>
  );
}

function Status({ policy }: { policy: CaregiverInsurancePolicy }) {
  if (policy.isValid) {
    return (
      <span className="inline-flex items-center rounded-pill bg-brand-primary/10 px-3 py-1 text-xs font-semibold text-brand-primary">
        Verified
      </span>
    );
  }

  if (policy.status === "Rejected") {
    return (
      <span className="inline-flex items-center rounded-pill bg-danger-accent/10 px-3 py-1 text-xs font-semibold text-danger-accent">
        Not accepted
      </span>
    );
  }

  if (policy.status === "Verified") {
    // Verified but not currently valid means it has since expired.
    return (
      <span className="inline-flex items-center rounded-pill bg-danger-accent/10 px-3 py-1 text-xs font-semibold text-danger-accent">
        Expired
      </span>
    );
  }

  return (
    <span className="inline-flex items-center rounded-pill bg-brand-accent/15 px-3 py-1 text-xs font-semibold text-text-primary">
      In review
    </span>
  );
}

function formatMonth(isoDate: string): string {
  const [year, month, day] = isoDate.slice(0, 10).split("-").map(Number);

  return new Date(year, month - 1, day).toLocaleDateString("en-CA", {
    month: "short",
    year: "numeric",
  });
}

function AddInsuranceForm({ onDone }: { onDone: () => void }) {
  const [state, formAction] = useActionState<CaregiverFormState, FormData>(
    async (previous, formData) => {
      const result = await submitInsuranceAction(previous, formData);

      if (result.saved) {
        onDone();
      }

      return result;
    },
    {},
  );

  const today = new Date().toISOString().slice(0, 10);
  const coverageTypes = Object.keys(INSURANCE_COVERAGE_LABELS) as InsuranceCoverageType[];

  return (
    <form
      action={formAction}
      className="flex flex-col gap-4 rounded-card border border-border bg-surface p-4"
      noValidate
    >
      <h3 className="font-heading text-base font-medium text-text-primary">Add a policy</h3>

      {state.error ? (
        <p role="alert" className="rounded-control bg-red-50 px-4 py-3 text-sm text-red-800">
          {state.error}
        </p>
      ) : null}

      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Coverage type" htmlFor="coverageType" error={state.fieldErrors?.coverageType}>
          <select
            id="coverageType"
            name="coverageType"
            required
            className="mt-1 h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          >
            {coverageTypes.map((type) => (
              <option key={type} value={type}>
                {INSURANCE_COVERAGE_LABELS[type]}
              </option>
            ))}
          </select>
        </Field>

        <Field label="Insurer" htmlFor="insurer" error={state.fieldErrors?.insurer}>
          <input
            id="insurer"
            name="insurer"
            type="text"
            required
            maxLength={200}
            placeholder="Aviva Canada"
            className="mt-1 h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          />
        </Field>

        <Field label="Policy number" htmlFor="policyReference" error={state.fieldErrors?.policyReference}>
          <input
            id="policyReference"
            name="policyReference"
            type="text"
            required
            maxLength={100}
            className="mt-1 h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          />
        </Field>

        <Field label="Liability limit (CAD)" htmlFor="liabilityLimit" error={state.fieldErrors?.liabilityLimit}>
          <input
            id="liabilityLimit"
            name="liabilityLimit"
            type="number"
            min="1"
            step="1"
            required
            placeholder="2000000"
            className="mt-1 h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          />
        </Field>

        <Field label="Effective from" htmlFor="effectiveOn" error={state.fieldErrors?.effectiveOn}>
          <input
            id="effectiveOn"
            name="effectiveOn"
            type="date"
            required
            max={today}
            className="mt-1 h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          />
        </Field>

        <Field label="Expires" htmlFor="expiresOn" error={state.fieldErrors?.expiresOn}>
          <input
            id="expiresOn"
            name="expiresOn"
            type="date"
            required
            min={today}
            className="mt-1 h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          />
        </Field>
      </div>

      <p className="text-sm text-text-secondary">
        Add the policy first, then upload a photo or PDF of the declaration page — our
        team checks it before it counts.
      </p>

      <div className="flex gap-2">
        <SubmitButton />
        <Button type="button" variant="ghost" onClick={onDone}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

function UploadForm({
  caregiverInsurancePolicyId,
  onDone,
}: {
  caregiverInsurancePolicyId: string;
  onDone: () => void;
}) {
  const action = uploadInsuranceDocumentAction.bind(null, caregiverInsurancePolicyId);

  const [state, formAction] = useActionState<CaregiverFormState, FormData>(
    async (previous, formData) => {
      const result = await action(previous, formData);

      if (result.saved) {
        onDone();
      }

      return result;
    },
    {},
  );

  return (
    <form action={formAction} className="flex flex-col gap-2">
      <input
        type="file"
        name="file"
        required
        accept="image/jpeg,image/png,image/heic,image/webp,application/pdf"
        className="text-sm text-text-primary file:mr-3 file:rounded-control file:border-0 file:bg-surface-muted file:px-3 file:py-2 file:font-control file:text-sm file:font-semibold file:text-text-primary"
      />

      {state.error ? <p className="text-sm text-red-700">{state.error}</p> : null}

      <div className="flex gap-2">
        <UploadButton />
        <Button type="button" variant="ghost" size="sm" onClick={onDone}>
          Cancel
        </Button>
      </div>
    </form>
  );
}

function Field({
  label,
  htmlFor,
  error,
  children,
}: {
  label: string;
  htmlFor: string;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <div>
      <label htmlFor={htmlFor} className="font-control text-sm text-text-secondary">
        {label}
      </label>
      {children}
      {error ? <p className="mt-1 text-sm text-red-700">{error}</p> : null}
    </div>
  );
}

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" disabled={pending}>
      {pending ? "Submitting…" : "Submit"}
    </Button>
  );
}

function UploadButton() {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" size="sm" disabled={pending}>
      {pending ? "Uploading…" : "Upload"}
    </Button>
  );
}
