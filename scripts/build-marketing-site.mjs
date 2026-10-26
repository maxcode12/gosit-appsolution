// Builds the public, brochure-only version of the GoSit website as plain static files.
//
//   node scripts/build-marketing-site.mjs     ->  .marketing-build/out
//
// It builds a COPY of the web app with the account, admin and API-proxy routes left out,
// and with NEXT_PUBLIC_MARKETING_ONLY=true so the remaining buttons become "email us"
// instead of pointing at sign-up. The real app under src/web/gosit-web is never touched.
// Deploy .marketing-build/out to any static host.

import { cpSync, existsSync, mkdirSync, rmSync } from "node:fs";
import { spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const web = path.join(root, "src", "web", "gosit-web");
const build = path.join(root, ".marketing-build");

// Everything under src/app that needs the API or a server. (marketing) stays.
const leftOut = ["(admin)", "(app)", "(auth)", "(caregiver)", "(parent)", "documents", "recordings", "session"]
  .map((name) => path.join(web, "src", "app", name));
leftOut.push(path.join(web, "src", "proxy.ts"));

function run(command, args, env = {}) {
  const result = spawnSync(command, args, {
    cwd: build,
    stdio: "inherit",
    shell: true,
    env: { ...process.env, ...env },
  });

  if (result.status !== 0) {
    console.error(`\n"${command} ${args.join(" ")}" failed with exit code ${result.status}.`);
    process.exit(result.status ?? 1);
  }
}

mkdirSync(build, { recursive: true });

for (const stale of ["src", "public", "out", ".next"]) {
  rmSync(path.join(build, stale), { recursive: true, force: true });
}

cpSync(path.join(web, "src"), path.join(build, "src"), {
  recursive: true,
  filter: (source) => !leftOut.some((skipped) => source === skipped || source.startsWith(skipped + path.sep)),
});
cpSync(path.join(web, "public"), path.join(build, "public"), { recursive: true });

for (const file of ["package.json", "package-lock.json", "tsconfig.json", "postcss.config.mjs", "next.config.ts"]) {
  cpSync(path.join(web, file), path.join(build, file));
}

if (!existsSync(path.join(build, "node_modules"))) {
  run("npm", ["ci", "--no-audit", "--no-fund"]);
}

// The contact form posts to the live API. Override MARKETING_API_URL to build against another
// (for example http://localhost:5199 to try the form locally).
run("npx", ["next", "build"], {
  NEXT_PUBLIC_MARKETING_ONLY: "true",
  NEXT_PUBLIC_API_BASE_URL: process.env.MARKETING_API_URL ?? "https://api.gosits.com",
});

console.log(`\nStatic site ready: ${path.join(build, "out")}`);
