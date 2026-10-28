import { notFound } from "next/navigation";
import * as rootParams from "next/root-params";
import { hasLocale } from "next-intl";
import { getRequestConfig } from "next-intl/server";
import { formats } from "./formats";
import { SITE_TIME_ZONE } from "./locales";
import { routing } from "./routing";

/**
 * Runs once per request on the server. The locale comes from the [locale] root
 * param, which works with Cache Components and needs no prop drilling and no
 * setRequestLocale() calls.
 */
export default getRequestConfig(async () => {
  const param = await rootParams.locale();
  if (!hasLocale(routing.locales, param)) notFound();
  const locale = param;

  return {
    locale,
    messages: (await import(`../../messages/${locale}.json`)).default,
    formats,
    // Egypt / KSA operations: dates render in site time, not the server's.
    timeZone: SITE_TIME_ZONE,
    onError(error) {
      // Missing keys fail loudly in dev; in production they degrade to the key name.
      if (process.env.NODE_ENV !== "production") console.error(error);
    },
    getMessageFallback({ namespace, key }) {
      return namespace ? `${namespace}.${key}` : key;
    },
  };
});
