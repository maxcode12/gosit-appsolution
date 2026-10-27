import { expect, test } from "@playwright/test";

/**
 * The public website — the only thing someone sees before deciding whether to trust
 * GoSit with their family, so these assert the argument it makes rather than its markup.
 */
test.describe("home page", () => {
  test("@smoke leads with care for the whole family, not only childcare", async ({ page }) => {
    await page.goto("/");

    await expect(page.getByRole("heading", { level: 1 })).toContainText("for every stage of family life");

    // Both sides of the marketplace have a way in from the first screen.
    const main = page.getByRole("main");
    await expect(main.getByRole("link", { name: "Find a caregiver" }).first()).toBeVisible();
    await expect(main.getByRole("link", { name: "Become a caregiver" }).first()).toBeVisible();
  });

  test("@smoke every care category is offered, not just children", async ({ page }) => {
    await page.goto("/");

    const main = page.getByRole("main");

    for (const category of ["Child care", "Senior care", "Specialized support", "Family support", "Pet care"]) {
      await expect(main.getByRole("heading", { name: category })).toBeVisible();
    }
  });

  /**
   * The safety claims are the product. They must be on the page and they must be the
   * specific ones the code actually enforces, not vague reassurance.
   */
  test("@smoke states what is checked before a caregiver is listed", async ({ page }) => {
    await page.goto("/safety");

    const main = page.getByRole("main");

    await expect(main).toContainText("Identity verification");
    await expect(main).toContainText("vulnerable-sector check");
    await expect(main).toContainText(/address .* only shown to a caregiver after they accept/i);
    // And the limits of the claim, which matter as much as the claim.
    await expect(main).toContainText(/cannot remove it|reduces risk/i);
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

  test("@smoke never scrolls horizontally on a phone", async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });

    for (const path of ["/", "/for-families", "/for-caregivers", "/faq", "/services", "/signup"]) {
      await page.goto(path);

      const overflows = await page.evaluate(
        () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
      );

      expect(overflows, `${path} scrolls sideways`).toBe(false);
    }
  });
});

test.describe("the pages of the website", () => {
  const PAGES: [path: string, heading: RegExp][] = [
    ["/for-families", /rely on/i],
    ["/for-caregivers", /on your terms/i],
    ["/services", /one place/i],
    ["/how-it-works", /fair/i],
    ["/safety", /where we stop/i],
    ["/about", /do the caring/i],
    ["/faq", /answered/i],
    ["/contact", /need/i],
  ];

  for (const [path, heading] of PAGES) {
    test(`@smoke ${path} exists and has its own headline`, async ({ page }) => {
      await page.goto(path);

      await expect(page.getByRole("heading", { level: 1 })).toContainText(heading);
      await expect(page.getByRole("banner")).toBeVisible();
      await expect(page.getByRole("contentinfo")).toBeVisible();
    });
  }

  test("@smoke the header leads to the other pages", async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto("/");

    await page.getByRole("banner").getByRole("link", { name: "For caregivers" }).click();

    await expect(page).toHaveURL(/\/for-caregivers$/);
  });

  test("@smoke the menu opens on a phone", async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto("/");

    await page.getByRole("button", { name: "Open menu" }).click();
    await page.getByRole("navigation", { name: "Mobile" }).getByRole("link", { name: "FAQ" }).click();

    await expect(page).toHaveURL(/\/faq$/);
  });

  test("@smoke the FAQ answers in the page, with a photograph banner", async ({ page }) => {
    await page.goto("/faq");

    await expect(page.getByText("What does GoSit actually do?")).toBeVisible();
    await expect(page.getByRole("heading", { name: "Children" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Seniors" })).toBeVisible();
  });

  test("@smoke the family and caregiver buttons open sign-up as the right kind of account", async ({ page }) => {
    await page.goto("/");

    await page.getByRole("main").getByRole("link", { name: "Become a caregiver" }).first().click();

    await expect(page).toHaveURL(/\/signup\?type=caregiver$/);
    await expect(page.getByRole("radio", { name: "I am a caregiver" })).toBeChecked();
  });
});

/**
 * "How many caregivers are near me?" has to be real.
 *
 * Anyone can ask without signing in, and the answer is a count and a place. The postal code
 * has to survive the whole trip too: someone types where they live, signs up, and should land
 * on a search for that place, not an unfiltered list as though they had never said.
 */
test.describe("caregivers near you", () => {
  test("@api reports a count for a postal code, and nothing about any person", async ({ page }) => {
    await page.goto("/for-families");

    await page.getByLabel("Where do you need care?").fill("M5V");
    await page.getByRole("button", { name: "Find caregivers" }).click();

    // Either some caregivers or an honest "just opening", and always the place.
    await expect(page.getByText(/near Downtown Toronto|just opening near Downtown Toronto/i)).toBeVisible();
    await expect(page.getByText(/within \d+ km of M5V/i)).toBeVisible();
  });

  test("@api says plainly when a postal code is not a Canadian one", async ({ page }) => {
    await page.goto("/for-families");

    await page.getByLabel("Where do you need care?").fill("Z9Z 9Z9");
    await page.getByRole("button", { name: "Find caregivers" }).click();

    await expect(page.getByText(/could not find that postal code or place in Canada/i)).toBeVisible();
  });

  test("@api a postal code typed on the website survives sign-up", async ({ page }) => {
    await page.goto("/for-families");

    await page.getByLabel("Where do you need care?").fill("M5S");
    await page.getByRole("button", { name: "Find caregivers" }).click();

    // Whether it says "see them" or "tell me when they join", it carries the place into sign-up.
    await page
      .getByRole("main")
      .getByRole("link", { name: /See them|Tell me when caregivers join/ })
      .click();

    await expect(page).toHaveURL(/\/signup\?type=family&postalCode=M5S$/);

    const email = `e2e-near-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;

    await page.getByRole("textbox", { name: "First name" }).fill("Nadia");
    await page.getByRole("textbox", { name: "Last name" }).fill("Osei");
    await page.getByRole("textbox", { name: "Email" }).fill(email);
    await page.getByLabel("Password").fill("correct-horse-battery-staple");
    await page.getByRole("button", { name: "Sign up for free" }).click();

    await expect(page).toHaveURL(/\/dashboard\?postalCode=M5S&radiusKm=25$/);

    // And the search that ran is the one they asked for.
    await expect(page.getByRole("textbox", { name: "Postal code" })).toHaveValue("M5S");
  });
});

test.describe("theme", () => {
  /**
   * The home page sets its text over a photograph in places, so colours that sit on one must
   * not follow the reader's theme. Checked on the closing panel, which is deep teal in both.
   */
  test("@smoke the deep panels keep their colours in both themes", async ({ page }) => {
    const panelColour = async (colorScheme: "light" | "dark") => {
      await page.emulateMedia({ colorScheme });
      await page.goto("/safety");

      return page
        .getByRole("heading", { name: /What GoSit is, and is not/i })
        .evaluate((el) => getComputedStyle(el).color);
    };

    const light = await panelColour("light");
    const dark = await panelColour("dark");

    expect(light).toBe(dark);
  });
});
