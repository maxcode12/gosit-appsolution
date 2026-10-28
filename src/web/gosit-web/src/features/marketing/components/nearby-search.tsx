"use client";

import Link from "next/link";
import { useEffect, useId, useRef, useState, type FormEvent } from "react";
import { buttonClassName } from "@/components/ui/button";
import { ArrowRightIcon, PinIcon } from "@/components/ui/marketing-icons";
import { PUBLIC_API_BASE_URL, getStartedHref, type Audience } from "@/features/marketing/public-mode";

type Area = { code: string; name: string; province: string };
type Overview = {
  area: Area;
  radiusKm: number;
  caregiverCount: number;
  categories: { code: string; name: string; count: number }[];
};

type State =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "found"; overview: Overview }
  | { status: "unknown" }
  | { status: "error" };

const RADII = [10, 25, 50];

/** "Downtown Toronto (CN Tower / King and Spadina / …)" is a lot; the first part says where. */
function shortName(name: string) {
  const open = name.indexOf("(");
  return (open > 0 ? name.slice(0, open) : name).trim();
}

/**
 * "How many caregivers are near me?" for anyone, signed in or not.
 *
 * It asks the API for a count and a place name, and shows nothing else: who the caregivers
 * are stays behind sign-in. Typing a postal code, part of one, or a city all work, and the
 * suggestions come from postal-area data held on our own servers, so nothing here depends
 * on a paid mapping service.
 *
 * When the answer is none, it says so honestly and turns the visit into an early-list
 * sign-up for that area, which is more useful to us and to them than a zero.
 */
export function NearbySearch({ audience = "family" }: { audience?: Audience }) {
  const inputId = useId();
  const listId = useId();
  const [text, setText] = useState("");
  const [radiusKm, setRadiusKm] = useState(25);
  const [suggestions, setSuggestions] = useState<Area[]>([]);
  const [state, setState] = useState<State>({ status: "idle" });
  const latest = useRef(0);

  // Suggestions follow the typing, a beat behind it, and a slow answer never overwrites a
  // newer one.
  useEffect(() => {
    const typed = text.trim();

    if (typed.length < 2) {
      return;
    }

    const controller = new AbortController();
    const timer = setTimeout(async () => {
      try {
        const response = await fetch(
          `${PUBLIC_API_BASE_URL}/api/v1/areas/suggest?q=${encodeURIComponent(typed)}`,
          { signal: controller.signal },
        );

        if (response.ok) {
          setSuggestions((await response.json()).areas ?? []);
        }
      } catch {
        // Suggestions are a convenience; their absence is not an error worth showing.
      }
    }, 220);

    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [text]);

  async function look(query: string, radius: number) {
    const ticket = ++latest.current;
    setState({ status: "loading" });

    try {
      const response = await fetch(
        `${PUBLIC_API_BASE_URL}/api/v1/areas/overview?query=${encodeURIComponent(query)}&radiusKm=${radius}`,
      );

      if (ticket !== latest.current) {
        return;
      }

      if (response.status === 404) {
        setState({ status: "unknown" });
      } else if (response.ok) {
        setState({ status: "found", overview: await response.json() });
      } else {
        setState({ status: "error" });
      }
    } catch {
      if (ticket === latest.current) {
        setState({ status: "error" });
      }
    }
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault();

    if (text.trim().length > 0) {
      void look(text.trim(), radiusKm);
    }
  }

  function changeRadius(radius: number) {
    setRadiusKm(radius);

    if (state.status === "found") {
      void look(state.overview.area.code, radius);
    }
  }

  const isCaregiver = audience === "caregiver";

  return (
    <div className="rounded-[2rem] border border-border bg-surface p-5 shadow-lg sm:p-8">
      <form onSubmit={onSubmit} className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <div className="flex-1">
          <label htmlFor={inputId} className="font-control text-sm font-semibold text-text-primary">
            {isCaregiver ? "Where do you want to work?" : "Where do you need care?"}
          </label>
          <div className="relative mt-2">
            <PinIcon className="pointer-events-none absolute left-4 top-1/2 size-5 -translate-y-1/2 text-text-muted" />
            <input
              id={inputId}
              list={listId}
              value={text}
              onChange={(event) => setText(event.target.value)}
              autoComplete="postal-code"
              placeholder="Postal code or city, like M5V or Ottawa"
              maxLength={40}
              className="h-14 w-full rounded-full border border-border bg-canvas pl-12 pr-4 font-control text-base text-text-primary placeholder:text-text-placeholder focus:border-brand-primary focus:outline-none focus:ring-4 focus:ring-brand-primary/15"
            />
            <datalist id={listId}>
              {suggestions.map((area) => (
                <option key={area.code} value={area.code}>
                  {`${shortName(area.name)}, ${area.province}`}
                </option>
              ))}
            </datalist>
          </div>
        </div>

        <button
          type="submit"
          disabled={state.status === "loading"}
          className={buttonClassName("primary", "!rounded-full sm:w-auto", "xl")}
        >
          {state.status === "loading" ? "Looking…" : isCaregiver ? "See who is nearby" : "Find caregivers"}
          <ArrowRightIcon />
        </button>
      </form>

      <div className="mt-4 flex flex-wrap items-center gap-2" role="group" aria-label="How far to look">
        <span className="font-control text-xs font-semibold uppercase tracking-widest text-text-muted">Within</span>
        {RADII.map((radius) => (
          <button
            key={radius}
            type="button"
            aria-pressed={radiusKm === radius}
            onClick={() => changeRadius(radius)}
            className={`rounded-full border px-3.5 py-1.5 font-control text-sm font-medium transition-colors ${
              radiusKm === radius
                ? "border-brand-primary bg-brand-soft text-brand-primary"
                : "border-border text-text-secondary hover:border-border-strong"
            }`}
          >
            {radius} km
          </button>
        ))}
      </div>

      <div aria-live="polite" className="mt-6 empty:mt-0">
        {state.status === "found" ? <Result overview={state.overview} audience={audience} /> : null}
        {state.status === "unknown" ? (
          <p className="rounded-2xl bg-surface-muted px-5 py-4 text-sm text-text-secondary">
            We could not find that postal code or place in Canada. Try the first three characters of your postal
            code, like <strong className="font-semibold text-text-primary">M5V</strong>.
          </p>
        ) : null}
        {state.status === "error" ? (
          <p className="rounded-2xl bg-danger-soft px-5 py-4 text-sm text-danger-accent">
            We could not check right now. Please try again in a moment.
          </p>
        ) : null}
      </div>
    </div>
  );
}

function Result({ overview, audience }: { overview: Overview; audience: Audience }) {
  const { area, caregiverCount, radiusKm } = overview;
  const place = shortName(area.name);
  const isCaregiver = audience === "caregiver";
  const withCare = overview.categories.filter((category) => category.count > 0);

  // In the full app, carry the place into sign-up so the first search is already theirs.
  const href = `${getStartedHref(audience)}&postalCode=${encodeURIComponent(area.code)}`;

  if (caregiverCount === 0) {
    return (
      <div className="rounded-2xl border border-brand-soft-border bg-brand-soft p-5 sm:p-6">
        <p className="font-display text-2xl text-text-primary">
          {isCaregiver ? `Be one of the first in ${place}.` : `We are just opening near ${place}.`}
        </p>
        <p className="mt-2 max-w-xl text-sm leading-relaxed text-text-secondary">
          {isCaregiver
            ? `No caregivers are listed within ${radiusKm} km of ${area.code} yet, so families here would find you straight away.`
            : `No caregivers are listed within ${radiusKm} km of ${area.code} yet. Join the early list and we will tell you as soon as one is.`}
        </p>
        <Link href={href} className={buttonClassName("primary", "!rounded-full mt-5", "lg")}>
          {isCaregiver ? "Become a caregiver here" : "Tell me when caregivers join"}
          <ArrowRightIcon />
        </Link>
      </div>
    );
  }

  return (
    <div className="rounded-2xl border border-brand-soft-border bg-brand-soft p-5 sm:p-6">
      <p className="font-display text-4xl text-text-primary sm:text-5xl">
        {caregiverCount.toLocaleString("en-CA")}{" "}
        <span className="text-2xl sm:text-3xl">
          {caregiverCount === 1 ? "caregiver" : "caregivers"} near {place}
        </span>
      </p>
      <p className="mt-2 text-sm text-text-secondary">
        Within {radiusKm} km of {area.code}, {area.province}. Names, rates and reviews are shown once you have an
        account.
      </p>

      {withCare.length > 0 ? (
        <ul className="mt-4 flex flex-wrap gap-2">
          {withCare.map((category) => (
            <li
              key={category.code}
              className="rounded-full border border-border bg-surface px-3.5 py-1.5 font-control text-sm text-text-secondary"
            >
              {category.name}
              <span className="ml-1.5 font-semibold text-text-primary">{category.count}</span>
            </li>
          ))}
        </ul>
      ) : null}

      <Link href={href} className={buttonClassName("primary", "!rounded-full mt-5", "lg")}>
        {isCaregiver ? "Join them" : "See them: create a free account"}
        <ArrowRightIcon />
      </Link>
    </div>
  );
}
