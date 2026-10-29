import type { Metadata } from "next";
import { NextIntlClientProvider } from "next-intl";
import { getLocale, getTranslations } from "next-intl/server";
import { fontVariables } from "@/lib/fonts";
import { getDirection, locales, type Locale } from "@/i18n/locales";
import "../globals.css";

/**
 * This IS the root layout (there is no src/app/layout.tsx on purpose):
 * <html lang dir> must come from the URL locale, and next/root-params lets any
 * server component read it. With Cache Components every locale must be listed here.
 */
export function generateStaticParams() {
  return locales.map((locale) => ({ locale }));
}

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("meta");
  return {
    title: { default: t("appName"), template: `%s · ${t("appName")}` },
    description: t("description"),
  };
}

export default async function RootLayout({ children }: LayoutProps<"/[locale]">) {
  const locale = (await getLocale()) as Locale;

  return (
    <html lang={locale} dir={getDirection(locale)} className={fontVariables}>
      <body>
        <NextIntlClientProvider>{children}</NextIntlClientProvider>
      </body>
    </html>
  );
}
