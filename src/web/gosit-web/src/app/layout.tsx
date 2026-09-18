import type { Metadata } from "next";
import { Inter, Plus_Jakarta_Sans } from "next/font/google";
import "./globals.css";
import { THEME_INIT_SCRIPT } from "@/lib/theme";

/*
 * Two families, bound to typography *roles* in tokens.css.
 *
 * Plus Jakarta Sans for headings: humanist and slightly rounded, which reads warm
 * rather than corporate — right for a product about looking after someone's family.
 * Inter for everything else, because nothing is more legible at 13px.
 *
 * Both are variable fonts, so the full weight range costs one file each.
 */
const jakarta = Plus_Jakarta_Sans({
  variable: "--font-jakarta",
  subsets: ["latin"],
  display: "swap",
});

const inter = Inter({
  variable: "--font-inter",
  subsets: ["latin"],
  display: "swap",
});

export const metadata: Metadata = {
  title: {
    default: "GoSit — Trusted care for every stage of family life",
    template: "%s | GoSit",
  },
  description:
    "GoSit connects families across Ontario with verified caregivers for children, seniors and adults who need support.",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    // The font variables go on <html> so they exist at :root, where tokens.css maps
    // them to typography roles. On <body> they would be out of scope for :root.
    //
    // suppressHydrationWarning because the script below writes data-theme onto this
    // element before React hydrates. The attribute is deliberately absent from the
    // server's HTML — the server has no way to know what a given reader chose.
    <html
      lang="en-CA"
      className={`${jakarta.variable} ${inter.variable}`}
      suppressHydrationWarning
    >
      <head>
        {/*
          Runs before anything paints, so the first frame is already the right palette.
          Doing this in a component would paint light and correct it a frame later.
        */}
        <script dangerouslySetInnerHTML={{ __html: THEME_INIT_SCRIPT }} />
      </head>
      <body className="font-sans antialiased">{children}</body>
    </html>
  );
}
