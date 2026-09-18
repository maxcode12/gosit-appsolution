/**
 * Inline icons for navigation and status.
 *
 * Drawn here rather than exported from Figma because the wireframes do not include an
 * icon set — these are simple geometric glyphs, always paired with a visible text label,
 * and marked aria-hidden so nothing depends on recognising them. If a designed icon set
 * arrives, replacing this file is the whole change.
 */

const base = {
  width: 20,
  height: 20,
  viewBox: "0 0 20 20",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: 1.6,
  strokeLinecap: "round" as const,
  strokeLinejoin: "round" as const,
  "aria-hidden": true,
};

export function SearchIcon() {
  return (
    <svg {...base}>
      <circle cx="9" cy="9" r="6" />
      <path d="m13.5 13.5 3.5 3.5" />
    </svg>
  );
}

export function HeartIcon({ filled = false }: { filled?: boolean }) {
  return (
    <svg {...base} fill={filled ? "currentColor" : "none"}>
      <path d="M10 16.5S3.5 12.8 3.5 8.2A3.7 3.7 0 0 1 10 5.8a3.7 3.7 0 0 1 6.5 2.4c0 4.6-6.5 8.3-6.5 8.3Z" />
    </svg>
  );
}

export function CalendarIcon() {
  return (
    <svg {...base}>
      <rect x="3" y="4.5" width="14" height="12.5" rx="2" />
      <path d="M3 8.5h14M7 3v3M13 3v3" />
    </svg>
  );
}

export function InboxIcon() {
  return (
    <svg {...base}>
      <path d="M3 11.5 5.2 4.6A1.5 1.5 0 0 1 6.6 3.5h6.8a1.5 1.5 0 0 1 1.4 1.1L17 11.5" />
      <path d="M3 11.5h3.5l1 2h5l1-2H17v3.5a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 3 15Z" />
    </svg>
  );
}

export function PersonIcon() {
  return (
    <svg {...base}>
      <circle cx="10" cy="7" r="3.2" />
      <path d="M4 16.5a6 6 0 0 1 12 0" />
    </svg>
  );
}

export function WalletIcon() {
  return (
    <svg {...base}>
      <rect x="3" y="5" width="14" height="10.5" rx="2" />
      <path d="M3 8.5h14M13.5 12h1.5" />
    </svg>
  );
}

export function ChartIcon() {
  return (
    <svg {...base}>
      <path d="M3.5 16.5h13" />
      <path d="M6.5 16.5v-5M10 16.5V5.5M13.5 16.5v-8" />
    </svg>
  );
}

export function ShieldIcon() {
  return (
    <svg {...base}>
      <path d="M10 3 16 5.2v4.3c0 3.6-2.5 6.3-6 7.5-3.5-1.2-6-3.9-6-7.5V5.2Z" />
      <path d="m7.5 9.8 1.8 1.8 3.4-3.6" />
    </svg>
  );
}

export function StarIcon({ filled = false }: { filled?: boolean }) {
  return (
    <svg {...base} fill={filled ? "currentColor" : "none"}>
      <path d="m10 3.5 2 4.1 4.5.6-3.3 3.2.8 4.5-4-2.1-4 2.1.8-4.5L3.5 8.2l4.5-.6Z" />
    </svg>
  );
}

export function PinIcon() {
  return (
    <svg {...base}>
      <path d="M10 17s5.5-4.7 5.5-8.5a5.5 5.5 0 1 0-11 0C4.5 12.3 10 17 10 17Z" />
      <circle cx="10" cy="8.5" r="2" />
    </svg>
  );
}

export function SignOutIcon() {
  return (
    <svg {...base}>
      <path d="M12.5 4.5h-6a1 1 0 0 0-1 1v9a1 1 0 0 0 1 1h6" />
      <path d="M13 10h4.5" />
      <path d="m15.5 7.5 2.5 2.5-2.5 2.5" />
    </svg>
  );
}

export function TrendIcon() {
  return (
    <svg {...base}>
      <path d="M3 14.5 7.5 10l3 3 6-6.5" />
      <path d="M13 6.5h3.5V10" />
    </svg>
  );
}

export function SunIcon() {
  return (
    <svg {...base}>
      <circle cx="10" cy="10" r="3.4" />
      <path d="M10 2.5v1.6M10 15.9v1.6M2.5 10h1.6M15.9 10h1.6M4.7 4.7l1.1 1.1M14.2 14.2l1.1 1.1M15.3 4.7l-1.1 1.1M5.8 14.2l-1.1 1.1" />
    </svg>
  );
}

export function MoonIcon() {
  return (
    <svg {...base}>
      <path d="M16 11.7A6.6 6.6 0 0 1 8.3 4a6.6 6.6 0 1 0 7.7 7.7Z" />
    </svg>
  );
}

export function MonitorIcon() {
  return (
    <svg {...base}>
      <rect x="2.8" y="4" width="14.4" height="9.2" rx="1.4" />
      <path d="M7.5 16.5h5M10 13.2v3.3" />
    </svg>
  );
}

export function TagIcon() {
  return (
    <svg {...base}>
      <path d="M9.6 3.2H16a.8.8 0 0 1 .8.8v6.4a1 1 0 0 1-.3.7l-5.4 5.4a1 1 0 0 1-1.4 0l-6.2-6.2a1 1 0 0 1 0-1.4l5.4-5.4a1 1 0 0 1 .7-.3Z" />
      <circle cx="13.2" cy="6.8" r="1.1" />
    </svg>
  );
}

export function DocumentIcon() {
  return (
    <svg {...base}>
      <path d="M11.5 2.5H6a1 1 0 0 0-1 1v13a1 1 0 0 0 1 1h8a1 1 0 0 0 1-1V6Z" />
      <path d="M11.5 2.5V6H15" />
    </svg>
  );
}

export function VideoIcon() {
  return (
    <svg {...base}>
      <rect x="2.5" y="5.5" width="10.5" height="9" rx="2" />
      <path d="m13 8.5 4.5-2.5v8L13 11.5" />
    </svg>
  );
}
