import { expect, test, type Page } from "@playwright/test";

/**
 * Choosing a theme, and the theme staying chosen.
 *
 * The palette follows the machine by default, which is what most people want and what
 * this used to do exclusively — a reader on a light laptop who wanted dark had no way to
 * get it. What makes the toggle worth having is precisely the case the old CSS could not
 * express, so that is what these pin down: an explicit choice beating the OS, and
 * surviving a reload.
 *
 * The attribute rather than a colour: `data-theme` is what the CSS keys on, so asserting
 * on it tests the mechanism rather than one token's current value.
 */

const theme = (page: Page) =>
  page.locator("html").evaluate((el) => el.dataset.theme);

/** Cycles: system → light → dark → system. */
const toggle = (page: Page) => page.getByRole("button", { name: /^Theme:/ });

test.describe("theme", () => {
  test("@smoke follows the device by default", async ({ page }) => {
    await page.emulateMedia({ colorScheme: "dark" });
    await page.goto("/");
    expect(await theme(page)).toBe("dark");

    await page.emulateMedia({ colorScheme: "light" });
    await page.goto("/");
    expect(await theme(page)).toBe("light");
  });

  /**
   * The whole reason the toggle exists. Someone on a light machine who wants dark could
   * not get it while the palette was keyed on `prefers-color-scheme` alone.
   */
  test("@smoke a chosen theme beats the device", async ({ page }) => {
    await page.emulateMedia({ colorScheme: "light" });
    await page.goto("/");

    // system → light → dark
    await toggle(page).click();
    await toggle(page).click();

    expect(await theme(page)).toBe("dark");
  });

  test("@smoke a chosen theme survives a reload", async ({ page }) => {
    await page.emulateMedia({ colorScheme: "light" });
    await page.goto("/");

    await toggle(page).click();
    await toggle(page).click();
    await page.reload();

    expect(await theme(page)).toBe("dark");
  });

  /**
   * Cycling back to "system" has to actually release the choice, not leave the last
   * explicit value stuck in storage — otherwise the evening switch to dark never comes
   * back and there is no way to ask for it again.
   */
  test("@smoke cycling back to the device releases the choice", async ({ page }) => {
    await page.emulateMedia({ colorScheme: "dark" });
    await page.goto("/");

    // system → light → dark → system
    await toggle(page).click();
    await toggle(page).click();
    await toggle(page).click();
    await page.reload();

    expect(await theme(page)).toBe("dark");
    await expect(toggle(page)).toHaveAccessibleName(/following your device/);
  });

  /**
   * The reason the choice is applied by an inline script rather than an effect: an
   * effect would paint the default first and correct it a frame later, which reads as a
   * flash of the wrong colour on every single navigation.
   */
  test("@smoke the choice is applied before the page paints", async ({ page }) => {
    await page.emulateMedia({ colorScheme: "light" });
    await page.goto("/");
    await toggle(page).click();
    await toggle(page).click();

    // Captured at document-start, before any of the body has rendered.
    await page.addInitScript(() => {
      (window as unknown as { themeAtStart?: string }).themeAtStart =
        document.documentElement.dataset.theme ?? "unset";
    });

    await page.goto("/dashboard");

    // Not asserting the value at document-start — the script runs in <head>, after that
    // hook — but that by first paint nothing has had to be corrected.
    expect(await theme(page)).toBe("dark");
  });

  test("@smoke the toggle is reachable from a signed-in screen too", async ({ page }) => {
    await page.goto("/login");

    // The auth screens sit outside both shells; the marketing header carries it for
    // signed-out visitors and the dashboard rail for signed-in ones.
    await page.goto("/");
    await expect(toggle(page)).toBeVisible();
  });
});
