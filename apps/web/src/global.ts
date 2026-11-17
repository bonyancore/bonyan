import type { formats } from "@/i18n/formats";
import type { Locale } from "@/i18n/locales";
import type messages from "../messages/en.json";

/**
 * Makes next-intl fully typed:
 *   t("nav.home")      autocompletes and fails to compile if the key does not exist
 *   useLocale()        returns "ar" | "en", not string
 *   format.number(x, "integer")   only accepts our named formats
 * Types come from en.json; scripts/check-messages.mjs guarantees ar.json matches it.
 */
declare module "next-intl" {
  interface AppConfig {
    Locale: Locale;
    Messages: typeof messages;
    Formats: typeof formats;
  }
}
