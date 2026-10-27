"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { TextInput } from "@/components/ui/text-input";
import type { CareServiceCatalogue } from "@/features/search/types";

/**
 * Search filters.
 *
 * State lives in the URL rather than in the component, so a search can be shared,
 * bookmarked and reloaded, and the back button does what people expect. Submitting
 * navigates; the server component re-runs the query.
 */
export function SearchFilters({
  catalogue,
  canUseAdvancedFilters = true,
  className = "",
}: {
  catalogue: CareServiceCatalogue;
  /**
   * Whether this account's plan includes ADVANCED_SEARCH_FILTERS.
   *
   * The API refuses the paid filters outright rather than quietly ignoring them, so
   * showing an input that produces an error would be worse than not showing it. This
   * only decides what to render — the entitlement is resolved and enforced on the
   * server, and a client that sends the filter anyway is still refused.
   */
  canUseAdvancedFilters?: boolean;
  className?: string;
}) {
  const router = useRouter();
  const params = useSearchParams();
  const [isOpen, setIsOpen] = useState(false);

  function apply(formData: FormData) {
    const next = new URLSearchParams();

    for (const [key, value] of formData.entries()) {
      const trimmed = String(value).trim();

      // Empty filters are dropped so the URL shows only what is actually applied.
      if (trimmed) {
        next.set(key, trimmed);
      }
    }

    router.push(next.size > 0 ? `/dashboard?${next}` : "/dashboard");
  }

  const activeCount = ["serviceCode", "city", "postalCode", "radiusKm", "maxHourlyRate", "minYearsOfExperience", "minRating"]
    .filter((key) => params.get(key))
    .length;

  return (
    <form action={apply} className={`rounded-card border border-border bg-surface p-5 ${className}`}>
      <div className="flex items-center justify-between gap-3">
        <h2 className="font-heading text-lg font-medium text-text-primary">
          Filters
          {activeCount > 0 ? (
            <span className="ml-2 rounded-pill bg-brand-primary px-2 py-0.5 text-xs text-text-on-brand">
              {activeCount}
            </span>
          ) : null}
        </h2>

        {/* Collapsed by default on small screens so results are visible first. */}
        <button
          type="button"
          onClick={() => setIsOpen(!isOpen)}
          aria-expanded={isOpen}
          className="font-control text-sm text-brand-primary underline lg:hidden"
        >
          {isOpen ? "Hide" : "Show"}
        </button>
      </div>

      <div className={`mt-4 flex-col gap-4 ${isOpen ? "flex" : "hidden"} lg:flex`}>
        <label className="flex flex-col gap-1">
          <span className="font-control text-sm text-text-secondary">Type of care</span>
          <select
            name="serviceCode"
            defaultValue={params.get("serviceCode") ?? ""}
            className="h-11 rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          >
            <option value="">Any</option>
            {catalogue.categories.map((category) => (
              <optgroup key={category.code} label={category.name}>
                {category.services.map((service) => (
                  <option key={service.code} value={service.code}>
                    {service.name}
                  </option>
                ))}
              </optgroup>
            ))}
          </select>
        </label>

        <TextInput
          label="City"
          name="city"
          hideLabel={false}
          placeholder="Toronto"
          defaultValue={params.get("city") ?? ""}
          className="h-11 rounded-control border-border px-3 text-sm"
        />

        <TextInput
          label="Postal code"
          name="postalCode"
          hideLabel={false}
          placeholder="M5S 1M2"
          defaultValue={params.get("postalCode") ?? ""}
          className="h-11 rounded-control border-border px-3 text-sm"
        />

        <label className="flex flex-col gap-1">
          <span className="font-control text-sm text-text-secondary">Distance from that postal code</span>
          <select
            name="radiusKm"
            defaultValue={params.get("radiusKm") ?? ""}
            className="h-11 rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          >
            <option value="">Any distance</option>
            <option value="5">Within 5 km</option>
            <option value="10">Within 10 km</option>
            <option value="25">Within 25 km</option>
            <option value="50">Within 50 km</option>
          </select>
        </label>

        <TextInput
          label="Maximum hourly rate"
          name="maxHourlyRate"
          hideLabel={false}
          type="number"
          min={1}
          step={1}
          placeholder="40"
          defaultValue={params.get("maxHourlyRate") ?? ""}
          className="h-11 rounded-control border-border px-3 text-sm"
        />

        {canUseAdvancedFilters ? (
          <TextInput
            label="Minimum years of experience"
            name="minYearsOfExperience"
            hideLabel={false}
            type="number"
            min={0}
            max={70}
            placeholder="2"
            defaultValue={params.get("minYearsOfExperience") ?? ""}
            className="h-11 rounded-control border-border px-3 text-sm"
          />
        ) : (
          <div className="flex flex-col gap-1">
            <span className="font-control text-sm text-text-secondary">
              Minimum years of experience
            </span>
            <p className="text-sm text-text-muted">
              Part of a paid plan.{" "}
              <Link href="/account/plan" className="underline hover:text-text-primary">
                See plans
              </Link>
            </p>
          </div>
        )}

        <label className="flex flex-col gap-1">
          <span className="font-control text-sm text-text-secondary">Minimum rating</span>
          <select
            name="minRating"
            defaultValue={params.get("minRating") ?? ""}
            className="h-11 rounded-control border border-border bg-surface px-3 font-control text-sm text-text-primary"
          >
            <option value="">Any</option>
            <option value="4">4 stars and above</option>
            <option value="4.5">4.5 stars and above</option>
          </select>
        </label>

        <div className="flex gap-2">
          <Button type="submit" className="flex-1">
            Apply
          </Button>
          {activeCount > 0 ? (
            <Button
              type="button"
              variant="ghost"
              onClick={() => router.push("/dashboard")}
            >
              Clear
            </Button>
          ) : null}
        </div>
      </div>
    </form>
  );
}
