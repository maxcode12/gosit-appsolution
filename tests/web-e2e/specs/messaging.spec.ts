import { expect, test, type BrowserContext, type Cookie, type Page } from "@playwright/test";

/**
 * Messaging and the account screens that gate it.
 *
 * Signs up once per worker and reuses the cookies, for the same reason as the dashboard
 * suite: registering per test would trip the auth rate limit, which is a protection
 * worth keeping switched on rather than relaxing for tests.
 */

const PASSWORD = "correct-horse-battery-staple";

let sessionCookies: Promise<Cookie[]> | undefined;

async function signUpOnce(context: BrowserContext): Promise<Cookie[]> {
  const page = await context.newPage();
  const email = `e2e-msg-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;

  await page.goto("/signup");
  await page.getByRole("textbox", { name: "First name" }).fill("Grace");
  await page.getByRole("textbox", { name: "Last name" }).fill("Hopper");
  await page.getByRole("textbox", { name: "Email" }).fill(email);
  await page.getByLabel("Password").fill(PASSWORD);
  await page.getByRole("button", { name: "Sign up for free" }).click();

  // Signing up now lands on the role's own home rather than the landing page.
  await expect(page).toHaveURL(/\/dashboard$/);

  const cookies = await context.cookies();
  await page.close();

  return cookies;
}

async function signIn(page: Page): Promise<void> {
  sessionCookies ??= signUpOnce(page.context());
  await page.context().addCookies(await sessionCookies);
}

test.describe("messages", () => {
  test("@smoke the inbox requires a session", async ({ page }) => {
    await page.goto("/messages");

    await expect(page).toHaveURL(/\/login$/);
  });

  test("@api a new account sees an inbox that explains itself", async ({ page }) => {
    await signIn(page);
    await page.goto("/messages");

    await expect(page.getByRole("heading", { name: "No messages yet" })).toBeVisible();

    // Scoped to the content column: "Find care" is also a permanent navigation
    // destination, so an unscoped match would find the rail on every page.
    await expect(
      page.getByRole("main").getByRole("link", { name: "Find care" }),
    ).toBeVisible();
  });

  test("@api navigation marks messages as the current section", async ({ page }) => {
    await signIn(page);
    await page.goto("/messages");

    await expect(page.getByRole("link", { name: "Messages" })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  /**
   * A conversation belonging to someone else must be indistinguishable from one that
   * does not exist — a 403 would confirm the id is real.
   */
  test("@api someone else's conversation is not found", async ({ page }) => {
    await signIn(page);

    const response = await page.goto("/messages/00000000-0000-0000-0000-000000000000");

    expect(response?.status()).toBe(404);
  });

  test("@api the inbox never scrolls horizontally on a phone", async ({ page }) => {
    await signIn(page);
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto("/messages");

    const overflows = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
    );

    expect(overflows).toBe(false);
  });
});

test.describe("account", () => {
  test("@smoke the account screen requires a session", async ({ page }) => {
    await page.goto("/account");

    await expect(page).toHaveURL(/\/login$/);
  });

  /**
   * A new account has no profile. The screen must still render and be completable,
   * because every other flow sends people here to fix exactly that.
   */
  test("@api an account with no profile can still be filled in", async ({ page }) => {
    await signIn(page);
    await page.goto("/account");

    await expect(page.getByRole("heading", { name: "Account" })).toBeVisible();
    await expect(page.getByLabel("Phone number")).toBeVisible();
    await expect(page.getByLabel("Street address")).toBeVisible();

    // Recipients belong to a profile, so until one is saved this says so rather than
    // offering a form that would fail on submit.
    await expect(page.getByText("Save your details above first")).toBeVisible();
  });

  test("@api saving details then adding a care recipient works", async ({ page }) => {
    await signIn(page);
    await page.goto("/account");

    await page.getByLabel("Phone number").fill("416 555 0188");
    await page.getByLabel("Street address").fill("100 King St W");
    await page.getByLabel("City").fill("Toronto");
    await page.getByLabel("Province").fill("ON");
    await page.getByLabel("Postal code").fill("M5X 1A9");
    await page.getByRole("button", { name: "Save details" }).click();

    await expect(page.getByRole("status")).toHaveText("Saved.");

    await page.getByLabel("First name").fill("Rosa");
    await page.getByLabel("Date of birth").fill("2016-05-02");
    await page.getByRole("button", { name: "Add", exact: true }).click();

    await expect(page.getByText("Rosa")).toBeVisible();
    await expect(page.getByRole("link", { name: "Add care needs" })).toBeVisible();

    /*
     * Care needs are the most sensitive record in the product, and the page title
     * reaches browser history and tab strips — so it must not carry the recipient's
     * name. Asserted here rather than in its own test because it needs a recipient to
     * exist, and a test that depends on another test having run is a flake waiting.
     */
    await page.getByRole("link", { name: "Add care needs" }).click();

    await expect(page).toHaveTitle(/^Care needs/);
    await expect(page.getByRole("heading", { name: "Rosa's care needs" })).toBeVisible();
  });
});

test.describe("bookings", () => {
  test("@smoke bookings require a session", async ({ page }) => {
    await page.goto("/bookings");

    await expect(page).toHaveURL(/\/login$/);
  });

  test("@api an account with no bookings is told what to do next", async ({ page }) => {
    await signIn(page);
    await page.goto("/bookings");

    await expect(page.getByRole("heading", { name: "No bookings yet" })).toBeVisible();
    await expect(
      page.getByRole("main").getByRole("link", { name: "Find care" }),
    ).toBeVisible();
  });

  test("@api the past tab reads differently from the upcoming one", async ({ page }) => {
    await signIn(page);
    await page.goto("/bookings?tab=past");

    await expect(page.getByRole("heading", { name: "No past bookings" })).toBeVisible();
  });

  test("@api someone else's booking is not found", async ({ page }) => {
    await signIn(page);

    const response = await page.goto("/bookings/00000000-0000-0000-0000-000000000000");

    expect(response?.status()).toBe(404);
  });
});
