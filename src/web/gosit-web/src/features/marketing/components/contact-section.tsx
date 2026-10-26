import { ContactForm } from "@/features/marketing/components/contact-form";

/**
 * The contact form's place on the public page. Every "Contact us" button on the site
 * scrolls here.
 */
export function ContactSection() {
  return (
    <section id="contact" className="scroll-mt-20 border-t border-border bg-surface">
      <div className="mx-auto max-w-2xl px-4 py-20 sm:px-6 sm:py-24">
        <div className="text-center">
          <p className="font-control text-sm font-semibold uppercase tracking-widest text-brand-primary">
            Contact
          </p>
          <h2 className="mt-3 font-heading text-3xl font-extrabold leading-tight tracking-tight text-text-primary sm:text-4xl">
            Tell us what you need
          </h2>
          <p className="mt-4 text-lg leading-relaxed text-text-secondary">
            Families, caregivers and partners: send us a message and we will reply by email.
          </p>
        </div>

        <div className="mt-10 rounded-card border border-border bg-canvas p-5 shadow-sm sm:p-8">
          <ContactForm />
        </div>
      </div>
    </section>
  );
}
