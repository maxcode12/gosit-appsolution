import {
  CalendarIcon,
  HeartIcon,
  InboxIcon,
  PersonIcon,
  SearchIcon,
} from "@/components/ui/icons";
import type { NavItem } from "@/components/layout/dashboard-shell";

/**
 * Navigation for the care-requester side.
 *
 * Held in one place so every page in the section shows the same rail — a destination
 * that appears on one page and not another reads as a bug.
 */
export const careRequesterNav: NavItem[] = [
  { href: "/dashboard", label: "Find care", icon: <SearchIcon /> },
  { href: "/saved", label: "Saved", icon: <HeartIcon /> },
  { href: "/bookings", label: "Bookings", icon: <CalendarIcon /> },
  { href: "/messages", label: "Messages", icon: <InboxIcon /> },
  { href: "/account", label: "Account", icon: <PersonIcon /> },
];
