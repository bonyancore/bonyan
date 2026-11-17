import { ChevronDown } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { Suspense } from "react";
import { LocaleSwitcher } from "@/components/i18n/locale-switcher";
import type { Locale } from "@/i18n/locales";
import { cn } from "@/lib/utils";
import { project } from "@/mocks/session";
import { OnlineStatus } from "./online-status";
import { SearchBox } from "./search-box";
import { SiteDate } from "./site-date";

const segment = "flex flex-col justify-center border-e px-5 py-1.5";
const label = "text-micro font-medium text-ink-600";
const value = "text-sm font-semibold";

export function TopBar() {
  const t = useTranslations("topbar");
  const locale = useLocale() as Locale;

  return (
    <header className="flex min-h-14 items-stretch border-b bg-card">
      <button
        type="button"
        className={cn(segment, "items-start text-start transition-colors hover:bg-accent")}
      >
        <span className={label}>{t("project")}</span>
        <span className={cn(value, "flex items-center gap-1")}>
          {project.name[locale]}
          <ChevronDown size={14} aria-hidden />
        </span>
      </button>
      <SearchBox className={segment} />
      <span className="flex-1" />
      <div className={segment}>
        <span className={label}>{t("code")}</span>
        <span className={cn(value, "num")}>{project.code}</span>
      </div>
      <div className={segment}>
        <span className={label}>{t("date")}</span>
        <Suspense fallback={<span className="num invisible text-sm">0000-00-00</span>}>
          <SiteDate />
        </Suspense>
      </div>
      <div className={segment}>
        <OnlineStatus />
      </div>
      <div className="flex items-center px-5">
        <LocaleSwitcher />
      </div>
    </header>
  );
}
