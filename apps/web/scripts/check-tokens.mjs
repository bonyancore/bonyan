#!/usr/bin/env node
/**
 * Token sync guard: every custom --text-* size and --shadow-* name defined in
 * globals.css must be registered in src/lib/utils.ts, otherwise cn() will drop
 * classes. Run: npm run check:tokens
 */
import { readFileSync } from "node:fs";

const css = readFileSync(new URL("../src/app/globals.css", import.meta.url), "utf8");
const utils = readFileSync(new URL("../src/lib/utils.ts", import.meta.url), "utf8");

const names = (re) => [...new Set([...css.matchAll(re)].map((m) => m[1]))];
// --text-micro: 11px;  (skip --text-micro--line-height etc.)
const sizes = names(/--text-([a-z0-9]+):/g);
// Tailwind's own t-shirt names are already known to tailwind-merge.
const shadows = names(/--shadow-([a-z]+):/g).filter((n) => !["xs", "sm", "md", "lg", "xl", "2xs", "2xl"].includes(n));

const registered = (constName) => {
  const m = utils.match(new RegExp(`${constName}\\s*=\\s*\\[([^\\]]*)\\]`));
  return new Set(m ? [...m[1].matchAll(/"([^"]+)"/g)].map((x) => x[1]) : []);
};

const errors = [];
for (const [label, defined, constName] of [
  ["font size", sizes, "CUSTOM_FONT_SIZES"],
  ["shadow", shadows, "CUSTOM_SHADOWS"],
]) {
  const reg = registered(constName);
  for (const n of defined) if (!reg.has(n)) errors.push(`${label} "${n}" is in globals.css but missing from ${constName} in src/lib/utils.ts`);
  for (const n of reg) if (!defined.includes(n)) errors.push(`${label} "${n}" is in ${constName} but not defined in globals.css`);
}

if (errors.length) {
  console.error(`Token check failed (${errors.length}):\n` + errors.map((e) => `  - ${e}`).join("\n"));
  process.exit(1);
}
console.log(`Token check passed: ${sizes.length} font sizes, ${shadows.length} shadows registered.`);
