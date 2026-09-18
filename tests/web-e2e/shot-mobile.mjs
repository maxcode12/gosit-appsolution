/**
 * Screenshots the MAUI app's Razor components at phone size.
 *
 * The components and CSS are the real ones from GoSit.Mobile.UI, hosted in a throwaway
 * Blazor Server app so they can be seen without a device. What this does NOT prove is
 * anything platform-specific — secure storage, the location permission prompt, the
 * WebView's own quirks. Those need a device.
 */
import { chromium } from "@playwright/test";
import path from "node:path";

const [email, password, outDir, ...routes] = process.argv.slice(2);
const base = "http://localhost:5285";

const browser = await chromium.launch();
const context = await browser.newContext({
  // iPhone 15-ish, the size most of this will be read at.
  viewport: { width: 393, height: 852 },
  deviceScaleFactor: 3,
});

const page = await context.newPage();

await page.goto(`${base}/login`, { waitUntil: "networkidle" });
await page.getByLabel("Email address").fill(email);
await page.getByLabel("Password").fill(password);
await page.getByRole("button", { name: "Sign in" }).click();
await page.waitForURL(`${base}/`, { timeout: 20000 });

// Blazor Server streams the first render; give the data a beat to arrive.
await page.waitForTimeout(1200);

for (const route of routes) {
  await page.goto(`${base}${route}`, { waitUntil: "networkidle" });
  await page.waitForTimeout(900);

  const name = route.replace(/^\//, "").replace(/\//g, "-") || "today";
  const file = path.join(outDir, `mobile-${name}.png`);

  await page.screenshot({ path: file });
  console.log("saved", file);
}

await browser.close();
