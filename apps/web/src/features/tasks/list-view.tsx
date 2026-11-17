"use client";

import { useTranslations } from "next-intl";
import { Chip } from "@/components/ui/chip";
import { columnIds, columnTones, type BoardState, type CardData } from "./types";

type ListViewProps = {
  board: BoardState;
  cards: Record<string, CardData>;
  onOpen: (id: string) => void;
};

export function ListView({ board, cards, onOpen }: ListViewProps) {
  const t = useTranslations("tasks");

  return (
    <div className="flex flex-col gap-6">
      {columnIds.map((column) => (
        <section key={column} className="flex flex-col gap-2">
          <h2 className="flex items-center gap-2 text-ui font-semibold">
            {t(`columns.${column}`)}
            <Chip tone={columnTones[column]}>
              <span className="num">{board[column].length}</span>
            </Chip>
          </h2>
          <ul className="flex flex-col overflow-hidden rounded-lg bg-card shadow-hair">
            {board[column].map((id) => {
              const card = cards[id];
              return (
                <li key={id} className="border-b border-border last:border-b-0">
                  <button
                    type="button"
                    onClick={() => onOpen(id)}
                    className="flex w-full items-center gap-4 px-4 py-3 text-start transition-colors hover:bg-muted"
                  >
                    <span className="num w-20 shrink-0 text-xs font-semibold text-ink-700">{card.code}</span>
                    <span className="min-w-0 flex-1 truncate text-ui font-semibold">{card.title}</span>
                    <Chip tone={card.tag.tone}>{card.tag.label}</Chip>
                    <span className="hidden w-24 text-caption text-ink-700 md:block">{card.assignee.name}</span>
                    <Chip tone={card.due.tone} marker={false}>{card.due.label}</Chip>
                  </button>
                </li>
              );
            })}
          </ul>
        </section>
      ))}
    </div>
  );
}
