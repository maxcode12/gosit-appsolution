/**
 * Theme choice: follow the machine, or override it.
 *
 * Kept in one place because the same three values are written by the toggle, read by
 * the toggle, and applied by an inline script that runs before React exists — and a
 * disagreement between those three is a flash of the wrong palette.
 */

export const THEME_STORAGE_KEY = "gosit-theme";

export const THEME_CHOICES = ["system", "light", "dark"] as const;

export type ThemeChoice = (typeof THEME_CHOICES)[number];

export function isThemeChoice(value: unknown): value is ThemeChoice {
  return THEME_CHOICES.includes(value as ThemeChoice);
}

/**
 * Applies a choice to the document.
 *
 * "system" is resolved here rather than left to CSS. The dark palette is keyed on
 * `data-theme="dark"` so that choosing dark on a light machine works at all, which means
 * something has to turn "system" into a concrete answer — and the same code has to do it
 * before first paint and again whenever the OS setting changes.
 */
export function applyTheme(choice: ThemeChoice): void {
  const prefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
  const resolved = choice === "system" ? (prefersDark ? "dark" : "light") : choice;

  document.documentElement.dataset.theme = resolved;
}

/**
 * The script that runs before the page paints.
 *
 * Inlined into the document head, so it executes before any markup renders and the
 * first paint is already the right palette. Anything asynchronous here — a module
 * import, a React effect — would paint light first and correct it a frame later, which
 * is the flash this exists to prevent.
 *
 * Wrapped in try/catch because localStorage throws outright in some privacy modes, and
 * a theme preference is not worth a blank page.
 */
export const THEME_INIT_SCRIPT = `
(function () {
  try {
    var stored = localStorage.getItem(${JSON.stringify(THEME_STORAGE_KEY)});
    var choice = ${JSON.stringify(THEME_CHOICES)}.indexOf(stored) >= 0 ? stored : "system";
    var dark = choice === "dark"
      || (choice === "system" && window.matchMedia("(prefers-color-scheme: dark)").matches);
    document.documentElement.dataset.theme = dark ? "dark" : "light";
  } catch (e) {
    document.documentElement.dataset.theme = "light";
  }
})();
`.trim();
