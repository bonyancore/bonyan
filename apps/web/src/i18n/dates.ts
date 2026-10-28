import { SITE_TIME_ZONE } from "./locales";

/**
 * Numeric dates are ALWAYS ISO order (2026-10-22), in every language.
 * Why: the "en" locale resolves to US order (10/22/2026), which is ambiguous for
 * Egypt/KSA users (is 03/04 March or April?). ISO order is unambiguous, language
 * independent, and matches the design boards.
 * Use these for numeric dates. Use format.dateTime(date, "long") for month names.
 */
function parts(date: Date, timeZone: string, withTime: boolean) {
  const f = new Intl.DateTimeFormat("en-CA", {
    timeZone,
    numberingSystem: "latn",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    ...(withTime ? { hour: "2-digit", minute: "2-digit", hourCycle: "h23" as const } : {}),
  });
  return Object.fromEntries(f.formatToParts(date).map((p) => [p.type, p.value]));
}

/** 2026-10-22 */
export function isoDate(date: Date, timeZone: string = SITE_TIME_ZONE): string {
  const p = parts(date, timeZone, false);
  return `${p.year}-${p.month}-${p.day}`;
}

/** 2026-10-22 12:30 (24-hour) */
export function isoStamp(date: Date, timeZone: string = SITE_TIME_ZONE): string {
  const p = parts(date, timeZone, true);
  return `${p.year}-${p.month}-${p.day} ${p.hour}:${p.minute}`;
}
