import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/button";
import { ArrowRightIcon, CheckIcon, HomeIcon, PawIcon } from "@/components/ui/marketing-icons";
import { FinalCta } from "@/features/marketing/components/final-cta";
import { Container, Eyebrow, PageHero } from "@/features/marketing/components/section";
import { getStartedHref } from "@/features/marketing/public-mode";

export const metadata: Metadata = {
  title: "Care we cover",
  description:
    "Child care, senior care, specialized non-medical support, family support and pet care, from independent caregivers across Ontario.",
};

/**
 * The service taxonomy, grouped the way the product groups it. This list has to keep up
 * with the reference data the API serves rather than being written once at launch.
 */
const CATEGORIES = [
  {
    id: "child-care",
    name: "Child care",
    blurb: "From a few hours after school to overnight care, with someone your children can get to know.",
    image: "/images/child-care.jpg",
    alt: "A caregiver and a young girl building with coloured blocks at a table.",
    services: ["Babysitting", "Nanny care", "After-school care", "Overnight child care"],
  },
  {
    id: "senior-care",
    name: "Senior care",
    blurb: "Company, practical help and a steady presence, so an older family member can stay comfortable at home.",
    image: "/images/senior-care.jpg",
    alt: "A caregiver holding an older woman's hands, helping her up from a chair.",
    services: [
      "Senior companionship",
      "Meal support",
      "Errand assistance",
      "Mobility assistance",
      "Appointment companion",
      "Dementia companion care",
    ],
  },
  {
    id: "specialized-support",
    name: "Specialized support",
    blurb: "Caregivers with experience of additional needs, matched against what the person actually needs.",
    image: "/images/specialised-support.jpg",
    alt: "A companion walking with someone in a wheelchair across a field at sunset.",
    services: [
      "Autism support",
      "Developmental disability support",
      "Special-needs respite",
      "Communication support",
      "Mobility support",
      "Behavioural support",
    ],
  },
  {
    id: "family-support",
    name: "Family support",
    blurb: "The practical help that keeps a household going while someone in it needs more attention.",
    image: "/images/family-support.jpg",
    alt: "Three adults of different generations cooking a meal together in a kitchen.",
    services: ["Respite care", "Transportation assistance", "Care-related light household support", "Routine and companionship support"],
  },
];

const PET_SERVICES = [
  { name: "Dog walking", note: "Priced by the hour" },
  { name: "Drop-in pet visits", note: "Priced by the hour" },
  { name: "In-home pet sitting", note: "At your home, priced per night" },
  { name: "Pet boarding", note: "At the caregiver's home, priced per night" },
];

export default function ServicesPage() {
  return (
    <>
      <PageHero
        eyebrow="Care we cover"
        title={
          <>
            Many kinds of help, <em className="text-brand-primary">one place</em>.
          </>
        }
        body="GoSit is for the whole of family life. Every caregiver is matched to the services they are qualified for, not just the ones they list."
      >
        <Link href={getStartedHref("family")} className={buttonClassName("primary", "!rounded-full", "xl")}>
          Find a caregiver
          <ArrowRightIcon />
        </Link>
      </PageHero>

      <section className="py-20 sm:py-28">
        <Container className="flex flex-col gap-10">
          {CATEGORIES.map((category, index) => (
            <article
              id={category.id}
              key={category.id}
              className="reveal scroll-mt-28 grid overflow-hidden rounded-[2rem] border border-border bg-surface shadow-md lg:grid-cols-2"
            >
              <div className={`relative min-h-[18rem] ${index % 2 === 1 ? "lg:order-2" : ""}`}>
                <Image
                  src={category.image}
                  alt={category.alt}
                  fill
                  sizes="(min-width: 1024px) 50vw, 100vw"
                  className="object-cover"
                />
              </div>
              <div className="p-8 sm:p-12">
                <Eyebrow>Service category</Eyebrow>
                <h2 className="font-display mt-4 text-4xl text-text-primary sm:text-5xl">{category.name}</h2>
                <p className="mt-4 text-text-secondary">{category.blurb}</p>
                <ul className="mt-7 grid gap-x-6 gap-y-3 sm:grid-cols-2">
                  {category.services.map((service) => (
                    <li key={service} className="flex items-start gap-2.5 text-sm font-medium text-text-primary">
                      <CheckIcon className="mt-0.5 size-4 shrink-0 text-brand-primary" />
                      {service}
                    </li>
                  ))}
                </ul>
                <p className="mt-7 flex items-center gap-2 text-xs text-text-muted">
                  <HomeIcon className="size-4" />
                  Care for people takes place at your home.
                </p>
              </div>
            </article>
          ))}

          <article
            id="pet-care"
            className="reveal deep-wash scroll-mt-28 grid overflow-hidden rounded-[2rem] shadow-md lg:grid-cols-2"
          >
            <div className="p-8 sm:p-12">
              <Eyebrow light>A separate service</Eyebrow>
              <h2 className="font-display mt-4 text-4xl text-text-on-deep sm:text-5xl">Pet care</h2>
              <p className="mt-4 text-text-on-deep/80">
                Pets are not family members who need care in the same way, so pet care is its own service with
                its own caregivers and its own rules.
              </p>
              <ul className="mt-7 grid gap-3">
                {PET_SERVICES.map((service) => (
                  <li key={service.name} className="flex items-center gap-3 rounded-2xl bg-white/10 px-4 py-3.5">
                    <span className="grid size-9 place-items-center rounded-xl bg-white/15 text-accent-on-deep">
                      <PawIcon className="size-5" />
                    </span>
                    <span>
                      <span className="block text-sm font-semibold text-text-on-deep">{service.name}</span>
                      <span className="block text-xs text-text-on-deep/70">{service.note}</span>
                    </span>
                  </li>
                ))}
              </ul>
            </div>
            <div className="relative min-h-[18rem]">
              <Image
                src="/images/pet-care.jpg"
                alt="A person walking a golden retriever on a leash along a paved path."
                fill
                sizes="(min-width: 1024px) 50vw, 100vw"
                className="object-cover"
              />
            </div>
          </article>
        </Container>
      </section>

      <section className="border-t border-border bg-surface py-16">
        <Container>
          <p className="mx-auto max-w-3xl text-center text-sm leading-relaxed text-text-muted">
            GoSit arranges <strong className="font-semibold text-text-secondary">non-medical care</strong> only.
            Caregivers do not administer medication, perform nursing procedures, or provide therapy or
            diagnosis. For regulated health care, please speak to a licensed provider.
          </p>
        </Container>
      </section>

      <FinalCta title="Tell us what you need." />
    </>
  );
}
