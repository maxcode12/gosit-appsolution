import {
  ChartIcon,
  InboxIcon,
  PersonIcon,
  ShieldIcon,
  StarIcon,
  TagIcon,
  VideoIcon,
  WalletIcon,
} from "@/components/ui/icons";
import type { NavItem } from "@/components/layout/dashboard-shell";

/**
 * Navigation for the staff portal.
 *
 * Counts are passed in rather than fixed, because a queue with nothing in it and a queue
 * with forty people waiting are different situations and the rail is where that should
 * be visible.
 */
export function adminNav(counts?: {
  verifications?: number;
  certifications?: number;
  insurance?: number;
  incidents?: number;
  disputes?: number;
  flaggedMessages?: number;
  reportedReviews?: number;
  enquiries?: number;
}): NavItem[] {
  return [
    { href: "/admin", label: "Overview", icon: <ChartIcon /> },
    {
      href: "/admin/verifications",
      label: "Checks",
      icon: <ShieldIcon />,
      badge: counts?.verifications,
      badgeLabel: "waiting",
    },
    {
      href: "/admin/certifications",
      label: "Certificates",
      icon: <StarIcon />,
      badge: counts?.certifications,
      badgeLabel: "waiting",
    },
    {
      href: "/admin/insurance",
      label: "Insurance",
      // A second use of the shield icon reads fine here: certificates and insurance are
      // both "does this caregiver hold what they claim", one credential apart.
      icon: <ShieldIcon />,
      badge: counts?.insurance,
      badgeLabel: "waiting",
    },
    {
      href: "/admin/incidents",
      label: "Reports",
      icon: <PersonIcon />,
      badge: counts?.incidents,
      badgeLabel: "waiting",
    },
    {
      href: "/admin/disputes",
      label: "Disputes",
      icon: <WalletIcon />,
      badge: counts?.disputes,
      badgeLabel: "waiting",
    },
    {
      href: "/admin/flagged-messages",
      label: "Messages",
      icon: <InboxIcon />,
      badge: counts?.flaggedMessages,
      badgeLabel: "waiting",
    },
    {
      href: "/admin/reported-reviews",
      label: "Reviews",
      // A filled star, because Certificates already uses the outline one and two
      // identical icons in the rail would be a coin toss.
      icon: <StarIcon filled />,
      badge: counts?.reportedReviews,
      badgeLabel: "waiting",
    },
    {
      href: "/admin/enquiries",
      label: "Enquiries",
      icon: <InboxIcon />,
      badge: counts?.enquiries,
      badgeLabel: "waiting",
    },
    // No badge, for the same reason as promo codes: recordings are looked up when a
    // concern is raised, not worked through.
    { href: "/admin/interviews", label: "Interviews", icon: <VideoIcon /> },
    // No badge: promo codes are not a queue. A count here would imply something is
    // waiting on staff when nothing is.
    { href: "/admin/promo-codes", label: "Promo codes", icon: <TagIcon /> },
  ];
}
