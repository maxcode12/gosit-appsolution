import { expect, test, type BrowserContext, type Cookie, type Page } from "@playwright/test";

/**
 * Plans, referrals and reviews.
 *
 * Signs up once per worker and reuses the cookies, as the other suites do — registering
 * per test would trip the auth rate limit, which is a protection worth keeping on.
 */

const PASSWORD = "correct-horse-battery-staple";

let sessionCookies: Promise<Cookie[]> | undefined;

async function signUpOnce(context: BrowserContext): Promise<Cookie[]> {
  const page = await context.newPage();
  const email = `e2e-growth-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`;

  await page.goto("/signup");
  await page.getByRole("textbox", { name: "First name" }).fill("Rosa");
  await page.getByRole("textbox", { name: "Last name" }).fill("Parks");
  await page.getByRole("textbox", { name: "Email" }).fill(email);
  await page.getByLabel("Password").fill(PASSWORD);
  await page.getByRole("button", { name: "Sign up for free" }).click();

  await expect(page).toHaveURL(/\/dashboard$/);

  const cookies = await context.cookies();
  await page.close();

  return cookies;
}

async function signIn(page: Page): Promise<void> {
  sessionCookies ??= signUpOnce(page.context());
  await page.context().addCookies(await sessionCookies);
}

test.describe("plans", () => {
  test("@smoke the plan screen requires a session", async ({ page }) => {
    await page.goto("/account/plan");

    await expect(page).toHaveURL(/\/login$/);
  });

  /**
   * The most important assertion in this file.
   *
   * With no billing provider, subscriptions take no payment and nothing renews. The
   * screen has to say so — a pricing page that omitted it would be taking a decision
   * from someone by describing a charge that does not happen.
   *
   * It also has to name the shortfall that is left. This assertion has been rewritten
   * twice as the gap closed — first "not yet applied anywhere", then "insights not built
   * yet" — because each wording was true when written and would have become the lie it
   * existed to prevent once the product moved past it.
   */
  test("@api the plan screen says plainly that plans are a preview", async ({ page }) => {
    await signIn(page);
    await page.goto("/account/plan");

    const main = page.getByRole("main");

    await expect(main.getByRole("heading", { name: "Plans are a preview" })).toBeVisible();
    await expect(main).toContainText(/takes no payment/i);
    await expect(main).toContainText(/not how you do in search, which we do not measure yet/i);
  });

  test("@api a new account is on the free plan, named properly", async ({ page }) => {
    await signIn(page);
    await page.goto("/account/plan");

    const main = page.getByRole("main");

    // Not "FREE_REQUESTER" — the plan's display name, which the API had been falling
    // back from to the raw code.
    await expect(main).toContainText("GoSit");
    await expect(main).not.toContainText("FREE_REQUESTER");
  });

  test("@api only this side of the marketplace's plans are offered", async ({ page }) => {
    await signIn(page);
    await page.goto("/account/plan");

    const main = page.getByRole("main");

    await expect(main).toContainText("GoSit Plus");
    // The caregiver plan must never appear on a family's screen.
    await expect(main).not.toContainText("GoSit Pro");
  });
});

test.describe("referrals", () => {
  test("@smoke the referrals screen requires a session", async ({ page }) => {
    await page.goto("/account/referrals");

    await expect(page).toHaveURL(/\/login$/);
  });

  test("@api an account is given a referral code", async ({ page }) => {
    await signIn(page);
    await page.goto("/account/referrals");

    // The code is assigned lazily, so loading the page is what creates it.
    await expect(page.getByRole("main").locator("code")).toHaveText(/^[A-Z0-9]{4,}$/);
  });

  /**
   * A referrer learns that an invitation converted, never who took it up. The counts
   * are the whole disclosure.
   */
  test("@api a new account has no invitations and is told so", async ({ page }) => {
    await signIn(page);
    await page.goto("/account/referrals");

    await expect(page.getByRole("main")).toContainText(/Nobody has used your code yet/i);
  });

  /**
   * The referrals counterpart of "plans are a preview".
   *
   * Qualification is real — an invitation reaches "Counted" once the person completes a
   * booking — but nothing decides or grants a reward, so "Rewarded" is a state no code
   * path reaches. Without this sentence the column reads like a payment on its way.
   */
  test("@api the referrals screen says no reward has been paid", async ({ page }) => {
    await signIn(page);
    await page.goto("/account/referrals");

    const main = page.getByRole("main");

    await expect(main).toContainText(/has not been set yet/i);
    await expect(main).toContainText(/nothing has been paid out/i);
  });

  test("@api your own code is refused, with the reason shown", async ({ page }) => {
    await signIn(page);
    await page.goto("/account/referrals");

    const code = await page.getByRole("main").locator("code").innerText();

    await page.getByLabel("Referral code").fill(code);
    await page.getByRole("button", { name: "Apply code" }).click();

    await expect(page.getByRole("alert")).toBeVisible();
  });
});

test.describe("reviews", () => {
  /**
   * A caregiver with no reviews must not read as badly rated. Showing them as zero
   * stars would keep good people from ever getting a first booking.
   */
  test("@api an unreviewed caregiver is shown as new, not as zero stars", async ({ page }) => {
    await signIn(page);
    await page.goto("/dashboard");

    const firstCaregiver = page.getByRole("main").locator('a[href^="/caregivers/"]').first();

    if ((await firstCaregiver.count()) === 0) {
      test.skip(true, "No listable caregiver in this environment.");
    }

    await firstCaregiver.click();

    const main = page.getByRole("main");

    await expect(main.getByRole("heading", { name: "What families say" })).toBeVisible();
    await expect(main).not.toContainText("0.0");
  });
});
