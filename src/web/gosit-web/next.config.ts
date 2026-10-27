import type { NextConfig } from "next";

const marketingOnly = process.env.NEXT_PUBLIC_MARKETING_ONLY === "true";

const nextConfig: NextConfig = {
  // The brochure build is plain files on a static host. Only scripts/build-marketing-site.mjs
  // sets the flag, and it builds a copy that has none of the routes needing a server.
  // The container build (Dockerfile) runs the app as its own small server.
  ...(process.env.STANDALONE_BUILD === "true" ? { output: "standalone" as const } : {}),
  ...(marketingOnly ? { output: "export" as const, images: { unoptimized: true } } : {}),
  experimental: {
    serverActions: {
      // The API accepts certificates and proof of insurance up to 10 MB. Multipart
      // framing adds a little overhead on top, so this leaves headroom rather than
      // matching that figure exactly.
      bodySizeLimit: "12mb",
    },
  },
};

export default nextConfig;
