"use client";

import { useTranslations } from "next-intl";
import { isActive, navItems } from "@/config/navigation";
import { Link, usePathname } from "@/i18n/navigation";
import { cn } from "@/lib/utils";

export function RailNav() {
  const t = useTranslations("nav");
  const pathname = usePathname();

  return (
    <nav aria-label={t("label")} className="flex flex-col gap-1.5">
      {navItems.map(({ key, href, icon: Icon }) => {
        const active = isActive(pathname, href);

        return (
          <Link
            key={key}
            href={href}
            aria-current={active ? "page" : undefined}
            className={cn(
              "relative flex w-18 flex-col items-center gap-1 rounded-md pt-2.5 pb-2 text-[11.5px] leading-3.5 font-medium text-sidebar-foreground transition-colors hover:bg-sidebar-accent hover:text-sidebar-accent-foreground",
              active &&
                "bg-sidebar-accent font-semibold text-sidebar-accent-foreground after:absolute after:inset-y-2.5 after:-inset-e-2 after:w-0.75 after:bg-sidebar-primary",
            )}
          >
            <Icon size={22} strokeWidth={1.5} />
            {t(key)}
          </Link>
        );
      })}
    </nav>
  );
}
