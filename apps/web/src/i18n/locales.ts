/**
 * Single source of truth for every locale the product supports.
 * Adding a language = add one entry here + one messages/<locale>.json file.
 * Nothing else in the codebase should hard-code "ar", "en" or "rtl".
 */
export const LOCALES = {
  ar: {
    dir: "rtl",
    nativeName: "العربية",
    /** Short label for tight UI such as the switcher. */
    short: "ع",
  },
  en: {
    dir: "ltr",
    nativeName: "English",
    short: "EN",
  },
} as const;

export type Locale = keyof typeof LOCALES;
export type Direction = (typeof LOCALES)[Locale]["dir"];

export const locales = Object.keys(LOCALES) as Locale[];
export const defaultLocale: Locale = "ar";

/**
 * Default "site time" for dates. Egypt observes DST, KSA does not, so this should
 * become a per-project setting once projects exist. Until then Cairo is the default.
 */
export const SITE_TIME_ZONE = "Africa/Cairo";

export function getDirection(locale: Locale): Direction {
  return LOCALES[locale].dir;
}
