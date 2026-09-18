"use client";

import { useEffect, useSyncExternalStore } from "react";
import { MonitorIcon, MoonIcon, SunIcon } from "@/components/ui/icons";
import {
  THEME_STORAGE_KEY,
  applyTheme,
  isThemeChoice,
  type ThemeChoice,
} from "@/lib/theme";

/**
 * Light, dark, or whatever the machine says.
 *
 * Three states rather than two, because "follow my system" is a real preference and the
 * default one — a two-way switch would silently pin someone to whichever mode they
 * happened to be in when they first pressed it, and they would never get their evening
 * dark mode back without knowing to press it again.
 *
 * One button that cycles rather than three side by side: it lives in a header next to
 * the primary action, and a segmented control there costs more room than the choice is
 * worth. The accessible name always says where you are and where the next press goes,
 * because a cycling control that only shows an icon is otherwise a guess.
 */

const NEXT: Record<ThemeChoice, ThemeChoice> = {
  system: "light",
  light: "dark",
  dark: "system",
};

const LABEL: Record<ThemeChoice, string> = {
  system: "following your device",
  light: "light",
  dark: "dark",
};

const ICON: Record<ThemeChoice, React.ReactNode> = {
  system: <MonitorIcon />,
  light: <SunIcon />,
  dark: <MoonIcon />,
};

/*
 * The stored choice is state that lives outside React — written by this component, read
 * by an inline script before React exists, and changeable from another tab. That is what
 * useSyncExternalStore is for; reading it in an effect and calling setState would be a
 * render-then-correct, which is both a lint error and a visible flicker of the wrong
 * icon.
 */
const subscribers = new Set<() => void>();

function notify(): void {
  for (const subscriber of subscribers) {
    subscriber();
  }
}

function subscribe(onChange: () => void): () => void {
  subscribers.add(onChange);
  // Another tab changing the preference. Same person, same expectation.
  window.addEventListener("storage", onChange);

  return () => {
    subscribers.delete(onChange);
    window.removeEventListener("storage", onChange);
  };
}

/**
 * What was chosen when storage cannot hold it.
 *
 * Private browsing throws on both read and write. Without this the theme would change
 * on click while the button kept reporting the old one — a control that lies about its
 * own state is worse than one that forgets between visits.
 */
let inMemoryChoice: ThemeChoice | null = null;

function readChoice(): ThemeChoice {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);

    if (isThemeChoice(stored)) {
      return stored;
    }
  } catch {
    // Fall through to whatever this visit has chosen.
  }

  return inMemoryChoice ?? "system";
}

function setChoice(choice: ThemeChoice): void {
  inMemoryChoice = choice;
  applyTheme(choice);

  try {
    localStorage.setItem(THEME_STORAGE_KEY, choice);
  } catch {
    // Remembered for this visit only.
  }

  notify();
}

/**
 * Null on the server and through hydration.
 *
 * The server cannot know what someone chose, so any guess would light the wrong icon
 * until hydration finishes. Rendering a same-sized blank keeps the header from
 * reflowing when the real control arrives.
 */
function readServerChoice(): ThemeChoice | null {
  return null;
}

export function ThemeToggle({ className = "" }: { className?: string }) {
  const choice = useSyncExternalStore(subscribe, readChoice, readServerChoice);

  // Only while following the device. An explicit choice must survive the OS changing
  // under it — that is the whole point of having made one.
  useEffect(() => {
    if (choice !== "system") {
      return;
    }

    const query = window.matchMedia("(prefers-color-scheme: dark)");
    const onChange = () => applyTheme("system");

    query.addEventListener("change", onChange);
    return () => query.removeEventListener("change", onChange);
  }, [choice]);

  if (choice === null) {
    return <div aria-hidden className={`size-9 ${className}`} />;
  }

  const next = NEXT[choice];

  return (
    <button
      type="button"
      onClick={() => setChoice(next)}
      aria-label={`Theme: ${LABEL[choice]}. Switch to ${LABEL[next]}.`}
      title={`Theme: ${LABEL[choice]}`}
      className={`grid size-9 shrink-0 place-items-center rounded-control text-text-secondary transition-colors hover:bg-surface hover:text-text-primary ${className}`}
    >
      <span aria-hidden>{ICON[choice]}</span>
    </button>
  );
}
