import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { getTranslations } from "next-intl/server";
import { ComingSoon } from "@/components/shell/coming-soon";
import { PageHeader } from "@/components/shell/page-header";
import { systemTabs } from "@/config/navigation";

export const generateStaticParams = () => systemTabs.map(({ slug }) => ({ tab: slug }));

const findTab = (slug: string) => systemTabs.find((tab) => tab.slug === slug);

export async function generateMetadata({ params }: PageProps<"/[locale]/system/[tab]">): Promise<Metadata> {
  const tab = findTab((await params).tab);
  if (!tab) return {};
  const t = await getTranslations("system");
  return { title: t(tab.key) };
}

export default async function SystemTabPage({ params }: PageProps<"/[locale]/system/[tab]">) {
  const tab = findTab((await params).tab);
  if (!tab) notFound();
  const t = await getTranslations("system");

  return (
    <>
      <PageHeader title={t(tab.key)} />
      <ComingSoon />
    </>
  );
}
