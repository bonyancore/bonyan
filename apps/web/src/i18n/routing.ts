import { defineRouting } from "next-intl/routing";
import { defaultLocale, locales } from "./locales";

/**
 * Locale lives in the URL (/ar/..., /en/...). Always prefixed: it is explicit,
 * shareable, cache-friendly, and avoids the ambiguity of an unprefixed default.
 * The proxy remembers the user's last choice in the NEXT_LOCALE cookie.
 */
export const routing = defineRouting({
  locales,
  defaultLocale,
  localePrefix: "always",
});
