import { expect, test } from "@playwright/test";

/**
 * The public landing page — the only page someone sees before deciding whether to trust
 * GoSit with their family, so these assert the argument it makes rather than its markup.
 */
test.describe("landing page", () => {
  test("@smoke leads with care for the whole family, not only childcare", async ({ page }) => {
    await page.goto("/");

    await expect(page.getByRole("heading", { level: 1 })).toContainText(
      "Trusted care for every stage",
    );

    // Both sides of the marketplace have a way in from the first screen. The family's
    // way in is the search box — it used to be a second button beside the caregiver
    // link, and the assertion moved with it.
    await expect(page.getByLabel("Where do you need care?")).toBeVisible();
    await expect(
      page.getByRole("main").getByRole("button", { name: "Find care" }),
    ).toBeVisible();
    await expect(
      page.getByRole("main").getByRole("link", { name: "Work as a caregiver" }),
    ).toBeVisible();
  });

  test("@smoke every care category is offered, not just children", async ({ page }) => {
    await page.goto("/");

    const main = page.getByRole("main");

    for (const category of [
      "Child care",
      "Senior care",
      "Specialized support",
      "Family support",
    ]) {
      await expect(main.getByRole("heading", { name: category })).toBeVisible();
    }
  });

  /**
   * The safety claims are the product. They must be on the page and they must be the
   * specific ones the code actually enforces, not vague reassurance.
   */
  test("@smoke states what is checked before a caregiver is listed", async ({ page }) => {
    await page.goto("/");

    const main = page.getByRole("main");

    await expect(main).toContainText("Vulnerable sector check");
    await expect(main).toContainText("Government ID");
    await expect(main).toContainText(/address stays private until you book/i);
  });

  /**
   * GoSit is a non-medical service. Someone arranging support for a parent with
   * dementia needs to know that before they book, so it belongs on the public page.
   */
  test("@smoke discloses the non-medical boundary", async ({ page }) => {
    await page.goto("/");

    await expect(page.getByRole("contentinfo")).toContainText("non-medical");
  });

  test("@smoke offers a way to sign in and to sign up", async ({ page }) => {
    await page.goto("/");

    const header = page.getByRole("banner");

    await expect(header.getByRole("link", { name: "Log in" })).toBeVisible();
    await expect(header.getByRole("link", { name: "Get started" })).toBeVisible();
  });

  /**
   * The photographs are the point of the page looking like anything, so a broken path
   * or a missing file should fail here rather than on someone's first visit.
   */
  test("@smoke the hero photograph actually loads", async ({ page }) => {
    await page.goto("/");

    const hero = page.getByRole("main").getByRole("img").first();

    await expect(hero).toBeVisible();

    // A broken <img> still has an alt and a box; only a decoded image has width.
    await expect
      .poll(() => hero.evaluate((img: HTMLImageElement) => img.naturalWidth))
      .toBeGreaterThan(0);
  });

  /**
   * The search box on the front page has to be real.
   *
   * Caregiver search is authenticated, so it cannot show results — but dropping what
   * someone typed would make it decoration: they say where they live, sign up, and land
   * on an unfiltered list as though they had never said. The postal code has to survive
   * the whole trip.
   */
  test("@api a postal code typed on the landing page survives sign-up", async ({ page }) => {
    await page.goto("/");

    await page.getByLabel("Where do you need care?").fill("M5S 1M2");
    await page.getByRole("button", { name: "Find care" }).click();

    await expect(page).toHaveURL(/\/signup\?postalCode=M5S(\+|%20)1M2$/);

    const email = `e2e-hero-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;

    await page.getByRole("textbox", { name: "First name" }).fill("Nadia");
    await page.getByRole("textbox", { name: "Last name" }).fill("Osei");
    await page.getByRole("textbox", { name: "Email" }).fill(email);
    await page.getByLabel("Password").fill("correct-horse-battery-staple");
    await page.getByRole("button", { name: "Sign up for free" }).click();

    await expect(page).toHaveURL(/\/dashboard\?postalCode=M5S(\+|%20)1M2$/);

    // And the search that ran is the one they asked for.
    await expect(page.getByRole("textbox", { name: "Postal code" })).toHaveValue("M5S 1M2");
  });

  /**
   * The hero sets its text over a photograph, so its colours must not follow the
   * reader's theme. A palette flip that turned the headline dark would leave it on a
   * dim living room — unreadable, and invisible to anyone developing in one theme.
   *
   * Asserted as "the same in both schemes" rather than against a specific hex, so
   * rebranding stays free and only the inversion is caught.
   */
  test("@smoke the hero headline does not follow the theme", async ({ page }) => {
    const colourIn = async (colorScheme: "light" | "dark") => {
      await page.emulateMedia({ colorScheme });
      await page.goto("/");

      return page
        .getByRole("heading", { level: 1 })
        .evaluate((el) => getComputedStyle(el).color);
    };

    const light = await colourIn("light");
    const dark = await colourIn("dark");

    expect(light).toBe(dark);
  });

  test("@smoke never scrolls horizontally on a phone", async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto("/");

    const overflows = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
    );

    expect(overflows).toBe(false);
  });
});
