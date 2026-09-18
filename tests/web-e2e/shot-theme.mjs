/**
 * The toggle, and the case the old CSS could not express: dark chosen on a light
 * machine. Both captured from a light OS so the difference is the choice, not the
 * device.
 */
import { chromium } from "@playwright/test";

const base = process.argv[2] ?? "http://localhost:3100";

const browser = await chromium.launch();
const context = await browser.newContext({
  viewport: { width: 1440, height: 760 },
  deviceScaleFactor: 2,
  colorScheme: "light",
});

const page = await context.newPage();
await page.goto(`${base}/`, { waitUntil: "networkidle" });
await page.waitForFunction(
  () => [...document.querySelectorAll("img")].every((i) => i.complete && i.naturalWidth > 0),
  null,
  { timeout: 20000 },
);

await page.screenshot({ path: "C:/Dev/GoSit/shots/theme-following-device.png" });

const toggle = page.getByRole("button", { name: /^Theme:/ });
await toggle.click(); // system -> light
await toggle.click(); // light -> dark
await page.waitForTimeout(300);

await page.screenshot({ path: "C:/Dev/GoSit/shots/theme-dark-chosen.png" });

console.log("theme:", await page.locator("html").evaluate((el) => el.dataset.theme));
await browser.close();
