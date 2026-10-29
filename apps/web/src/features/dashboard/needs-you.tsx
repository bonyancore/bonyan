import { ArrowRight } from "lucide-react";
import { getTranslations } from "next-intl/server";
import { Card, Overline } from "@/components/ui/card";
import { Chip } from "@/components/ui/chip";
import { Link } from "@/i18n/navigation";
import { getBoard } from "@/features/tasks/queries";

export async function NeedsYou() {
  const [t, { board, cards }] = await Promise.all([getTranslations("dashboard.needsYou"), getBoard()]);

  return (
    <Card className="flex h-full flex-col gap-2 p-5">
      <Overline>{t("title")}</Overline>
      <ul className="flex flex-col">
        {board.todo.slice(0, 4).map((id) => (
          <li key={id} className="border-b border-border last:border-b-0">
            <Link href="/tasks" className="flex items-center gap-3 rounded-md py-3 transition-colors hover:bg-muted">
              <span className="min-w-0 flex-1">
                <span className="num block text-xs font-semibold text-ink-700">{cards[id].code}</span>
                <span className="block truncate text-ui font-semibold">{cards[id].title}</span>
              </span>
              <Chip tone={cards[id].due.tone} marker={false}>{cards[id].due.label}</Chip>
            </Link>
          </li>
        ))}
      </ul>
      <Link href="/tasks" className="mt-auto flex items-center gap-2 pt-2 text-ui font-semibold text-primary">
        {t("all")}
        <ArrowRight aria-hidden className="size-4 rtl:-scale-x-100" />
      </Link>
    </Card>
  );
}
