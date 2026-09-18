import type { NextConfig } from "next";

const nextConfig: NextConfig = {
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
