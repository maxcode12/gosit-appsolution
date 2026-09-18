import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { buttonClassName } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { getMyProfile } from "@/features/bookings/api";
import { BookingForm } from "@/features/bookings/components/booking-form";
import { getCareServiceCatalogue, getCaregiverDetail } from "@/features/search/api";
import { careRequesterNav } from "@/features/search/nav";
import { formatAddress } from "@/features/bookings/address";
import { ApiError } from "@/lib/api/client";
import type { CaregiverDetail } from "@/features/search/types";
import { isPetCareService } from "@/features/bookings/types";

export const metadata: Metadata = {
  title: "Request a booking",
};

/**
 * The booking request form.
 *
 * Two things must be true before someone can book: they need a profile with an address
 * (the booking defaults to it), and at least one care recipient. Rather than letting the
 * form fail at submit, each missing piece is caught here and sent to the place that
 * fixes it.
 */
export default async function BookCaregiverPage({
  params,
}: {
  params: Promise<{ caregiverProfileId: string }>;
}) {
  const { caregiverProfileId } = await params;

  let caregiver: CaregiverDetail;

  try {
    caregiver = await getCaregiverDetail(caregiverProfileId);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      notFound();
    }

    throw error;
  }

  const [profile, catalogue] = await Promise.all([getMyProfile(), getCareServiceCatalogue()]);
  const name = `${caregiver.firstName}${caregiver.lastInitial ? ` ${caregiver.lastInitial}.` : ""}`;

  // A caregiver lists the services they offer; each service also demands certifications.
  // The API refuses a booking when those do not line up, so the mismatch is filtered out
  // here — offering a choice that is certain to be rejected is worse than not offering it.
  const held = new Set(caregiver.certificationCodes);

  const requirements = new Map(
    catalogue.categories
      .flatMap((category) => category.services)
      .map((service) => [service.code, service.requiredCertificationCodes]),
  );

  // How each service is priced and where it happens, from the catalogue.
  const termsByService: Record<string, { pricingUnit: string; careLocation: string }> =
    Object.fromEntries(
      catalogue.categories
        .flatMap((category) => category.services)
        .map((service) => [
          service.code,
          { pricingUnit: service.pricingUnit, careLocation: service.careLocation },
        ]),
    );

  const bookableServices = caregiver.services.filter(
    (service) =>
      (requirements.get(service.code) ?? []).every((code) => held.has(code)) &&
      // A caregiver cleared only for pet care cannot be booked for a person, whatever
      // their profile lists; the API would refuse it.
      (isPetCareService(service) || caregiver.clearedForPeopleCare) &&
      // Boarding and in-home sitting are priced per night, so they need a nightly rate.
      (termsByService[service.code]?.pricingUnit !== "Nightly" || caregiver.nightlyRate !== null),
  );

  // Which kinds of animal each pet service is for, so a dog walk is never offered for a cat.
  const petSpeciesByService: Record<string, string[]> = Object.fromEntries(
    catalogue.categories
      .flatMap((category) => category.services)
      .map((service) => [service.code, service.applicablePetSpecies]),
  );

  const shell = (children: React.ReactNode) => (
    <DashboardShell
      items={careRequesterNav}
      currentPath="/bookings"
      heading={`Request care from ${name}`}
      description={caregiver.approximateLocation}
    >
      <div className="max-w-2xl">{children}</div>
    </DashboardShell>
  );

  if (!profile || !profile.address) {
    return shell(
      <EmptyState
        title="Add your details first"
        description="A booking needs a contact number and the address where care will happen. It takes a minute, and you will not have to do it again."
        action={
          <Link href="/account" className={buttonClassName()}>
            Complete your profile
          </Link>
        }
      />,
    );
  }

  if (profile.careRecipients.length === 0 && profile.pets.length === 0) {
    return shell(
      <EmptyState
        title="Who is the care for?"
        description="Add the person who needs care — or your pet — with anything a caregiver should know, and you can book straight away."
        action={
          <Link href="/account" className={buttonClassName()}>
            Add who care is for
          </Link>
        }
      />,
    );
  }

  if (bookableServices.length === 0) {
    return shell(
      <EmptyState
        title={`${name} cannot be booked just yet`}
        description="Every service needs the right certifications on file, and theirs are still being sorted out. Try again later, or find someone else available now."
        action={
          <Link href="/dashboard" className={buttonClassName()}>
            Find another caregiver
          </Link>
        }
      />,
    );
  }

  // Care for people names people and pet care names pets, so a service is offered only
  // when the family has recorded someone, or some animal, it could be for.
  const usableServices = bookableServices.filter((service) =>
    isPetCareService(service)
      ? profile.pets.some((pet) => (petSpeciesByService[service.code] ?? []).includes(pet.species))
      : profile.careRecipients.length > 0,
  );

  if (usableServices.length === 0) {
    const offersPetCare = bookableServices.some(isPetCareService);

    return shell(
      <EmptyState
        title={offersPetCare ? "Add your pet first" : "Who is the care for?"}
        description={
          offersPetCare
            ? `${name} offers pet care. Add your pet, with anything a sitter should know, and you can book straight away.`
            : "Add the person who needs care, with anything a caregiver should know, and you can book straight away."
        }
        action={
          <Link href="/account" className={buttonClassName()}>
            {offersPetCare ? "Add a pet" : "Add a care recipient"}
          </Link>
        }
      />,
    );
  }

  return shell(
    <div className="flex flex-col gap-6">
      <div className="rounded-card border border-border bg-surface p-5">
        <h2 className="font-heading text-base font-medium text-text-primary">Where</h2>
        <p className="mt-1 text-sm text-text-secondary">{formatAddress(profile.address)}</p>
        <p className="mt-2 text-sm text-text-muted">
          Your address is shared with {name} once they confirm the booking.
        </p>
        {usableServices.some(
          (service) => termsByService[service.code]?.careLocation === "CaregiverHome",
        ) ? (
          <p className="mt-2 text-sm text-text-muted">
            Boarding is the exception: it happens at {name}&apos;s home in{" "}
            {caregiver.approximateLocation}, and you see their address once they accept.
          </p>
        ) : null}
      </div>

      <BookingForm
        caregiverProfileId={caregiver.caregiverProfileId}
        caregiverName={caregiver.firstName}
        hourlyRate={caregiver.hourlyRate}
        nightlyRate={caregiver.nightlyRate}
        termsByService={termsByService}
        services={usableServices}
        recipients={profile.careRecipients}
        pets={profile.pets}
        petSpeciesByService={petSpeciesByService}
      />
    </div>,
  );
}
