import { getLocale } from "next-intl/server";
import { systemTabs } from "@/config/navigation";
import { redirect } from "@/i18n/navigation";

export default async function SystemPage() {
  redirect({ href: `/system/${systemTabs[0].slug}`, locale: await getLocale() });
}
