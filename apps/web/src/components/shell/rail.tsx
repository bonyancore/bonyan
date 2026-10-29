import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { Locale } from "@/i18n/locales";
import { user } from "@/mocks/session";
import { RailNav } from "./rail-nav";

export function Rail() {
  const t = useTranslations("meta");
  const locale = useLocale() as Locale;
  const name = user.name[locale];

  return (
    <aside className="sticky top-0 flex h-dvh w-22 shrink-0 flex-col items-center gap-4 bg-sidebar py-5">
      <Link
        href="/"
        aria-label={t("appName")}
        className="grid size-10.5 place-items-center rounded-md bg-sidebar-primary text-sidebar-primary-foreground"
      >
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden className="size-6">
          <path d="M4 3h16v4H14v10h6v4H4v-4h6V7H4z" />
        </svg>
      </Link>
      <RailNav />
      <span
        role="img"
        aria-label={name}
        className="mt-auto grid size-10 place-items-center rounded-full bg-accent text-[15px] font-bold text-accent-foreground"
      >
        {name[0]}
      </span>
    </aside>
  );
}
