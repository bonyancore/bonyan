"use client";

import { useLocale, useTranslations } from "next-intl";
import { cn } from "@/lib/utils";
import { LOCALES, locales } from "@/i18n/locales";
import { Link, usePathname } from "@/i18n/navigation";

export function LocaleSwitcher({ className }: { className?: string }) {
  const t = useTranslations("locale");
  const current = useLocale();
  const pathname = usePathname();

  return (
    <nav
      aria-label={t("label")}
      className={cn("inline-flex gap-0.5 rounded-md bg-segment p-0.75", className)}
    >
      {locales.map((locale) => {
        const active = locale === current;
        return (
          <Link
            key={locale}
            href={pathname}
            locale={locale}
            lang={locale}
            hrefLang={locale}
            aria-current={active ? "true" : undefined}
            aria-label={active ? undefined : t("switchTo", { language: LOCALES[locale].nativeName })}
            className={cn(
              "rounded-sm px-3.5 py-1.5 text-[13.5px] font-medium text-ink-700 transition-colors",
              active
                ? "bg-white font-semibold text-foreground shadow-[0_0_0_1px_#c6c4bd]"
                : "hover:text-foreground",
            )}
          >
            {LOCALES[locale].nativeName}
          </Link>
        );
      })}
    </nav>
  );
}
