import type { FaqItem } from "@/features/marketing/components/faq";

/**
 * Written to be accurate before it is reassuring. Every claim here is something the
 * product actually does, and the places where GoSit's role stops are stated plainly:
 * caregivers are independent, screening reduces risk without guaranteeing anything, and
 * the service is non-medical.
 */
export const FAMILY_FAQ: FaqItem[] = [
  {
    question: "What does GoSit actually do?",
    answer:
      "GoSit is a marketplace. We help you find, check and book independent caregivers, and we handle messaging, scheduling and payment around the booking. The caregiver is not a GoSit employee, and the care itself is arranged between you and them.",
  },
  {
    question: "Is it free to join?",
    answer:
      "Yes. Creating an account, browsing caregivers and messaging them costs nothing, and you are not charged until a caregiver confirms your booking. You see the full cost before you confirm.",
  },
  {
    question: "What is checked before a caregiver appears in search?",
    answer:
      "For care of people, a caregiver has to pass identity verification and a vulnerable-sector check before they can be listed. For pet care only, it is identity verification and a criminal record check. Certifications such as First Aid and CPR are reviewed by our team before they show on a profile. Screening reduces risk, but it cannot promise a particular outcome.",
  },
  {
    question: "Can I meet a caregiver before I book?",
    answer:
      "Yes. You can message them and ask for a video interview inside GoSit. Interviews are recorded for safety, and you are told that before you join. Please keep everything on the platform: arranging a call or meeting elsewhere puts you outside the protections GoSit provides.",
  },
  {
    question: "Where does care take place?",
    answer:
      "Care for people, whether children, seniors or adults, happens at your home. The one exception is pet boarding, which takes place at the caregiver's home, and a caregiver's address is only shared with the family whose boarding booking they have accepted.",
  },
  {
    question: "Who can see my family's information?",
    answer:
      "Care needs, notes and your exact address are only shown to a caregiver after they accept your booking, and never in public search. Searching shows an approximate area only.",
  },
  {
    question: "Can caregivers give medication or nursing care?",
    answer:
      "No. GoSit is for non-medical care such as company, meals, errands, mobility help, supervision and routine support. Caregivers on GoSit do not administer medication, perform nursing procedures or provide therapy. If you need regulated health care, please speak to a licensed provider.",
  },
  {
    question: "What if something goes wrong?",
    answer:
      "You can report an incident from your booking and our trust and safety team reviews it. Bookings keep a record of times, messages and payments, so there is always an account of what happened. In an emergency, call 911 first.",
  },
];

export const CAREGIVER_FAQ: FaqItem[] = [
  {
    question: "Am I a GoSit employee?",
    answer:
      "No. You are an independent caregiver. You set your own rate, choose your own hours and accept or decline every request. GoSit provides the marketplace, scheduling, messaging and payments, but does not direct how you provide care.",
  },
  {
    question: "What do I need to get listed?",
    answer:
      "Create a profile, accept the Caregiver Service Agreement, and complete the checks for the kind of care you offer: identity verification plus a vulnerable-sector check for care of people, or identity verification plus a criminal record check for pet care only. Certifications are optional but help families choose you.",
  },
  {
    question: "Do I need my own insurance?",
    answer:
      "Caregivers are responsible for their own insurance. GoSit does not insure caregivers as if they were employees. We will tell you what is expected for the services you offer before you are matched with bookings that need it.",
  },
  {
    question: "How do I get paid?",
    answer:
      "You connect a payout account once. After a booking, you are paid for the time you actually worked, based on your check-in and check-out, and you can see every fee in one statement.",
  },
  {
    question: "Can I work with more than one family?",
    answer:
      "Yes. You decide how many bookings you accept and when you are available. Nothing about GoSit stops you from working elsewhere.",
  },
];
