import { expect, test, type Page } from "@playwright/test";

/**
 * The staff portal.
 *
 * Unlike the other suites this cannot sign up its own account — admin accounts are
 * provisioned from configuration, never self-registered. The credentials come from the
 * environment so CI can point at whatever it bootstrapped; when they are absent the
 * signed-in tests skip rather than fail, and the guard tests still run.
 */

const ADMIN_EMAIL = process.env.ADMIN_EMAIL;
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD;

const hasAdmin = Boolean(ADMIN_EMAIL && ADMIN_PASSWORD);

async function signInAsAdmin(page: Page): Promise<void> {
  await page.goto("/login");
  await page.getByRole("textbox", { name: "Email address" }).fill(ADMIN_EMAIL!);
  await page.getByLabel("Password").fill(ADMIN_PASSWORD!);
  await page.getByRole("button", { name: "Log in" }).click();

  await expect(page).toHaveURL(/\/admin$/);
}

test.describe("staff portal", () => {
  test("@smoke the portal requires a session", async ({ page }) => {
    await page.goto("/admin");

    await expect(page).toHaveURL(/\/login$/);
  });

  /**
   * The dispute queue holds caregivers' money, so it is worth pinning that it sits
   * behind a session like the rest of the portal rather than being reachable by URL.
   */
  test("@smoke the dispute queue requires a session", async ({ page }) => {
    await page.goto("/admin/disputes");

    await expect(page).toHaveURL(/\/login$/);
  });

  /*
   * Certification documents are criminal record checks and first-aid certificates. The
   * route that serves them is not behind the proxy that guards /admin, so its own
   * refusals are the whole protection — and unlike the staff screens above, these can be
   * checked without an admin account, which is exactly why they are worth checking here.
   */
  test("@smoke a signed-out visitor cannot fetch a certification document", async ({
    page,
  }) => {
    const response = await page.goto(
      `/documents/certifications/${crypto.randomUUID()}`,
    );

    // Redirected to sign in rather than served anything.
    expect(page.url()).toMatch(/\/login$/);
    expect(response?.status()).toBeLessThan(400);
  });

  test("@api a care requester cannot fetch a certification document", async ({ page }) => {
    const email = `e2e-doc-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;

    await page.goto("/signup");
    await page.getByRole("textbox", { name: "First name" }).fill("Rosa");
    await page.getByRole("textbox", { name: "Last name" }).fill("Parks");
    await page.getByRole("textbox", { name: "Email" }).fill(email);
    await page.getByLabel("Password").fill("correct-horse-battery-staple");
    await page.getByRole("button", { name: "Sign up for free" }).click();

    await expect(page).toHaveURL(/\/dashboard$/);

    const response = await page.request.get(
      `/documents/certifications/${crypto.randomUUID()}`,
    );

    // The API refuses on the AdminOnly policy and the route passes that through
    // unchanged, so a signed-in non-admin gets a 403 rather than a file.
    expect(response.status()).toBe(403);
  });

  test.describe(() => {
    test.skip(!hasAdmin, "Set ADMIN_EMAIL and ADMIN_PASSWORD to run the staff tests.");

    /**
     * Staff land on the portal, not on a family's search screen. Admin accounts have
     * neither a care-requester nor a caregiver profile, so the other homes would be a
     * wall of empty states.
     */
    test("@api an administrator lands on the staff overview", async ({ page }) => {
      await signInAsAdmin(page);

      await expect(page.getByRole("heading", { name: "Staff overview" })).toBeVisible();
    });

    test("@api every queue is reachable from the rail", async ({ page }) => {
      await signInAsAdmin(page);

      // Matched by href rather than by name: a queue with items waiting folds the count
      // into the link's accessible name, so an exact name match breaks the moment the
      // queue is non-empty — which is precisely when it matters.
      for (const [href, heading] of [
        ["/admin/verifications", "Background checks"],
        ["/admin/certifications", "Certificates"],
        ["/admin/incidents", "Reports"],
        ["/admin/disputes", "Disputes"],
      ]) {
        await page.getByRole("navigation", { name: "Main" }).locator(`a[href="${href}"]`).click();
        await expect(page.getByRole("heading", { name: heading, level: 1 })).toBeVisible();
      }
    });

    /**
     * The queues exist to name people. A row that shows only an id cannot be decided on,
     * which is the defect this portal was built to fix.
     */
    test("@api the checks queue is not just a list of ids", async ({ page }) => {
      await signInAsAdmin(page);
      await page.goto("/admin/verifications");

      const main = page.getByRole("main");

      // Either something is waiting and it is described in words, or nothing is.
      const empty = await main.getByRole("heading", { name: "No checks waiting" }).isVisible();

      if (!empty) {
        await expect(main).toContainText(/Required before this caregiver can appear/);
      }
    });

    test("@api the portal never scrolls horizontally on a phone", async ({ page }) => {
      await signInAsAdmin(page);
      await page.setViewportSize({ width: 375, height: 812 });
      await page.goto("/admin");

      const overflows = await page.evaluate(
        () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
      );

      expect(overflows).toBe(false);
    });
  });
});
