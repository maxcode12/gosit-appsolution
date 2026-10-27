import type { Metadata } from "next";
import { Faq } from "@/features/marketing/components/faq";
import { CAREGIVER_FAQ, FAMILY_FAQ } from "@/features/marketing/components/faq-data";
import { FinalCta } from "@/features/marketing/components/final-cta";
import { Container, Eyebrow, PageHero } from "@/features/marketing/components/section";

export const metadata: Metadata = {
  title: "FAQ",
  description: "Straight answers for families and caregivers about how GoSit works, what is checked, and what it costs.",
};

export default function FaqPage() {
  const all = [...FAMILY_FAQ, ...CAREGIVER_FAQ];

  return (
    <>
      {/* Lets a search engine show the answers directly under the result. */}
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{
          __html: JSON.stringify({
            "@context": "https://schema.org",
            "@type": "FAQPage",
            mainEntity: all.map((item) => ({
              "@type": "Question",
              name: item.question,
              acceptedAnswer: { "@type": "Answer", text: item.answer },
            })),
          }),
        }}
      />

      <PageHero
        eyebrow="FAQ"
        title={
          <>
            Questions, <em className="text-brand-primary">answered</em>.
          </>
        }
        body="Including the awkward ones about what GoSit does not do."
      />

      <section className="py-24 sm:py-32">
        <Container className="grid gap-20 lg:grid-cols-2">
          <div>
            <Eyebrow>For families</Eyebrow>
            <div className="mt-6">
              <Faq items={FAMILY_FAQ} />
            </div>
          </div>
          <div>
            <Eyebrow>For caregivers</Eyebrow>
            <div className="mt-6">
              <Faq items={CAREGIVER_FAQ} />
            </div>
          </div>
        </Container>
      </section>

      <FinalCta title="Still have a question?" body="Send it to us. A real person replies." />
    </>
  );
}
