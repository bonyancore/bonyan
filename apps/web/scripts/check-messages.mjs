#!/usr/bin/env node
/**
 * i18n guard. Fails CI when ar.json and en.json drift apart.
 *   1. same set of keys in every locale
 *   2. no empty strings
 *   3. same ICU argument names ({count}, {language}...) in every locale
 * Run: npm run i18n:check
 */
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";

const dir = new URL("../messages/", import.meta.url).pathname;
const files = readdirSync(dir).filter((f) => f.endsWith(".json"));
const base = "en.json";

const flat = (obj, prefix = "") =>
  Object.entries(obj).flatMap(([k, v]) =>
    typeof v === "object" && v !== null ? flat(v, `${prefix}${k}.`) : [[`${prefix}${k}`, String(v)]],
  );

// Collect top-level {arg} names; ignores the nested {...} option bodies of plural/select.
function args(message) {
  const names = new Set();
  let depth = 0;
  let start = -1;
  for (let i = 0; i < message.length; i++) {
    const c = message[i];
    if (c === "{") {
      if (depth === 0) start = i + 1;
      depth++;
    } else if (c === "}") {
      depth--;
      if (depth === 0 && start >= 0) {
        const name = message.slice(start, i).split(",")[0].trim();
        if (name) names.add(name);
        start = -1;
      }
    }
  }
  return names;
}

const data = Object.fromEntries(
  files.map((f) => [f, new Map(flat(JSON.parse(readFileSync(join(dir, f), "utf8"))))]),
);
const reference = data[base];
const errors = [];

for (const [file, map] of Object.entries(data)) {
  for (const [key, value] of map) {
    if (!value.trim()) errors.push(`${file}: empty value for "${key}"`);
  }
  if (file === base) continue;
  for (const key of reference.keys()) {
    if (!map.has(key)) errors.push(`${file}: missing key "${key}"`);
  }
  for (const key of map.keys()) {
    if (!reference.has(key)) errors.push(`${file}: extra key "${key}" (not in ${base})`);
  }
  for (const [key, value] of reference) {
    if (!map.has(key)) continue;
    const a = [...args(value)].sort().join(",");
    const b = [...args(map.get(key))].sort().join(",");
    if (a !== b) errors.push(`${file}: "${key}" has arguments {${b}} but ${base} has {${a}}`);
  }
}

if (errors.length) {
  console.error(`i18n check failed (${errors.length}):\n` + errors.map((e) => `  - ${e}`).join("\n"));
  process.exit(1);
}
console.log(`i18n check passed: ${files.length} locales, ${reference.size} keys each.`);
