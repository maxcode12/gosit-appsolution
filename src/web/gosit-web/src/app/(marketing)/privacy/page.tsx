import type { Metadata } from "next";
import Link from "next/link";
import {
  LegalDocument,
  Points,
  type LegalSection,
} from "@/features/marketing/components/legal-document";

export const metadata: Metadata = {
  title: "Privacy policy",
  description:
    "What personal information GoSit collects, why, who it is shared with, how long it is kept, and the choices you have.",
};

/**
 * Written from docs/legal/Go-Sit-Privacy-Policy-DRAFT.docx and the code as it stands.
 * Every statement here describes something the product does; where the product has not
 * settled a point (screening vendor, retention after account closure) the text says so in
 * general terms rather than promising a number. Counsel's open flags are in the draft.
 */
const SECTIONS: LegalSection[] = [
  {
    id: "who-we-are",
    title: "Who we are",
    body: (
      <>
        <p>
          This policy is from 11419056 Canada Inc., operating as GoSit (&ldquo;GoSit&rdquo;, &ldquo;we&rdquo;,
          &ldquo;us&rdquo;), a corporation incorporated under the Canada Business Corporations Act and
          registered in Ontario. GoSit is a marketplace that connects families with independent
          caregivers. It operates mainly in Ontario.
        </p>
        <p>
          We handle personal information in line with Canada&apos;s federal privacy law, the{" "}
          <em>Personal Information Protection and Electronic Documents Act</em> (PIPEDA).
        </p>
      </>
    ),
  },
  {
    id: "what-we-collect",
    title: "What we collect",
    body: (
      <>
        <p>
          Some of this is about you. Some is about another person or a pet that you tell us about so
          that care can be arranged.
        </p>
        <Points
          items={[
            <>
              <strong>Your account:</strong> name, email address, phone number, role (family or
              caregiver) and a password, which we store only in hashed form. If you sign in with
              Google, we receive your Google name and verified email address, and nothing else from
              your Google account.
            </>,
            <>
              <strong>Families:</strong> home address, an &ldquo;about me&rdquo; note and emergency contacts.
            </>,
            <>
              <strong>People receiving care:</strong> first name, date of birth, and care-needs notes
              such as mobility, communication, allergies, routines and emergency instructions. A family
              provides this about a child, senior or adult who does not have an account of their own.
              It is sensitive and is never shown in public search.
            </>,
            <>
              <strong>Pets:</strong> name, species, breed, size, feeding, medication, behaviour, vet
              clinic and other notes.
            </>,
            <>
              <strong>Caregivers:</strong> profile, experience, rates, service area, home address,
              certifications, insurance documents, availability, and the result of identity and
              background screening.
            </>,
            <>
              <strong>Bookings:</strong> schedule, address, instructions, price, and check-in and
              check-out times together with whether the device&apos;s location matched the booking address
              at that moment.
            </>,
            <>
              <strong>Payments:</strong> handled by Stripe. We do not store card numbers.
            </>,
            <>
              <strong>Messages and reviews</strong> between a family and a caregiver.
            </>,
            <>
              <strong>Video interviews:</strong> the video, audio and in-call chat of a recorded
              interview, and when each person agreed and joined.
            </>,
            <>
              <strong>Device and usage:</strong> IP address, browser, device type, and log and error
              data.
            </>,
          ]}
        />
      </>
    ),
  },
  {
    id: "how-we-use-it",
    title: "How we use it",
    body: (
      <Points
        items={[
          "To run the marketplace: search, matching, bookings, scheduling and payment.",
          "To verify identity and screen caregivers before they can be listed.",
          "To let a family and a caregiver communicate, including by recorded video interview.",
          "For trust and safety: reviewing reports, flagged messages and reported reviews, resolving disputes, and reviewing an interview recording when a concern is raised.",
          "To send booking, message, certification and dispute notifications by email and in the app.",
          "To turn an address into a map location, so caregivers can be found near a postal code and arrival can be checked.",
          "To meet our legal obligations, including responding to lawful requests.",
          "To improve the service, using aggregate information that does not identify anyone.",
        ]}
      />
    ),
  },
  {
    id: "consent",
    title: "Consent",
    body: (
      <>
        <p>
          By creating an account you agree to the collection, use and disclosure of personal
          information described here. Some things need a separate, specific agreement at the time. The
          main one is joining a video interview, which requires you to agree to the recording before
          the call connects.
        </p>
        <p>
          You can withdraw consent for optional uses at any time. That may limit what you can do on
          GoSit. A caregiver, for example, cannot be listed without completing verification.
        </p>
        <p>
          If you tell us about another person, such as a child or a parent, you confirm that you are
          entitled to do so.
        </p>
      </>
    ),
  },
  {
    id: "google",
    title: "Sign in with Google and Google services",
    body: (
      <>
        <p>
          If you choose &ldquo;Continue with Google&rdquo;, Google tells us your name, your email address and
          whether it is verified. We use this only to create your account or sign you in, and to link
          it to an existing GoSit account with the same verified email. We do not request access to
          your Google contacts, calendar, Drive or any other Google data, and we do not read or store
          anything else from your Google account.
        </p>
        <p>
          GoSit&apos;s use of information received from Google follows the{" "}
          <a
            href="https://developers.google.com/terms/api-services-user-data-policy"
            rel="noopener"
            className="underline hover:text-brand-primary"
          >
            Google API Services User Data Policy
          </a>
          , including its Limited Use requirements. Information from Google is not sold, not used for
          advertising and not shared except as described in this policy.
        </p>
        <p>
          We also send addresses to Google&apos;s mapping service to find their location on a map. Google
          processes them under its own terms.
        </p>
      </>
    ),
  },
  {
    id: "interviews",
    title: "Video interview recordings",
    body: (
      <>
        <p>
          A video interview can show a family&apos;s home and anyone in view of the camera, including a
          child or senior who is not the account holder. We treat recordings as one of our most
          sensitive categories.
        </p>
        <Points
          items={[
            "Recordings exist so that GoSit’s trust and safety staff have a record if either person raises a concern about a call.",
            "Only trust and safety staff can view a recording, and every view is logged with who and when.",
            "A recording is never shown to the other participant, never made public, and never used for anything but trust and safety review.",
            "Recordings are deleted automatically 90 days after the interview, unless staff have placed a hold on one for an active dispute or incident.",
            "Recordings are not used to train any machine-learning or AI model.",
          ]}
        />
      </>
    ),
  },
  {
    id: "sharing",
    title: "Who we share it with",
    body: (
      <>
        <p>We do not sell personal information. We share it only as follows.</p>
        <Points
          items={[
            <>
              <strong>The other person in a booking.</strong> Before a caregiver accepts, they see a
              family&apos;s first name, last initial and approximate area. After acceptance, they see the
              full name, exact address, booking instructions and, for a person, the care needs, or for a
              pet, the pet-care notes. For pet boarding, the caregiver&apos;s home address is shown to the
              family once the caregiver accepts.
            </>,
            <>
              <strong>Stripe</strong>, to take payments and pay caregivers.
            </>,
            <>
              <strong>Microsoft Azure</strong>, which hosts the platform, its database and its file
              storage.
            </>,
            <>
              <strong>Our video interview provider</strong>, which carries the call and its recording
              before the recording reaches our own storage.
            </>,
            <>
              <strong>A screening provider</strong>, to run identity and background checks on caregivers
              who submit them.
            </>,
            <>
              <strong>Google</strong>, for sign-in (if you use it) and for address look-ups.
            </>,
            <>
              <strong>An email delivery service</strong>, to send you notifications and messages from us.
            </>,
            <>
              <strong>Police, courts and regulators</strong>, where a valid legal request requires it or
              where it is needed to protect someone&apos;s safety.
            </>,
          ]}
        />
      </>
    ),
  },
  {
    id: "outside-canada",
    title: "Where information is processed",
    body: (
      <p>
        The platform and its stored files are hosted on Microsoft Azure. Some of our providers,
        including our video interview provider and Google, process information on servers outside
        Canada, including in the United States. Information held in another country can be accessed by
        that country&apos;s courts and authorities under its laws. We choose providers with care and
        limit what each one receives.
      </p>
    ),
  },
  {
    id: "retention",
    title: "How long we keep it",
    body: (
      <>
        <Points
          items={[
            "Interview recordings: 90 days, then deleted, unless held for a dispute or incident.",
            "Search analytics (counts only, no message content): 180 days.",
            "Messages, bookings, reviews and payment records: for the life of the account and afterwards for as long as the law requires, for example for tax and accounting.",
            "Screening and certification documents: while the certification or account is active, and then only as long as we need them. We prefer to keep a verification summary rather than a full report.",
            "Incident reports: kept, because they are the record behind a safety decision.",
          ]}
        />
        <p>
          We keep only what we need for these purposes, and we delete or de-identify information we no
          longer need.
        </p>
      </>
    ),
  },
  {
    id: "security",
    title: "How we protect it",
    body: (
      <Points
        items={[
          "Sensitive documents and recordings sit in private storage that is never publicly accessible, and only authorised staff can open them.",
          "Every access to a screening document or an interview recording is logged.",
          "Connections are encrypted, and passwords are stored hashed, never in plain text.",
          "Care needs, exact addresses and messages are shown only to the people who need them for a booking.",
          "We keep health, disability, child, senior, address and screening details out of our logs.",
        ]}
      />
    ),
  },
  {
    id: "children",
    title: "Children",
    body: (
      <p>
        GoSit accounts are for adults 18 and over. A child can be named as someone receiving care, but
        only by the adult responsible for them. We do not knowingly collect information directly from
        a child, and a child does not use GoSit on their own account.
      </p>
    ),
  },
  {
    id: "your-rights",
    title: "Your choices and rights",
    body: (
      <>
        <p>
          Subject to legal limits, you can ask to see the personal information we hold about you, ask us
          to correct it, ask us to delete it, and withdraw consent for optional uses. To do so,{" "}
          <Link href="/contact" className="underline hover:text-brand-primary">
            contact us
          </Link>{" "}
          and tell us it is a privacy request. We may need to confirm who you are first.
        </p>
        <p>
          If you believe your rights under PIPEDA have not been respected, you can complain to the{" "}
          <a
            href="https://www.priv.gc.ca/"
            rel="noopener"
            className="underline hover:text-brand-primary"
          >
            Office of the Privacy Commissioner of Canada
          </a>
          .
        </p>
      </>
    ),
  },
  {
    id: "cookies",
    title: "Cookies",
    body: (
      <p>
        GoSit uses cookies that are needed to sign you in and keep you signed in. They are set by us,
        and we do not use them for advertising. If you use &ldquo;Continue with Google&rdquo;, Google&apos;s
        sign-in script may set its own cookies while it runs.
      </p>
    ),
  },
  {
    id: "automated",
    title: "Automated decisions",
    body: (
      <p>
        GoSit does not make decisions with a legal or similarly significant effect on you by automated
        means. Search results and matches follow the filters and details that people set themselves.
      </p>
    ),
  },
  {
    id: "changes",
    title: "Changes to this policy",
    body: (
      <p>
        We may update this policy. We will tell you about material changes in the app or by email
        before they take effect. The date at the top shows when it last changed.
      </p>
    ),
  },
];

export default function PrivacyPage() {
  return (
    <LegalDocument
      eyebrow="Legal"
      title="Privacy policy"
      intro="GoSit looks after people who are often at their most vulnerable, so we collect as little as we can and say plainly what we do with it."
      sections={SECTIONS}
    />
  );
}
