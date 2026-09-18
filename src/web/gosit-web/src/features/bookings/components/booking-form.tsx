"use client";

import { useActionState, useState } from "react";
import { useFormStatus } from "react-dom";
import { Button } from "@/components/ui/button";
import type { Pet } from "@/features/account/types";
import { createBookingAction, type BookingFormState } from "@/features/bookings/actions";
import { formatMoney } from "@/features/bookings/components/booking-status";
import { describePet, isPetCareService, type CareRecipientSummary } from "@/features/bookings/types";
import type { CareServiceSummary } from "@/features/search/types";

/**
 * Requests care from a chosen caregiver.
 *
 * The client sends no price. The estimate shown here is presentational only — the server
 * computes the real total from the caregiver's stored rate, so a tampered form changes
 * what is displayed and nothing else.
 */
export function BookingForm({
  caregiverProfileId,
  caregiverName,
  hourlyRate,
  nightlyRate,
  termsByService,
  services,
  recipients,
  pets,
  petSpeciesByService,
}: {
  caregiverProfileId: string;
  caregiverName: string;
  hourlyRate: number;
  nightlyRate: number | null;
  /** How each service is priced and where it happens, from the catalogue. */
  termsByService: Record<string, { pricingUnit: string; careLocation: string }>;
  services: CareServiceSummary[];
  recipients: CareRecipientSummary[];
  pets: Pet[];
  /** Which kinds of animal each pet service is for, from the catalogue. */
  petSpeciesByService: Record<string, string[]>;
}) {
  const [state, formAction] = useActionState<BookingFormState, FormData>(
    createBookingAction,
    {},
  );

  const [serviceCode, setServiceCode] = useState(services[0]?.code ?? "");

  const selectedService = services.find((service) => service.code === serviceCode);
  const selectedIsPetCare = selectedService ? isPetCareService(selectedService) : false;

  // A dog walk is offered for dogs only; the API refuses anything else, so it is not offered.
  const eligiblePets = pets.filter((pet) =>
    (petSpeciesByService[serviceCode] ?? []).includes(pet.species),
  );

  const [startTime, setStartTime] = useState("09:00");
  const [endTime, setEndTime] = useState("13:00");

  const selectedIsNightly = termsByService[serviceCode]?.pricingUnit === "Nightly";
  const selectedAtCaregiverHome = termsByService[serviceCode]?.careLocation === "CaregiverHome";

  const [date, setDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const nights = nightsBetween(date, endDate);

  const hours = durationHours(startTime, endTime);

  return (
    <form action={formAction} className="flex flex-col gap-6" noValidate>
      <input type="hidden" name="caregiverProfileId" value={caregiverProfileId} />

      {state.error ? (
        <p role="alert" className="rounded-control bg-red-50 px-4 py-3 text-sm text-red-800">
          {state.error}
        </p>
      ) : null}

      <Field
        label="Type of care"
        htmlFor="careServiceCode"
        error={state.fieldErrors?.careServiceCode}
      >
        <select
          id="careServiceCode"
          name="careServiceCode"
          required
          value={serviceCode}
          onChange={(event) => setServiceCode(event.target.value)}
          className="h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
        >
          {services.map((service) => (
            <option key={service.code} value={service.code}>
              {service.name}
            </option>
          ))}
        </select>
        <p className="mt-1 text-sm text-text-secondary">
          Only what {caregiverName} offers and is certified for.
        </p>
      </Field>

      {selectedIsPetCare ? (
        <fieldset>
          <legend className="font-control text-sm text-text-secondary">Which pets?</legend>

          {eligiblePets.length > 0 ? (
            <ul className="mt-2 flex flex-col gap-2">
              {eligiblePets.map((pet) => (
                <li key={pet.id}>
                  <label className="flex cursor-pointer items-center gap-3 rounded-control border border-border p-3 has-[:checked]:border-brand-primary has-[:checked]:bg-brand-primary/5">
                    <input
                      type="checkbox"
                      name="petIds"
                      value={pet.id}
                      defaultChecked={eligiblePets.length === 1}
                      className="size-4 accent-[var(--color-brand-primary)]"
                    />
                    <span className="text-text-primary">{describePet(pet)}</span>
                  </label>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-2 text-sm text-text-secondary">
              None of your pets can be booked for {selectedService?.name.toLowerCase()}.
            </p>
          )}

          {state.fieldErrors?.careRecipientIds ? (
            <p className="mt-1 text-sm text-red-700">{state.fieldErrors.careRecipientIds}</p>
          ) : null}
        </fieldset>
      ) : (
        <fieldset>
          <legend className="font-control text-sm text-text-secondary">Who is the care for?</legend>

          <ul className="mt-2 flex flex-col gap-2">
            {recipients.map((recipient) => (
              <li key={recipient.id}>
                <label className="flex cursor-pointer items-center gap-3 rounded-control border border-border p-3 has-[:checked]:border-brand-primary has-[:checked]:bg-brand-primary/5">
                  <input
                    type="checkbox"
                    name="careRecipientIds"
                    value={recipient.id}
                    defaultChecked={recipients.length === 1}
                    className="size-4 accent-[var(--color-brand-primary)]"
                  />
                  <span className="text-text-primary">
                    {recipient.firstName}
                    <span className="text-text-secondary">
                      {" "}
                      · {recipient.age} · {recipient.recipientType}
                    </span>
                  </span>

                  {/* Whether needs exist, never what they say. */}
                  {recipient.hasRecordedCareNeeds ? (
                    <span className="ml-auto text-xs text-text-secondary">Care needs on file</span>
                  ) : null}
                </label>
              </li>
            ))}
          </ul>

          {state.fieldErrors?.careRecipientIds ? (
            <p className="mt-1 text-sm text-red-700">{state.fieldErrors.careRecipientIds}</p>
          ) : null}
        </fieldset>
      )}

      {selectedIsNightly ? (
        // A stay — boarding, or a sitter staying over — runs across days and is priced per night.
        <div className="grid gap-4 sm:grid-cols-2">
          <Field
            label={selectedAtCaregiverHome ? "Drop-off date" : "Sitter arrives on"}
            htmlFor="date"
            error={state.fieldErrors?.date}
          >
            <input
              id="date"
              name="date"
              type="date"
              required
              value={date}
              onChange={(event) => setDate(event.target.value)}
              min={new Date().toISOString().slice(0, 10)}
              className="h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
            />
          </Field>

          <Field
            label={selectedAtCaregiverHome ? "Drop-off time" : "Arrives at"}
            htmlFor="startTime"
            error={state.fieldErrors?.startTime}
          >
            <input
              id="startTime"
              name="startTime"
              type="time"
              required
              value={startTime}
              onChange={(event) => setStartTime(event.target.value)}
              className="h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
            />
          </Field>

          <Field
            label={selectedAtCaregiverHome ? "Pick-up date" : "Sitter leaves on"}
            htmlFor="endDate"
            error={state.fieldErrors?.endDate}
          >
            <input
              id="endDate"
              name="endDate"
              type="date"
              required
              value={endDate}
              onChange={(event) => setEndDate(event.target.value)}
              min={date || new Date().toISOString().slice(0, 10)}
              className="h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
            />
          </Field>

          <Field
            label={selectedAtCaregiverHome ? "Pick-up time" : "Leaves at"}
            htmlFor="endTime"
            error={state.fieldErrors?.endTime}
          >
            <input
              id="endTime"
              name="endTime"
              type="time"
              required
              value={endTime}
              onChange={(event) => setEndTime(event.target.value)}
              className="h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
            />
          </Field>
        </div>
      ) : (
      <div className="grid gap-4 sm:grid-cols-3">
        <Field label="Date" htmlFor="date" error={state.fieldErrors?.date}>
          <input
            id="date"
            name="date"
            type="date"
            required
            value={date}
            onChange={(event) => setDate(event.target.value)}
            // The API needs at least two hours' notice, so today is the earliest a
            // booking could ever be valid.
            min={new Date().toISOString().slice(0, 10)}
            className="h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          />
        </Field>

        <Field label="Start" htmlFor="startTime" error={state.fieldErrors?.startTime}>
          <input
            id="startTime"
            name="startTime"
            type="time"
            required
            value={startTime}
            onChange={(event) => setStartTime(event.target.value)}
            className="h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          />
        </Field>

        <Field label="End" htmlFor="endTime" error={state.fieldErrors?.endTime}>
          <input
            id="endTime"
            name="endTime"
            type="time"
            required
            value={endTime}
            onChange={(event) => setEndTime(event.target.value)}
            className="h-12 w-full rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          />
        </Field>
      </div>
      )}

      <Field label="Anything the caregiver should know?" htmlFor="instructions">
        <textarea
          id="instructions"
          name="instructions"
          rows={4}
          maxLength={2000}
          placeholder={
            selectedIsPetCare
              ? "Lead is on the hook by the door. The spare key is with the neighbour."
              : "Bedtime is 8pm. The spare key is with the neighbour."
          }
          className="w-full rounded-control border border-border bg-surface p-3 font-control text-sm text-text-primary"
        />
        {/* True since the API stopped sending notes with a request; worth saying where a door code gets typed. */}
        <p className="mt-1 text-sm text-text-secondary">
          {selectedAtCaregiverHome
            ? `${caregiverName} sees these once they accept, and you see their home address then.`
            : `${caregiverName} sees these, and your address, only once they accept.`}
        </p>
      </Field>

      <div className="rounded-card bg-surface-muted p-4">
        {selectedIsNightly && nightlyRate !== null ? (
          <>
            <div className="flex items-center justify-between text-sm">
              <span className="text-text-secondary">
                {formatMoney(nightlyRate)}/night × {nights} {nights === 1 ? "night" : "nights"}
              </span>
              <span className="font-heading text-lg font-medium text-text-primary">
                {formatMoney(nightlyRate * nights)}
              </span>
            </div>
            <p className="mt-2 text-sm text-text-secondary">
              An estimate. You are charged for the nights the stay actually lasts, and
              nothing is taken until {caregiverName} confirms.
            </p>
          </>
        ) : (
        <>
        <div className="flex items-center justify-between text-sm">
          <span className="text-text-secondary">
            {formatMoney(hourlyRate)}/hr × {hours > 0 ? hours.toFixed(hours % 1 === 0 ? 0 : 1) : "0"}{" "}
            {hours === 1 ? "hour" : "hours"}
          </span>
          <span className="font-heading text-lg font-medium text-text-primary">
            {formatMoney(hours > 0 ? hourlyRate * hours : 0)}
          </span>
        </div>
        <p className="mt-2 text-sm text-text-secondary">
          An estimate. You are charged for the time actually worked, and nothing is taken
          until {caregiverName} confirms.
        </p>
        </>
        )}
      </div>

      <SubmitButton caregiverName={caregiverName} />
    </form>
  );
}

function SubmitButton({ caregiverName }: { caregiverName: string }) {
  const { pending } = useFormStatus();

  return (
    <Button type="submit" disabled={pending} className="h-12 w-full text-base">
      {pending ? "Sending request…" : `Request booking with ${caregiverName}`}
    </Button>
  );
}

function Field({
  label,
  htmlFor,
  error,
  children,
}: {
  label: string;
  htmlFor?: string;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <div>
      <label htmlFor={htmlFor} className="font-control text-sm text-text-secondary">
        {label}
      </label>
      <div className="mt-1">{children}</div>
      {error ? <p className="mt-1 text-sm text-red-700">{error}</p> : null}
    </div>
  );
}

/** Hours between two "HH:mm" values, or 0 if the window is not yet valid. */
/**
 * Hours between two wall-clock times, wrapping past midnight.
 *
 * An end earlier in the day than the start is an overnight, not a mistake: 20:00 to
 * 06:00 is ten hours. This used to return 0 for that case, so the estimate read "$0.00"
 * on exactly the booking a family was most likely to be checking the price of.
 */
function durationHours(start: string, end: string): number {
  const toMinutes = (value: string) => {
    const [hours, minutes] = value.split(":").map(Number);
    return Number.isFinite(hours) && Number.isFinite(minutes) ? hours * 60 + minutes : NaN;
  };

  const from = toMinutes(start);
  const to = toMinutes(end);

  if (!Number.isFinite(from) || !Number.isFinite(to) || from === to) {
    return 0;
  }

  const minutes = to > from ? to - from : to - from + 24 * 60;

  return minutes / 60;
}

/** Nights between two "YYYY-MM-DD" dates, or 0 until both are chosen and in order. */
function nightsBetween(start: string, end: string): number {
  if (!start || !end) {
    return 0;
  }

  const toUtc = (value: string) => {
    const [year, month, day] = value.split("-").map(Number);
    return Date.UTC(year, month - 1, day);
  };

  const days = Math.round((toUtc(end) - toUtc(start)) / 86_400_000);

  return days > 0 ? days : 0;
}
