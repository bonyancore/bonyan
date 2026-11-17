"use client";

import { AnimatePresence } from "motion/react";
import * as m from "motion/react-m";
import { useDroppable } from "@dnd-kit/react";
import { useTranslations } from "next-intl";
import { Chip } from "@/components/ui/chip";
import { cn } from "@/lib/utils";
import { COLUMN_TYPE, ITEM_TYPE } from "./dnd";
import { TaskCard } from "./task-card";
import { columnTones, type CardData, type ColumnId } from "./types";

type ColumnProps = {
  id: ColumnId;
  ids: string[];
  cards: Record<string, CardData>;
  order: number;
  locked: boolean;
  onOpen: (id: string) => void;
};

export function Column({ id, ids, cards, order, locked, onOpen }: ColumnProps) {
  const t = useTranslations("tasks");
  const { ref, isDropTarget } = useDroppable({ id, type: COLUMN_TYPE, accept: ITEM_TYPE });
  const name = t(`columns.${id}`);

  return (
    <m.section
      aria-label={t("board.column", { column: name, count: ids.length })}
      initial={{ opacity: 0, y: 14 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ type: "spring", stiffness: 260, damping: 26, delay: order * 0.06 }}
      className={cn(
        "flex flex-col gap-2.5 rounded-lg bg-muted p-2 transition-colors",
        isDropTarget && "bg-accent",
      )}
    >
      <header className="flex items-center gap-2 px-2 py-1.5">
        <h2 className="text-ui font-semibold">{name}</h2>
        <Chip tone={columnTones[id]} className="ms-auto overflow-hidden">
          <AnimatePresence mode="popLayout" initial={false}>
            <m.span
              key={ids.length}
              className="num inline-block"
              initial={{ y: 10, opacity: 0 }}
              animate={{ y: 0, opacity: 1 }}
              exit={{ y: -10, opacity: 0 }}
              transition={{ duration: 0.18 }}
            >
              {ids.length}
            </m.span>
          </AnimatePresence>
        </Chip>
      </header>
      <div ref={ref} className="flex min-h-20 flex-col gap-2.5">
        {ids.map((cardId, index) => (
          <TaskCard key={cardId} card={cards[cardId]} column={id} index={index} disabled={locked} onOpen={onOpen} />
        ))}
        {ids.length === 0 && !locked && (
          <p className="m-auto text-caption text-ink-600">{t("board.empty")}</p>
        )}
      </div>
    </m.section>
  );
}
