import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { PageHeader } from "@/components/shell/page-header";
import { Chip } from "@/components/ui/chip";
import { Board } from "@/features/tasks/board";
import { getBoard } from "@/features/tasks/queries";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("nav");
  return { title: t("tasks") };
}

export default async function TasksPage() {
  const [nav, t, { board, cards, details, dueToday, me }] = await Promise.all([
    getTranslations("nav"),
    getTranslations("tasks"),
    getBoard(),
  ]);

  return (
    <>
      <PageHeader
        title={nav("tasks")}
        actions={
          <Chip tone="primary">{t("dueToday", { count: dueToday })}</Chip>
        }
      />
      <Board initial={board} cards={cards} initialDetails={details} me={me} />
    </>
  );
}
