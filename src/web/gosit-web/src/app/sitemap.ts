import type { MetadataRoute } from "next";

// Static so the brochure build (a plain `output: "export"`) can emit it.
export const dynamic = "force-static";

const SITE = process.env.NEXT_PUBLIC_SITE_URL ?? "https://www.gosits.com";

/** The public pages only. Account screens are not for search engines. */
const PAGES = ["", "/for-families", "/for-caregivers", "/services", "/how-it-works", "/safety", "/about", "/faq", "/contact", "/privacy", "/terms"];

export default function sitemap(): MetadataRoute.Sitemap {
  return PAGES.map((path) => ({
    url: `${SITE}${path}`,
    changeFrequency: "monthly",
    priority: path === "" ? 1 : 0.7,
  }));
}
