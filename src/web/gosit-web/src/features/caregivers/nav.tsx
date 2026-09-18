import {
  CalendarIcon,
  ChartIcon,
  InboxIcon,
  PersonIcon,
  StarIcon,
  TrendIcon,
  WalletIcon,
} from "@/components/ui/icons";
import type { NavItem } from "@/components/layout/dashboard-shell";

/**
 * Navigation for the caregiver side.
 *
 * Deliberately a different set from the requester's, not a relabelling of it: a
 * caregiver's day is requests, work and money, where a requester's is search, saved and
 * bookings. Sharing one rail would have forced both into the wrong shape.
 */
export const caregiverNav: NavItem[] = [
  { href: "/caregiver", label: "Today", icon: <ChartIcon /> },
  { href: "/caregiver/bookings", label: "Work", icon: <CalendarIcon /> },
  { href: "/caregiver/messages", label: "Messages", icon: <InboxIcon /> },
  { href: "/caregiver/reviews", label: "Reviews", icon: <StarIcon /> },
  { href: "/caregiver/insights", label: "Insights", icon: <TrendIcon /> },
  { href: "/caregiver/earnings", label: "Earnings", icon: <WalletIcon /> },
  { href: "/caregiver/account", label: "Account", icon: <PersonIcon /> },
];
