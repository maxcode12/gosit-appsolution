/**
 * Signs in and screenshots a list of signed-in routes.
 *
 * Separate from shot.mjs because these pages need a session, and logging in through the
 * real form is the only way to get one the app will accept — the tokens live in httpOnly
 * cookies that no script outside the server can mint.
 *
 *   node shot-app.mjs <email> <password> <outDir> <prefix> <light|dark> <route> [route...]
 */
import { chromium } from "@playwright/test";
import path from "node:path";

const [email, password, outDir, prefix, colorScheme, ...routes] = process.argv.slice(2);

// Go Sit does not always own port 3000 — another project may be on it. Override with
// E2E_BASE_URL rather than assuming.
const base = process.env.E2E_BASE_URL ?? "http://localhost:3000";

const browser = await chromium.launch();
const context = await browser.newContext({
  viewport: { width: 1440, height: 900 },
  deviceScaleFactor: 2,
  colorScheme,
});

const page = await context.newPage();

await page.goto(`${base}/login`, { waitUntil: "networkidle" });
await page.getByRole("textbox", { name: "Email" }).fill(email);
await page.getByLabel("Password").fill(password);
await page.getByRole("button", { name: "Log in" }).click();
await page.waitForURL(/\/(dashboard|caregiver|admin)$/, { timeout: 20000 });

for (const route of routes) {
  await page.goto(`${base}${route}`, { waitUntil: "networkidle" });

  // Same lazy-image handling as shot.mjs: a full-page shot otherwise catches
  // placeholders it scrolled past too fast, which looks like a broken path.
  await page.evaluate(() => {
    for (const img of document.querySelectorAll("img")) {
      img.loading = "eager";
      img.scrollIntoView();
    }
    window.scrollTo(0, 0);
  });

  await page
    .waitForFunction(
      () => [...document.querySelectorAll("img")].every((i) => i.complete && i.naturalWidth > 0),
      null,
      { timeout: 10000 },
    )
    .catch(() => undefined);

  const name = route.replace(/^\//, "").replace(/\//g, "-") || "home";
  const file = path.join(outDir, `${prefix}-${name}.png`);

  await page.screenshot({ path: file, fullPage: true });
  console.log("saved", file);
}

await browser.close();
