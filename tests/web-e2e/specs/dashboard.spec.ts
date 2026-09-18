import { expect, test, type BrowserContext, type Cookie, type Page } from "@playwright/test";

/**
 * The signed-in care-requester journey.
 *
 * Most tests are tagged @api: these screens read real data, so they need the ASP.NET
 * Core API running as well as the web app.
 *
 * The suite signs up **once per worker** and reuses the session cookies. Registering
 * per test would hammer the credential endpoints and trip the auth rate limit — which
 * is a protection worth keeping switched on rather than relaxing for tests.
 */

const PASSWORD = "correct-horse-battery-staple";
const API = process.env.API_BASE_URL ?? "http://localhost:5199";

let sessionCookies: Promise<Cookie[]> | undefined;

/** Signs up a care requester through the UI and captures the session cookies. */
async function signUpOnce(context: BrowserContext): Promise<Cookie[]> {
  const page = await context.newPage();
  const email = `e2e-ui-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;

  await page.goto("/signup");
  await page.getByRole("textbox", { name: "First name" }).fill("Ada");
  await page.getByRole("textbox", { name: "Last name" }).fill("Lovelace");
  await page.getByRole("textbox", { name: "Email" }).fill(email);
  await page.getByLabel("Password").fill(PASSWORD);
  await page.getByRole("button", { name: "Sign up for free" }).click();

  // Signing up now lands on the role's own home rather than the landing page.
  await expect(page).toHaveURL(/\/dashboard$/);

  const cookies = await context.cookies();
  await page.close();

  return cookies;
}

/** Gives the page a signed-in session, creating one for this worker if needed. */
async function signIn(page: Page): Promise<void> {
  sessionCookies ??= signUpOnce(page.context());
  await page.context().addCookies(await sessionCookies);
}

test.describe("care requester dashboard", () => {
  /**
   * The gate is a convenience, not the security boundary — the API authorises every
   * request itself — but a signed-out visitor should still land on login.
   */
  test("@smoke a signed-out visitor is sent to login", async ({ page }) => {
    await page.goto("/dashboard");

    await expect(page).toHaveURL(/\/login$/);
  });

  test("@smoke saved also requires a session", async ({ page }) => {
    await page.goto("/saved");

    await expect(page).toHaveURL(/\/login$/);
  });

  test("@api a signed-in requester can search for care", async ({ page }) => {
    await signIn(page);
    await page.goto("/dashboard");

    await expect(page.getByRole("heading", { name: "Find care" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Filters" })).toBeVisible();

    // The taxonomy comes from the API, so the filter is populated from real data.
    await expect(
      page.getByRole("combobox").first().getByRole("option", { name: "Babysitting" }),
    ).toBeAttached();
  });

  /**
   * Filters live in the URL so a search can be shared, bookmarked and reloaded.
   */
  test("@api applying a filter puts it in the URL", async ({ page }) => {
    await signIn(page);
    await page.goto("/dashboard");

    await page.getByLabel("City").fill("Toronto");
    await page.getByRole("button", { name: "Apply" }).click();

    await expect(page).toHaveURL(/city=Toronto/);
    await expect(page.getByLabel("City")).toHaveValue("Toronto");
  });

  test("@api a search with no matches explains what to do next", async ({ page }) => {
    await signIn(page);

    // A city nobody is registered in.
    await page.goto("/dashboard?city=Nowheresville");

    await expect(page.getByRole("heading", { name: "No caregivers matched" })).toBeVisible();
    await expect(page.getByRole("link", { name: "Clear filters" })).toBeVisible();
  });

  test("@api navigation marks the current section", async ({ page }) => {
    await signIn(page);
    await page.goto("/saved");

    await expect(page.getByRole("link", { name: "Saved" })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  /**
   * The API returns 404 for a caregiver who is not listable — unverified, delisted or
   * suspended — as well as one that does not exist. Both must surface as not found
   * rather than an error page.
   */
  test("@api an unknown caregiver profile is not found", async ({ page }) => {
    await signIn(page);

    const response = await page.goto("/caregivers/00000000-0000-0000-0000-000000000000");

    expect(response?.status()).toBe(404);
  });

  test("@api the page never scrolls horizontally on a phone", async ({ page }) => {
    await signIn(page);
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto("/dashboard");

    const overflows = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
    );

    expect(overflows).toBe(false);
  });

  /**
   * Search results carry a first name and last initial, an approximate location, and
   * verified certifications — never a street address, email or full name. This asserts
   * the rendered page keeps that true.
   */
  test("@api search results expose no contact details or exact address", async ({ page }) => {
    await signIn(page);
    await page.goto("/dashboard");

    const body = (await page.locator("main").innerText()).toLowerCase();

    expect(body).not.toContain("@example.com");
    expect(body).not.toContain("queen st");
    expect(body).not.toContain("bloor");
  });

  test("@api the API is reachable, so these results are real", async ({ request }) => {
    const response = await request.get(`${API}/health/ready`);

    expect(response.ok()).toBe(true);
  });
});
