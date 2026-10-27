import Image from "next/image";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/button";
import { ArrowRightIcon } from "@/components/ui/marketing-icons";
import { Container, SectionHeading } from "@/features/marketing/components/section";

const PEOPLE = [
  {
    title: "Children",
    body: "A caregiver your child gets to know: after school, in the evening, or overnight.",
    image: "/images/reading-together.jpg",
    alt: "A woman with glasses reading a book with a young girl on a bed.",
  },
  {
    title: "Seniors",
    body: "Company, meals, errands and steady help so an older parent can stay comfortable at home.",
    image: "/images/senior-activity.jpg",
    alt: "A caregiver knitting with two older women, all smiling.",
  },
  {
    title: "Adults who need support",
    body: "Experienced, non-medical support for people with additional needs, at their own pace.",
    image: "/images/specialised-support.jpg",
    alt: "A companion walking with someone in a wheelchair across a field at sunset.",
  },
];

/**
 * What GoSit is for, shown rather than described: the people the care is for, with a
 * caregiver in each photograph. Stock images, used illustratively (see CREDITS.md).
 */
export function WhatWeDo() {
  return (
    <section className="border-y border-border bg-surface py-16 sm:py-24">
      <Container>
        <div className="flex flex-col justify-between gap-6 lg:flex-row lg:items-end">
          <SectionHeading
            eyebrow="What we do"
            title="We connect families with caregivers they can trust."
            body="GoSit is a marketplace. We help you find, check and book independent caregivers for the people you look after, and we handle the messaging, scheduling and payment around it."
          />
          <Link href="/services" className={buttonClassName("ghost", "!rounded-full self-start lg:self-auto", "lg")}>
            See the care we cover
            <ArrowRightIcon />
          </Link>
        </div>

        <ul className="mt-12 grid gap-5 md:grid-cols-3">
          {PEOPLE.map((person) => (
            <li
              key={person.title}
              className="reveal group relative min-h-[26rem] overflow-hidden rounded-[1.75rem] shadow-md"
            >
              <Image
                src={person.image}
                alt={person.alt}
                fill
                sizes="(min-width: 768px) 33vw, 100vw"
                className="object-cover transition-transform duration-700 group-hover:scale-105"
              />
              <div
                aria-hidden
                className="absolute inset-0 bg-gradient-to-t from-[rgba(12,20,22,0.9)] via-[rgba(12,20,22,0.25)] to-transparent"
              />
              <div className="absolute inset-x-0 bottom-0 p-7">
                <h3 className="font-display text-3xl text-white">{person.title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-white/85">{person.body}</p>
              </div>
            </li>
          ))}
        </ul>
      </Container>
    </section>
  );
}
