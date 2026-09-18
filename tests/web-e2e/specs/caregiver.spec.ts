import { expect, test, type BrowserContext, type Cookie, type Page } from "@playwright/test";

/**
 * The caregiver side.
 *
 * Signs up once per worker and reuses the cookies, as the other suites do — registering
 * per test would trip the auth rate limit, which is a protection worth keeping on.
 */

const PASSWORD = "correct-horse-battery-staple";

let caregiverCookies: Promise<Cookie[]> | undefined;
let requesterCookies: Promise<Cookie[]> | undefined;

async function signUpOnce(context: BrowserContext, role: "Caregiver" | "CareRequester") {
  const page = await context.newPage();
  const prefix = role === "Caregiver" ? "e2e-cg" : "e2e-rq";
  const email = `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;

  await page.goto("/signup");
  await page.getByRole("textbox", { name: "First name" }).fill("Priya");
  await page.getByRole("textbox", { name: "Last name" }).fill("Raman");
  await page.getByRole("textbox", { name: "Email" }).fill(email);
  await page.getByLabel("Password").fill(PASSWORD);

  // The signup form offers the role; a caregiver has to choose it. The radio is
  // visually hidden behind its label, so the label is what gets clicked.
  if (role === "Caregiver") {
    await page.getByText("I am a caregiver").click();
  }

  await page.getByRole("button", { name: "Sign up for free" }).click();
  await page.waitForURL(/\/(caregiver|dashboard)$/);

  const cookies = await context.cookies();
  await page.close();

  return cookies;
}

async function signInAsCaregiver(page: Page): Promise<void> {
  caregiverCookies ??= signUpOnce(page.context(), "Caregiver");
  await page.context().addCookies(await caregiverCookies);
}

async function signInAsRequester(page: Page): Promise<void> {
  requesterCookies ??= signUpOnce(page.context(), "CareRequester");
  await page.context().addCookies(await requesterCookies);
}

/**
 * Saves a caregiver profile for the signed-in account.
 *
 * Any screen that measures a caregiver needs one to exist, and leaning on the account
 * test having already run works right up until the two land in different workers — which
 * is precisely the flake this file's own comments warn about. The save is an upsert, so
 * calling it from several tests costs a request and changes nothing.
 */
async function ensureProfile(page: Page): Promise<void> {
  await page.goto("/caregiver/account");

  await page.getByLabel("Your hourly rate (CAD)").fill("32");
  await page.getByLabel("Years of experience").fill("6");
  await page.getByLabel("Introduce yourself").fill("Six years with children and seniors.");
  await page.getByLabel("Street address").fill("12 Bloor St W");
  await page.getByLabel("City").fill("Toronto");
  await page.getByLabel("Province").fill("ON");
  await page.getByLabel("Postal code").fill("M5S 1M2");
  await page.getByLabel("How far will you travel? (km)").fill("20");

  await page.getByRole("button", { name: "Save profile" }).click();
  await expect(page.getByRole("status").first()).toHaveText("Saved.");
}

test.describe("caregiver", () => {
  test("@smoke the caregiver home requires a session", async ({ page }) => {
    await page.goto("/caregiver");

    await expect(page).toHaveURL(/\/login$/);
  });

  /**
   * Signing up as a caregiver must land on the caregiver side, not the family's search
   * screen — the two sides want completely different things.
   */
  test("@api a caregiver lands on their own home after signing up", async ({ page }) => {
    await signInAsCaregiver(page);
    await page.goto("/caregiver");

    // The caregiver rail, not the family's. Asserted on navigation rather than the
    // heading, which reads "Welcome to GoSit" until a profile exists.
    await expect(page).toHaveURL(/\/caregiver$/);
    await expect(page.getByRole("link", { name: "Earnings" })).toBeVisible();
  });

  /**
   * A brand-new caregiver has no profile at all. The screen has to say what to do
   * rather than fail on a missing profile.
   */
  test("@api a new caregiver is told to set up their profile", async ({ page }) => {
    await signInAsCaregiver(page);
    await page.goto("/caregiver");

    await expect(page.getByRole("heading", { name: "Set up your profile" })).toBeVisible();
    await expect(
      page.getByRole("main").getByRole("link", { name: "Get started" }),
    ).toBeVisible();
  });

  test("@api the account screen lets a caregiver save a profile", async ({ page }) => {
    await signInAsCaregiver(page);
    await page.goto("/caregiver/account");

    await page.getByLabel("Your hourly rate (CAD)").fill("32");
    await page.getByLabel("Years of experience").fill("6");
    await page.getByLabel("Introduce yourself").fill("Six years with children and seniors.");
    await page.getByLabel("Street address").fill("12 Bloor St W");
    await page.getByLabel("City").fill("Toronto");
    await page.getByLabel("Province").fill("ON");
    await page.getByLabel("Postal code").fill("M5S 1M2");
    await page.getByLabel("How far will you travel? (km)").fill("20");

    await page.getByRole("button", { name: "Save profile" }).click();

    await expect(page.getByRole("status").first()).toHaveText("Saved.");

    /*
     * Verification is the floor for being visible to families, so the switch that opens
     * someone to work must be inert until their checks clear — the API would refuse it
     * anyway. Asserted here rather than in its own test because the toggle only exists
     * once a profile does, and a test that depends on another test having run is a
     * flake waiting.
     */
    const toggle = page.getByRole("button", { name: "Start taking requests" });

    await expect(toggle).toBeVisible();
    await expect(toggle).toBeDisabled();
  });

  test("@api earnings explain themselves before there is anything to show", async ({ page }) => {
    await signInAsCaregiver(page);
    await page.goto("/caregiver/earnings");

    await expect(page.getByRole("heading", { name: "Earnings" })).toBeVisible();
  });

  test("@api the caregiver inbox does not offer to start a conversation", async ({ page }) => {
    await signInAsCaregiver(page);
    await page.goto("/caregiver/messages");

    await expect(page.getByRole("heading", { name: "No messages yet" })).toBeVisible();

    // A caregiver cannot initiate — POST /conversations is requester-only — so the
    // empty state must not tell them to.
    await expect(page.getByRole("main")).not.toContainText("Find care");
  });

  test("@api someone else's job is not found", async ({ page }) => {
    await signInAsCaregiver(page);

    const response = await page.goto(
      "/caregiver/bookings/00000000-0000-0000-0000-000000000000",
    );

    expect(response?.status()).toBe(404);
  });

  test("@api the caregiver home never scrolls horizontally on a phone", async ({ page }) => {
    await signInAsCaregiver(page);
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto("/caregiver");

    const overflows = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
    );

    expect(overflows).toBe(false);
  });
});

test.describe("insights", () => {
  test("@smoke insights require a session", async ({ page }) => {
    await page.goto("/caregiver/insights");

    await expect(page).toHaveURL(/\/login$/);
  });

  /**
   * A caregiver on the free plan sees what the plan offers, not an error. The API
   * refuses the endpoint outright, so the screen has to resolve entitlement before it
   * calls anything — get that wrong and the whole page is a stack trace.
   */
  test("@api the free plan is offered the feature rather than an error", async ({ page }) => {
    await signInAsCaregiver(page);
    await ensureProfile(page);
    await page.goto("/caregiver/insights");

    const main = page.getByRole("main");

    await expect(main).toContainText(/Insights are part of a paid plan/i);
    await expect(main.getByRole("link", { name: "See plans" })).toBeVisible();
  });

  /**
   * The half of the plan's promise GoSit does not keep. Without this sentence, the
   * absence of search figures reads as "nobody is finding you" rather than "we do not
   * measure that".
   */
  test("@api a subscribed caregiver sees their search figures", async ({ page }) => {
    await signInAsCaregiver(page);
    await ensureProfile(page);

    await page.goto("/caregiver/plan");

    /*
     * Two steps: the first click swaps the button for a form asking for a promo code,
     * and the form's own submit button carries the same label.
     *
     * Both clicks were originally on the same locator, which resolves to two different
     * elements before and after the swap. That is the most frequently failing test in
     * this suite under parallel load — the second click lands on the button React is in
     * the middle of replacing. Scoping the second click to the form makes the target
     * unambiguous and the wait meaningful.
     */
    await page.getByRole("button", { name: /Switch to GoSit Pro/i }).click();

    const form = page.locator("form").filter({ has: page.getByLabel("Promo code (optional)") });

    await expect(form).toBeVisible();
    await form.getByRole("button", { name: /Switch to GoSit Pro/i }).click();

    await expect(page.getByRole("main")).toContainText(/You are on this plan/i);

    await page.goto("/caregiver/insights");

    const main = page.getByRole("main");

    await expect(main).toContainText(/In search, last 90 days/i);

    /*
     * A brand-new caregiver has not been shown to anyone, and is told that rather than
     * given a row of zeroes — the same rule the booking figures follow. "0 shown, 0
     * opened, 0%" reads as a verdict on a profile that has not been tested yet.
     */
    await expect(main).toContainText(/have not appeared in anyone.s search results yet/i);

    await expect(main).toContainText(/No requests yet/i);

    /*
     * The figures are counts of appearances, never of families, and the screen says so
     * whether or not there is anything to show.
     *
     * Asserted because this is the only place a caregiver is told what is and is not
     * recorded about the people looking at them, and it is the sentence most likely to
     * be dropped as clutter by someone tidying the layout.
     */
    await expect(main).toContainText(/we do not record who searched/i);
  });
});

test.describe("the two sides stay apart", () => {
  /**
   * Each side's screens call endpoints the other role cannot use. Sending someone home
   * beats showing them a section of failures.
   */
  test("@api a care requester visiting the caregiver side is sent home", async ({ page }) => {
    await signInAsRequester(page);
    await page.goto("/caregiver");

    await expect(page).toHaveURL(/\/dashboard$/);
  });

  test("@api a caregiver visiting the family side is sent home", async ({ page }) => {
    await signInAsCaregiver(page);
    await page.goto("/dashboard");

    await expect(page).toHaveURL(/\/caregiver$/);
  });

  test("@api a caregiver cannot reach the family's bookings screen", async ({ page }) => {
    await signInAsCaregiver(page);
    await page.goto("/bookings");

    await expect(page).toHaveURL(/\/caregiver$/);
  });
});
