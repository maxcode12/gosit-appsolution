import type { Metadata } from "next";
import Link from "next/link";
import { DashboardShell } from "@/components/layout/dashboard-shell";
import { getMyCaregiverProfile, getMyVerifications } from "@/features/caregivers/api";
import { CertificationsPanel } from "@/features/caregivers/components/certifications-panel";
import { DocumentCard } from "@/features/caregivers/components/document-card";
import { InsurancePanel } from "@/features/caregivers/components/insurance-panel";
import { OpenCardFromLink } from "@/features/caregivers/components/open-card-from-link";
import { ServiceAgreementPanel } from "@/features/caregivers/components/service-agreement-panel";
import { VerificationPanel } from "@/features/caregivers/components/verification-panel";
import { buildDocuments, needsAttention } from "@/features/caregivers/documents";
import { caregiverNav } from "@/features/caregivers/nav";
import { getCareServiceCatalogue } from "@/features/search/api";

export const metadata: Metadata = {
  title: "Documents",
  description: "Your required and optional documents, and where each one stands.",
};

/**
 * Every document in one place, required first, each with its state and a way to upload or
 * renew it. The uploads themselves are the existing panels; this page organises them so a
 * caregiver can see at a glance what is missing, in review, or about to lapse.
 */
export default async function CaregiverDocumentsPage() {
  const [profile, verifications, catalogue] = await Promise.all([
    getMyCaregiverProfile(),
    getMyVerifications(),
    getCareServiceCatalogue(),
  ]);

  if (!profile) {
    return (
      <DashboardShell
        items={caregiverNav}
        currentPath="/caregiver/documents"
        heading="Documents"
        description="Your required and optional documents."
      >
        <p className="max-w-2xl text-sm text-text-secondary">
          Save your profile first, then you can add your documents.{" "}
          <Link href="/caregiver/account" className="text-brand-primary underline">
            Go to your account
          </Link>
          .
        </p>
      </DashboardShell>
    );
  }

  const services = catalogue.categories.flatMap((category) => category.services);
  const offered = new Set(profile.serviceCodes);

  // Certificates that something this caregiver offers asks for, and every certificate any
  // service asks for — the set worth offering to add.
  const requiredCertificationCodes = services
    .filter((service) => offered.has(service.code))
    .flatMap((service) => service.requiredCertificationCodes);

  const knownCertifications = [
    ...new Set(services.flatMap((service) => service.requiredCertificationCodes)),
  ].sort();

  const documents = buildDocuments(profile, verifications, requiredCertificationCodes);
  const required = documents.filter((item) => item.required);
  const optional = documents.filter((item) => !item.required);
  const attention = required.filter((item) => needsAttention(item.state));
  const certificatesNeedAttention = required.some(
    (item) => item.id.startsWith("certificate-") && needsAttention(item.state),
  );

  return (
    <DashboardShell
      items={caregiverNav}
      currentPath="/caregiver/documents"
      heading="Documents"
      description="What GoSit needs from you, and what you can add."
    >
      <OpenCardFromLink />

      <div className="flex max-w-2xl flex-col gap-8">
        <section
          className={`rounded-card border p-5 ${
            attention.length === 0
              ? "border-border bg-surface"
              : "border-brand-accent/40 bg-brand-accent/5"
          }`}
        >
          <h2 className="font-heading text-base font-medium text-text-primary">
            {attention.length === 0
              ? "Your required documents are in order"
              : attention.length === 1
                ? "One document needs your attention"
                : `${attention.length} documents need your attention`}
          </h2>
          <p className="mt-1 text-sm text-text-secondary">
            {attention.length === 0
              ? "We will email you before anything expires, so you can renew it in time."
              : "A required document that is missing or has lapsed keeps you out of search and off new bookings."}
          </p>
        </section>

        <section aria-labelledby="required-documents" className="flex flex-col gap-3">
          <h2 id="required-documents" className="font-heading text-lg font-medium text-text-primary">
            Required
          </h2>

          {required.map((item) => (
            <DocumentCard
              key={item.id}
              item={item}
              defaultOpen={needsAttention(item.state)}
              linkTo="#certificates"
            >
              {item.id === "checks" ? (
                <VerificationPanel verifications={verifications} />
              ) : item.id === "insurance" ? (
                <InsurancePanel policies={profile.insurancePolicies} />
              ) : item.id === "agreement" ? (
                <ServiceAgreementPanel profile={profile} />
              ) : undefined}
            </DocumentCard>
          ))}
        </section>

        <section aria-labelledby="optional-documents" className="flex flex-col gap-3">
          <h2 id="optional-documents" className="font-heading text-lg font-medium text-text-primary">
            Optional
          </h2>

          {optional.map((item) => (
            <DocumentCard key={item.id} item={item} defaultOpen={certificatesNeedAttention}>
              <CertificationsPanel
                certifications={profile.certifications}
                available={knownCertifications}
              />
            </DocumentCard>
          ))}
        </section>
      </div>
    </DashboardShell>
  );
}
