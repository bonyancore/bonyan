import type { ReactNode } from "react";
import { getTranslations } from "next-intl/server";
import { ModuleTabs } from "@/components/shell/module-tabs";
import { systemTabs } from "@/config/navigation";

export default async function SystemLayout({ children }: { children: ReactNode }) {
  const t = await getTranslations("system");
  const items = systemTabs.map(({ slug, key }) => ({ href: `/system/${slug}`, label: t(key) }));

  return (
    <>
      <ModuleTabs label={t("tabsLabel")} items={items} />
      {children}
    </>
  );
}
