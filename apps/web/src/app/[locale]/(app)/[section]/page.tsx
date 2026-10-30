import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { getTranslations } from "next-intl/server";
import { ComingSoon } from "@/components/shell/coming-soon";
import { PageHeader } from "@/components/shell/page-header";
import { isSection, sections } from "@/config/navigation";

export const generateStaticParams = () => sections.map((section) => ({ section }));

export async function generateMetadata({ params }: PageProps<"/[locale]/[section]">): Promise<Metadata> {
  const { section } = await params;
  if (!isSection(section)) return {};
  const t = await getTranslations("nav");
  return { title: t(section) };
}

export default async function SectionPage({ params }: PageProps<"/[locale]/[section]">) {
  const { section } = await params;
  if (!isSection(section)) notFound();
  const t = await getTranslations("nav");

  return (
    <>
      <PageHeader title={t(section)} />
      <ComingSoon />
    </>
  );
}
