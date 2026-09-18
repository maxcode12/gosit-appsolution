import { expect, test } from "@playwright/test";

/**
 * Screens implemented from Figma 49:8 (Login) and 77:12 (Sign Up).
 *
 * Tests tagged @smoke need only the web app. Tests tagged @api additionally need the
 * ASP.NET Core API reachable at API_BASE_URL, so CI runs them in a separate job.
 */

test.describe("login screen", () => {
  test("@smoke shows the elements from the design", async ({ page }) => {
    await page.goto("/login");

    await expect(page.getByRole("heading", { name: "Log in to GoSit" })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Email" })).toBeVisible();
    await expect(page.getByLabel("Password")).toBeVisible();
    await expect(page.getByRole("button", { name: "Log in" })).toBeVisible();
    await expect(page.getByRole("link", { name: "Forgot password" })).toBeVisible();
    await expect(page.getByRole("link", { name: "Create an account" })).toBeVisible();
  });

  test("@smoke offers social sign-in as not yet available", async ({ page }) => {
    await page.goto("/login");

    // Rendered in the designed position but disabled, because no provider is wired up.
    for (const provider of ["Continue with Facebook", "Apple", "Google"]) {
      await expect(page.getByRole("button", { name: new RegExp(provider) })).toBeDisabled();
    }
  });

  test("@smoke links through to sign up", async ({ page }) => {
    await page.goto("/login");

    await page.getByRole("link", { name: "Create an account" }).click();

    await expect(page).toHaveURL(/\/signup$/);
    await expect(page.getByRole("heading", { name: "Welcome to GoSit" })).toBeVisible();
  });

  test("@smoke the close control returns to the landing page", async ({ page }) => {
    await page.goto("/login");

    await page.getByRole("link", { name: "Close and return home" }).click();

    await expect(page).toHaveURL(/\/$/);
  });
});

test.describe("sign up screen", () => {
  test("@smoke shows the elements from the design", async ({ page }) => {
    await page.goto("/signup");

    await expect(page.getByRole("heading", { name: "Welcome to GoSit" })).toBeVisible();
    await expect(page.getByText("Sign up for free, no commitment")).toBeVisible();
    await expect(page.getByRole("textbox", { name: "First name" })).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Last name" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Sign up for free" })).toBeVisible();
  });

  test("@smoke requires an account type to be chosen", async ({ page }) => {
    await page.goto("/signup");

    // Design gap: the wireframe has no account-type control, but the API needs a role.
    await expect(page.getByRole("radio", { name: "I need a caregiver" })).toBeChecked();
    await expect(page.getByRole("radio", { name: "I am a caregiver" })).not.toBeChecked();
  });

  test("@smoke rejects a short password before contacting the API", async ({ page }) => {
    await page.goto("/signup");

    await page.getByRole("textbox", { name: "First name" }).fill("Ada");
    await page.getByRole("textbox", { name: "Last name" }).fill("Lovelace");
    await page.getByRole("textbox", { name: "Email" }).fill("ada@example.com");
    await page.getByLabel("Password").fill("short");
    await page.getByRole("button", { name: "Sign up for free" }).click();

    await expect(page.getByText(/at least 12 characters/i)).toBeVisible();
    await expect(page).toHaveURL(/\/signup$/);
  });
});

test.describe("authentication round trip", () => {
  test("@api a new account can be created and then used to log in", async ({ page }) => {
    const email = `e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;
    const password = "correct-horse-battery-staple";

    await page.goto("/signup");
    await page.getByRole("textbox", { name: "First name" }).fill("Ada");
    await page.getByRole("textbox", { name: "Last name" }).fill("Lovelace");
    await page.getByRole("textbox", { name: "Email" }).fill(email);
    await page.getByLabel("Password").fill(password);
    await page.getByRole("button", { name: "Sign up for free" }).click();

    // Signing up lands on the home for the chosen role — a care requester by default.
    await expect(page).toHaveURL(/\/dashboard$/);

    // Session tokens must be httpOnly, so page scripts cannot read them.
    const cookies = await page.context().cookies();
    const accessToken = cookies.find((c) => c.name === "gosit_at");
    expect(accessToken?.httpOnly).toBe(true);
    expect(cookies.find((c) => c.name === "gosit_rt")?.httpOnly).toBe(true);

    await page.context().clearCookies();

    await page.goto("/login");
    await page.getByRole("textbox", { name: "Email" }).fill(email);
    await page.getByLabel("Password").fill(password);
    await page.getByRole("button", { name: "Log in" }).click();

    await expect(page).toHaveURL(/\/dashboard$/);
  });

  /**
   * Signing out has to actually end the session, not just navigate away from it.
   *
   * The refresh token is revoked server-side by the API, so the check that matters is
   * that a protected route is no longer reachable afterwards — a cleared cookie with a
   * live token behind it would look identical up to that point.
   */
  test("@api signing out ends the session", async ({ page }) => {
    const email = `e2e-logout-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;
    const password = "correct-horse-battery-staple";

    await page.goto("/signup");
    await page.getByRole("textbox", { name: "First name" }).fill("Grace");
    await page.getByRole("textbox", { name: "Last name" }).fill("Hopper");
    await page.getByRole("textbox", { name: "Email" }).fill(email);
    await page.getByLabel("Password").fill(password);
    await page.getByRole("button", { name: "Sign up for free" }).click();

    await expect(page).toHaveURL(/\/dashboard$/);

    // Reachable from every signed-in screen, because it lives in the shared shell.
    await page.goto("/account");
    await page.getByRole("button", { name: "Log out" }).click();

    await expect(page).toHaveURL(/\/login$/);

    const cookies = await page.context().cookies();
    expect(cookies.find((c) => c.name === "gosit_at")).toBeUndefined();
    expect(cookies.find((c) => c.name === "gosit_rt")).toBeUndefined();

    // The session is gone, not merely navigated away from.
    await page.goto("/account");
    await expect(page).toHaveURL(/\/login$/);
  });

  /**
   * The access cookie carries the token's own fifteen-minute expiry, so the browser
   * drops it long before someone is finished. Every signed-in screen used to throw
   * SessionExpiredError at that point and show a crash — the refresh token was sitting
   * unused in the next cookie along.
   *
   * Dropping the cookie is exactly what expiry does, so this reproduces it without
   * waiting a quarter of an hour.
   */
  test("@api an expired access token is renewed rather than crashing the page", async ({
    page,
  }) => {
    const email = `e2e-refresh-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;
    const password = "correct-horse-battery-staple";

    await page.goto("/signup");
    await page.getByRole("textbox", { name: "First name" }).fill("Ada");
    await page.getByRole("textbox", { name: "Last name" }).fill("Lovelace");
    await page.getByRole("textbox", { name: "Email" }).fill(email);
    await page.getByLabel("Password").fill(password);
    await page.getByRole("button", { name: "Sign up for free" }).click();

    await expect(page).toHaveURL(/\/dashboard$/);

    await page.context().clearCookies({ name: "gosit_at" });

    // A query string, because it must survive the round trip through the refresh step.
    await page.goto("/dashboard?city=Windsor");

    await expect(page).toHaveURL(/\/dashboard\?city=Windsor$/);
    await expect(page.getByRole("heading", { name: "Find care" })).toBeVisible();

    // Renewed, not merely tolerated.
    const cookies = await page.context().cookies();
    expect(cookies.find((c) => c.name === "gosit_at")).toBeDefined();
  });

  /**
   * With no refresh token there is nothing to renew, and the person is simply signed
   * out. The failure must still be a login screen rather than a stack trace.
   */
  test("@api a session with nothing left to renew lands on login", async ({ page }) => {
    const email = `e2e-norefresh-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;
    const password = "correct-horse-battery-staple";

    await page.goto("/signup");
    await page.getByRole("textbox", { name: "First name" }).fill("Alan");
    await page.getByRole("textbox", { name: "Last name" }).fill("Turing");
    await page.getByRole("textbox", { name: "Email" }).fill(email);
    await page.getByLabel("Password").fill(password);
    await page.getByRole("button", { name: "Sign up for free" }).click();

    await expect(page).toHaveURL(/\/dashboard$/);

    await page.context().clearCookies({ name: "gosit_at" });
    await page.context().clearCookies({ name: "gosit_rt" });

    await page.goto("/dashboard");

    await expect(page).toHaveURL(/\/login$/);
  });

  test("@api a wrong password is refused without revealing the cause", async ({ page }) => {
    await page.goto("/login");

    await page.getByRole("textbox", { name: "Email" }).fill("nobody@example.com");
    await page.getByLabel("Password").fill("definitely-not-the-password");
    await page.getByRole("button", { name: "Log in" }).click();

    // Scoped to the form: Next.js renders its own role="alert" route announcer.
    await expect(page.locator("form [role=alert]")).toContainText(
      "The email address or password is incorrect.",
    );
    await expect(page).toHaveURL(/\/login$/);
  });
});
