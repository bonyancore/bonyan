#!/usr/bin/env node
/**
 * RTL guard. The UI must work in both directions, so physical Tailwind classes
 * (ml-, pr-, left-, text-right, rounded-l ...) are banned in favour of logical
 * ones (ms-, pe-, start-, text-start, rounded-s ...).
 * Run: npm run lint:rtl
 */
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative } from "node:path";

const root = new URL("../src/", import.meta.url).pathname;

const PHYSICAL = [
  [/^-?(ml|mr)-/, (m) => m.replace(/(ml|mr)/, (x) => (x === "ml" ? "ms" : "me"))],
  [/^(pl|pr)-/, (m) => m.replace(/(pl|pr)/, (x) => (x === "pl" ? "ps" : "pe"))],
  [/^-?(left|right)-/, (m) => m.replace(/(left|right)/, (x) => (x === "left" ? "start" : "end"))],
  [/^text-(left|right)$/, (m) => (m === "text-left" ? "text-start" : "text-end")],
  [/^float-(left|right)$/, (m) => (m === "float-left" ? "float-start" : "float-end")],
  [/^border-(l|r)(-|$)/, (m) => m.replace(/border-(l|r)/, (_, x) => (x === "l" ? "border-s" : "border-e"))],
  [/^rounded-(l|r|tl|tr|bl|br)(-|$)/, () => "rounded-s / rounded-e / rounded-ss / rounded-se / rounded-es / rounded-ee"],
  [/^(scroll-ml|scroll-mr|scroll-pl|scroll-pr)-/, () => "scroll-ms / scroll-me / scroll-ps / scroll-pe"],
];

function* walk(dir) {
  for (const name of readdirSync(dir)) {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) yield* walk(p);
    else if (/\.(tsx|ts|jsx|js)$/.test(name)) yield p;
  }
}

const strings = /(["'`])((?:\\.|(?!\1)[^\\])*)\1/gs;
const problems = [];

for (const file of walk(root)) {
  const src = readFileSync(file, "utf8");
  for (const m of src.matchAll(strings)) {
    const line = src.slice(0, m.index).split("\n").length;
    for (const token of m[2].split(/\s+/)) {
      // Strip variants (md:, hover:, rtl:) and the important marker.
      const bare = token.replace(/^(?:[\w-]+:)+/, "").replace(/^!/, "");
      for (const [re, fix] of PHYSICAL) {
        if (re.test(bare)) {
          problems.push(`${relative(root, file)}:${line}  "${token}" -> use ${fix(bare)}`);
          break;
        }
      }
    }
  }
}

if (problems.length) {
  console.error(`RTL check failed (${problems.length}):\n` + problems.map((p) => `  - ${p}`).join("\n"));
  process.exit(1);
}
console.log("RTL check passed: no physical left/right classes.");
