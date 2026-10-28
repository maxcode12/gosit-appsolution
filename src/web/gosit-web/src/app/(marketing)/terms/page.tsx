import type { Metadata } from "next";
import Link from "next/link";
import {
  LegalDocument,
  Points,
  type LegalSection,
} from "@/features/marketing/components/legal-document";

export const metadata: Metadata = {
  title: "Terms of service",
  description:
    "The terms for using GoSit: what the marketplace is and is not, how bookings, payments and interviews work, and what we expect of everyone.",
};

/**
 * Written from docs/legal/Go-Sit-Terms-of-Service-DRAFT.docx. The marketplace position is
 * the load-bearing part (CLAUDE.md, "Business Model"): caregivers are independent, GoSit
 * does not supervise care, and nothing here may promise safety. Fee percentages, the
 * cancellation window, the liability cap and the dispute venue are counsel's to settle, so
 * they are described rather than stated.
 */
const SECTIONS: LegalSection[] = [
  {
    id: "agreement",
    title: "This agreement",
    body: (
      <>
        <p>
          These terms are between 11419056 Canada Inc., operating as GoSit (&ldquo;GoSit&rdquo;, &ldquo;we&rdquo;,
          &ldquo;us&rdquo;), and anyone who creates an account or uses GoSit (&ldquo;you&rdquo;). By creating an
          account or using GoSit you agree to them and to our{" "}
          <Link href="/privacy" className="underline hover:text-brand-primary">
            privacy policy
          </Link>
          .
        </p>
        <p>
          GoSit is a marketplace. It connects families and others who need care (&ldquo;care requesters&rdquo;)
          with independent people who provide it (&ldquo;caregivers&rdquo;). That covers care for children,
          seniors, adults and people who need specialised non-medical support, and, separately, care
          for pets.
        </p>
      </>
    ),
  },
  {
    id: "what-we-are",
    title: "What GoSit is, and is not",
    body: (
      <>
        <Points
          items={[
            "GoSit is a technology platform. We are not a child care agency, home care agency, staffing or employment agency, kennel, veterinary service or health care provider.",
            "Caregivers are independent. They are not GoSit employees. They choose their own availability, decide which bookings to accept, and are responsible for how they provide care. We do not supervise, train or direct them.",
            "We do not provide care. We provide search, matching, verification, booking, scheduling, messaging, payments, reviews and check-in tools around an arrangement that a care requester and a caregiver make themselves.",
            "We do not guarantee the availability, quality, safety or legality of any care.",
            "GoSit is for non-medical care. Do not use it to arrange medication administration, nursing, therapy, diagnosis or any other regulated health service.",
          ]}
        />
        <p>In an emergency, call 911.</p>
      </>
    ),
  },
  {
    id: "accounts",
    title: "Accounts",
    body: (
      <Points
        items={[
          "You must be at least 18 and able to enter a binding contract.",
          "GoSit currently operates in Ontario. We do not promise it is available elsewhere.",
          "Give accurate information, keep your sign-in details to yourself, and tell us if you think someone else has used your account. You are responsible for what happens under it.",
          "An account is personal. It cannot be sold, transferred or shared.",
          "People receiving care, and pets, do not have accounts. If you add someone, or a pet, you confirm that you are entitled to add them and to share what you tell us about them.",
        ]}
      />
    ),
  },
  {
    id: "verification",
    title: "Verification and screening",
    body: (
      <>
        <p>
          Before a caregiver can be listed they must verify their identity, sign the caregiver service
          agreement, and complete the screening that fits the care they offer: a vulnerable sector
          check for care of people, and a criminal record check for pet care only. A caregiver cleared
          only for pet care is never eligible for care of people, however their profile is set up.
          Caregivers who offer services that need certifications have them reviewed by our team.
        </p>
        <p>
          <strong className="font-semibold text-text-primary">
            Verification and screening reduce risk. They do not remove it.
          </strong>{" "}
          A &ldquo;verified&rdquo; badge means a caregiver has passed those checks at the time of review. It is
          not a guarantee of anyone&apos;s fitness, skill, honesty or safety, and you should use your own
          judgement as well, including by interviewing a caregiver before you book. We rely on records
          from government bodies and our screening provider and are not responsible for errors or
          delays in them.
        </p>
        <p>
          Caregivers are responsible for holding any insurance their work calls for. GoSit does not
          insure caregivers.
        </p>
      </>
    ),
  },
  {
    id: "bookings",
    title: "Bookings",
    body: (
      <Points
        items={[
          "A care requester sends a booking request for a service and schedule, naming the person who will receive care or the pet. The caregiver can accept, decline or let it lapse.",
          "Hourly services are billed for the time actually worked, in 15-minute steps. Overnight pet sitting and pet boarding are billed per night.",
          "A care requester’s exact address and booking notes, and any care needs, are kept from a caregiver until they accept. For pet boarding, the caregiver’s home address is shown to the family once the caregiver accepts.",
          "Care of people always happens at the family’s home or another place the family arranges. It never takes place at a caregiver’s home. Only pet boarding happens at a caregiver’s home.",
          "Either person can cancel before a booking starts. Cancelling late may affect the payment. The terms that apply are shown on the booking.",
          "Check-in and check-out are recorded, along with whether the device’s location matched the address. That is a record for both people, not a statement about anyone’s conduct.",
          "If nobody checks out within the grace period, the booking is closed automatically and billed to its scheduled end.",
          "Either person can dispute a completed booking. Our staff review it and may adjust the payment. Both people receive the same explanation of the decision.",
        ]}
      />
    ),
  },
  {
    id: "pets",
    title: "Pet care",
    body: (
      <p>
        Dog walking, drop-in visits, in-home pet sitting and boarding are care for animals only. A pet
        is not a care recipient and the protections in these terms that are written for people do not
        apply to a pet. Pet owners and caregivers are responsible for complying with any municipal or
        provincial rules that apply to keeping or boarding animals, and for any injury or damage that
        involves an animal.
      </p>
    ),
  },
  {
    id: "interviews",
    title: "Video interviews",
    body: (
      <>
        <Points
          items={[
            "A care requester can ask a caregiver for a video interview before booking. The caregiver accepts or declines. Interviews take place inside GoSit.",
            "Every interview is recorded. You must agree to the recording to join, and a call whose recording cannot start does not go ahead.",
            "Recordings are used only for trust and safety review. Only GoSit trust and safety staff can view one, every view is logged, and recordings are deleted after 90 days unless held for an active dispute or incident.",
            "You must not record or capture an interview in any other way.",
          ]}
        />
        <p>
          Do not arrange care, share contact details or move a relationship off GoSit by any means, in
          an interview, a message or otherwise.
        </p>
      </>
    ),
  },
  {
    id: "messaging",
    title: "Messages",
    body: (
      <p>
        Messages between a care requester and a caregiver are private to that conversation. Messages
        that look like an exchange of contact details are flagged for staff review. They are still
        delivered. The flag supports the rule against taking arrangements off GoSit.
      </p>
    ),
  },
  {
    id: "payments",
    title: "Fees and payments",
    body: (
      <Points
        items={[
          "Payments are processed by Stripe. GoSit does not store card numbers.",
          "GoSit charges a service fee on completed bookings, which is taken before the caregiver is paid. The amount is shown to you before you confirm a booking or subscribe.",
          "Caregivers are independent and are responsible for their own income tax and any tax registration. GoSit does not withhold tax for them.",
          "Refunds that follow a cancellation or a resolved dispute go back to the original payment method.",
          "Subscriptions, where offered, have their own terms, which are shown when you subscribe.",
        ]}
      />
    ),
  },
  {
    id: "reviews",
    title: "Reviews",
    body: (
      <p>
        After a completed booking both people can review each other. A review is not shown until both
        have written one or the review window has closed. A review can be reported, and staff may hide
        one they find false, abusive or against these terms. We do not edit reviews, and we keep a
        hidden review as a record.
      </p>
    ),
  },
  {
    id: "conduct",
    title: "What you must not do",
    body: (
      <Points
        items={[
          "Give false information about your identity, qualifications, certifications, or the needs of a person or pet.",
          "Harass, threaten, discriminate against or endanger anyone.",
          "Arrange care, payment or communication outside GoSit.",
          "Record or capture an interview outside GoSit’s own recording.",
          "Share another person’s personal information without their consent.",
          "Use GoSit to promote anything unrelated to its services.",
          "Get around our fees, verification or safety systems.",
        ]}
      />
    ),
  },
  {
    id: "suspension",
    title: "Suspension and closing your account",
    body: (
      <p>
        We may suspend or end an account for a breach of these terms, a safety concern or a legal
        requirement, with or without notice depending on how serious it is. A suspended account is
        signed out everywhere. You can close your account at any time. Some records are kept afterwards
        as described in our privacy policy and as the law requires.
      </p>
    ),
  },
  {
    id: "warranty",
    title: "No warranty",
    body: (
      <p>
        GoSit is provided &ldquo;as is&rdquo; and &ldquo;as available&rdquo;. To the fullest extent the law permits,
        we make no promise that it will be uninterrupted or error-free, and we do not promise that
        verification, screening or any other safety measure will prevent harm. Nothing in these terms
        limits rights you have under consumer protection law that cannot be waived.
      </p>
    ),
  },
  {
    id: "liability",
    title: "Limits on our liability",
    body: (
      <p>
        To the fullest extent the law permits, GoSit is not liable for the acts or omissions of any
        caregiver or care requester, or for indirect or consequential losses, and our total liability
        for a claim is limited to the fees we kept from your bookings in the period before the claim.
        These limits do not apply where the law does not allow them, including to liability for gross
        negligence or for injury caused by our own fault.
      </p>
    ),
  },
  {
    id: "indemnity",
    title: "Your responsibility",
    body: (
      <p>
        You are responsible for your own conduct, and for the care you give or arrange. You agree to
        cover losses and reasonable costs GoSit incurs because you broke these terms or because of
        harm you caused in connection with a booking, to the extent the law allows.
      </p>
    ),
  },
  {
    id: "law",
    title: "Governing law",
    body: (
      <p>
        These terms are governed by the laws of Ontario and the federal laws of Canada that apply
        there. Disputes are for the courts of Ontario, unless the law gives you the right to go
        elsewhere.
      </p>
    ),
  },
  {
    id: "changes",
    title: "Changes to these terms",
    body: (
      <p>
        We may update these terms. We will tell you about material changes in the app or by email
        before they take effect. If you keep using GoSit after a change takes effect, you accept it.
      </p>
    ),
  },
];

export default function TermsPage() {
  return (
    <LegalDocument
      eyebrow="Legal"
      title="Terms of service"
      intro="GoSit is a marketplace that introduces families to independent caregivers. These terms set out what that means for everyone who uses it."
      sections={SECTIONS}
    />
  );
}
