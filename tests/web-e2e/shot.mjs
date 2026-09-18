/**
 * Screenshots a page of the running app.
 *
 * For showing work rather than testing it — Playwright is already here and can render a
 * page faithfully, which beats describing one.
 *
 *   node shot.mjs <url> <out.png> [width] [height] [light|dark] [full|viewport] [clicks]
 *
 * `clicks` presses the theme toggle that many times before shooting, which is how to
 * photograph a state the OS setting alone cannot produce.
 */
import { chromium } from "@playwright/test";

const [
  url,
  out,
  width = "1440",
  height = "900",
  colorScheme = "light",
  extent = "full",
  themeClicks = "0",
] = process.argv.slice(2);

const browser = await chromium.launch();
const context = await browser.newContext({
  viewport: { width: Number(width), height: Number(height) },
  // Retina, so text in the screenshot is readable rather than mushy.
  deviceScaleFactor: 2,
  colorScheme,
});

const page = await context.newPage();
await page.goto(url, { waitUntil: "networkidle" });

for (let i = 0; i < Number(themeClicks); i++) {
  await page.getByRole("button", { name: /^Theme:/ }).click();
}

/*
 * Nudge every lazy image into view and wait for all of them to decode. A full-page
 * screenshot otherwise captures the placeholders it scrolled past too quickly, which
 * looks exactly like a broken image path.
 */
await page.evaluate(() => {
  for (const img of document.querySelectorAll("img")) {
    img.loading = "eager";
    img.scrollIntoView();
  }
  window.scrollTo(0, 0);
});

await page.waitForFunction(
  () => [...document.querySelectorAll("img")].every((i) => i.complete && i.naturalWidth > 0),
  null,
  { timeout: 20000 },
);

await page.screenshot({ path: out, fullPage: extent === "full" });
await browser.close();

console.log("saved", out);
