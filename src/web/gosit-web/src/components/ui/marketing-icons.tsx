import type { ReactNode } from "react";

/**
 * Line icons for the public website. Same drawing rules as icons.tsx: 24px box, 1.75
 * stroke, rounded ends, inheriting the surrounding text colour.
 */
function Icon({ children, className = "size-5" }: { children: ReactNode; className?: string }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.75}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
      className={className}
    >
      {children}
    </svg>
  );
}

type P = { className?: string };

export const ArrowRightIcon = ({ className }: P) => (
  <Icon className={className}>
    <path d="M5 12h14M13 6l6 6-6 6" />
  </Icon>
);

export const CheckIcon = ({ className }: P) => (
  <Icon className={className}>
    <path d="m5 12.5 4.5 4.5L19 7.5" />
  </Icon>
);

export const PlusIcon = ({ className }: P) => (
  <Icon className={className}>
    <path d="M12 5v14M5 12h14" />
  </Icon>
);

export const MenuIcon = ({ className }: P) => (
  <Icon className={className}>
    <path d="M4 7h16M4 12h16M4 17h16" />
  </Icon>
);

export const CloseIcon = ({ className }: P) => (
  <Icon className={className}>
    <path d="M6 6l12 12M18 6 6 18" />
  </Icon>
);

export const LockIcon = ({ className }: P) => (
  <Icon className={className}>
    <rect x="5" y="11" width="14" height="9" rx="2.5" />
    <path d="M8 11V8a4 4 0 0 1 8 0v3" />
  </Icon>
);

export const ClockIcon = ({ className }: P) => (
  <Icon className={className}>
    <circle cx="12" cy="12" r="8.5" />
    <path d="M12 7.5V12l3 2" />
  </Icon>
);

export const IdCardIcon = ({ className }: P) => (
  <Icon className={className}>
    <rect x="3" y="5" width="18" height="14" rx="2.5" />
    <circle cx="9" cy="11" r="2" />
    <path d="M6 16c.6-1.4 1.8-2 3-2s2.4.6 3 2M15 10h3M15 13h3" />
  </Icon>
);

export const VideoCameraIcon = ({ className }: P) => (
  <Icon className={className}>
    <rect x="3" y="6" width="12" height="12" rx="2.5" />
    <path d="m15 10.5 6-3v9l-6-3" />
  </Icon>
);

export const ChatIcon = ({ className }: P) => (
  <Icon className={className}>
    <path d="M4 6.5A2.5 2.5 0 0 1 6.5 4h11A2.5 2.5 0 0 1 20 6.5v7a2.5 2.5 0 0 1-2.5 2.5H10l-4 3.5V16a2.5 2.5 0 0 1-2-2.5z" />
  </Icon>
);

export const HomeIcon = ({ className }: P) => (
  <Icon className={className}>
    <path d="M4 11 12 4l8 7v8a1 1 0 0 1-1 1h-4v-5h-6v5H5a1 1 0 0 1-1-1z" />
  </Icon>
);

export const PawIcon = ({ className }: P) => (
  <Icon className={className}>
    <circle cx="6.5" cy="10" r="1.7" />
    <circle cx="10" cy="6" r="1.7" />
    <circle cx="14" cy="6" r="1.7" />
    <circle cx="17.5" cy="10" r="1.7" />
    <path d="M12 12.5c-2.6 0-4.5 2-4.5 4 0 1.5 1.3 2.2 2.6 2.2.7 0 1.2-.3 1.9-.3s1.2.3 1.9.3c1.3 0 2.6-.7 2.6-2.2 0-2-1.9-4-4.5-4z" />
  </Icon>
);

export const SparkIcon = ({ className }: P) => (
  <Icon className={className}>
    <path d="M12 3v4M12 17v4M3 12h4M17 12h4M6 6l2.5 2.5M15.5 15.5 18 18M18 6l-2.5 2.5M8.5 15.5 6 18" />
  </Icon>
);

export const WalletCardIcon = ({ className }: P) => (
  <Icon className={className}>
    <path d="M4 7.5A2.5 2.5 0 0 1 6.5 5H18a2 2 0 0 1 2 2v1H6.5A2.5 2.5 0 0 1 4 5.5" />
    <rect x="4" y="8" width="16" height="11" rx="2.5" />
    <circle cx="16" cy="13.5" r="1" />
  </Icon>
);

export const CalendarCheckIcon = ({ className }: P) => (
  <Icon className={className}>
    <rect x="4" y="5" width="16" height="15" rx="2.5" />
    <path d="M4 10h16M8 3v4M16 3v4m-7 7.5 2 2 4-4" />
  </Icon>
);

export const UsersIcon = ({ className }: P) => (
  <Icon className={className}>
    <circle cx="9" cy="9" r="3.2" />
    <path d="M3.5 19c.6-3 2.8-4.7 5.5-4.7s4.9 1.7 5.5 4.7M16 6.3a3 3 0 0 1 0 5.4M17.5 14.6c1.8.5 3 2 3.5 4.4" />
  </Icon>
);
